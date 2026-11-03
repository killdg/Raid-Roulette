using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class LabStaffBehaviour
    {
        private sealed class State
        {
            internal int HubIndex;
            internal bool HandsReady;
            internal bool Changing;
            internal bool Submitting;
            internal Vector3? Hub;
            internal float RetryHands;
            internal float Until;
            internal float NextPath;
            internal float LastMoved;
            internal Vector3 Threat;
            internal Vector3 LastPosition;
            internal Vector3? Destination;
        }

        private static readonly Dictionary<string, State> actors = new();
        private static readonly Queue<(Vector3 Position, float At)> threats = new();
        private static Vector3[] anchors = Array.Empty<Vector3>();
        internal static void Reset()
        {
            actors.Clear();
            LabStaffHubs.Reset();
            threats.Clear();
            anchors = Array.Empty<Vector3>();
        }

        internal static void Threat(Vector3 position)
        {
            if (!LabWorkDirector.Active && !CampaignActors.Active)
            {
                return;
            }

            threats.Enqueue((position, Time.unscaledTime));
            while (threats.Count > 12)
            {
                threats.Dequeue();
            }
        }

        internal static bool IsStaffId(string id)
        {
            if (!LabWorkDirector.Active && !CampaignActors.Active)
            {
                return false;
            }

            if (actors.ContainsKey(id))
            {
                return true;
            }

            return Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.RegisteredPlayers.OfType<Player>()
                .Any(p => p.ProfileId == id && LabWorkDirector.IsStaff(p));
        }

        internal static void Register(BotOwner bot)
        {
            if (!actors.TryGetValue(bot.ProfileId, out var state))
            {
                actors[bot.ProfileId] = state = new State
                {
                    HubIndex = actors.Count,
                    NextPath = Time.unscaledTime + UnityEngine.Random.Range(0, .8f),
                    LastMoved = Time.unscaledTime,
                    LastPosition = bot.Position
                };
            }

            if (Vector3.Distance(state.LastPosition, bot.Position) > .5f)
            {
                state.LastPosition = bot.Position;
                state.LastMoved = Time.unscaledTime;
            }

            var player = bot.GetPlayer;
            if (LabWorkDirector.Active && !state.Hub.HasValue && LabStaffHubs.TryAssign(state.HubIndex, out var hub))
            {
                state.Hub = hub;
                // Initial placement only: never visibly relocate a fully active worker.
                if (bot.BotState == EBotState.PreActive)
                {
                    player.Teleport(hub + Vector3.up * 0.05f);
                    state.LastPosition = hub;
                }
            }

            if (!state.HandsReady && !state.Changing && Time.unscaledTime >= state.RetryHands)
            {
                if (player.HandsController is Player.EmptyHandsController empty)
                {
                    empty.FastForwardCurrentState();
                    state.HandsReady = true;
                }
                else
                {
                    state.Changing = true;
                    state.RetryHands = Time.unscaledTime + 3;
                    player.Proceed(
                        false,
                        result =>
                    {
                        state.Changing = false;
                        state.HandsReady = string.IsNullOrEmpty(result.Error);
                        if (state.HandsReady)
                        {
                            player.HandsController?.FastForwardCurrentState();
                        }
                    },
                        false
                    );
                }
            }

            // Remove the upper-body weapon overlay rather than posing empty hands as a knife.
            if (state.HandsReady)
            {
                // Empty hands are the completed equipment state for civilians. Native activation
                // also reads this property outside our UpdateManual readiness adapter.
                if (bot.WeaponManager != null && player.HandsController is Player.EmptyHandsController)
                {
                    bot.WeaponManager.IsReady = true;
                }

                player.SetInHands(false);
                player.MovementContext.PlayerAnimatorSetWeaponId(PlayerAnimator.EWeaponAnimationType.EmptyHands);
                player.BodyAnimatorCommon.SetLayerWeight(PlayerAnimator.TP_WEAPON_LAYER_INDEX, 0);
            }
        }

        internal static bool Panicked(BotOwner bot) => actors.TryGetValue(bot.ProfileId, out var state) && Time.unscaledTime < state.Until;
        internal static bool OwnsRoute(BotMover mover)
        {
            var owner = mover._owner;
            return owner?.HealthController?.IsAlive == true && owner.BotState == EBotState.Active && mover
                .CurrentState == EBotMoverState.Default && !owner.DoorOpener.Interacting && !owner.DoorOpener.NearDoor
                && (LabWorkDirector.Active || Panicked(owner)) && actors.TryGetValue(owner.ProfileId, out var state) && state
                .Destination is { } destination && Vector3.Distance(owner.Position, destination) > 3f;
        }

        internal static bool AllowRoute(BotOwner bot, Vector3 target)
        {
            // Native door interactions temporarily replace the roaming target with
            // the handle/doorway approach point and must be allowed to pause movement.
            if (bot.Mover.CurrentState != EBotMoverState.Default || bot.DoorOpener.Interacting || bot.DoorOpener.NearDoor)
            {
                return true;
            }

            if (!actors.TryGetValue(bot.ProfileId, out var state) || (!LabWorkDirector.Active && !Panicked(bot)) || !state
                .Destination.HasValue)
            {
                return true;
            }

            return state.Submitting || Vector3.Distance(target, state.Destination.Value) < 3f;
        }

        internal static void Observe()
        {
            while (threats.Count > 0 && Time.unscaledTime - threats.Peek().At > 25)
            {
                threats.Dequeue();
            }

            if (!Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            foreach (var player in Singleton<GameWorld>.Instance.RegisteredPlayers.OfType<Player>().Where(p => LabWorkDirector
                .IsStaff(p) && p.HealthController?.IsAlive == true))
            {
                var bot = player.AIData?.BotOwner;
                if (bot == null)
                {
                    continue;
                }

                Register(bot);
                foreach (var danger in threats)
                {
                    if (!LabStaffPolicy.Panic(Time.unscaledTime, danger.At, Vector3.Distance(player.Position, danger.Position)))
                    {
                        continue;
                    }

                    var state = actors[bot.ProfileId];
                    if (danger.At + 25 > state.Until)
                    {
                        if (Vector3.Distance(state.Threat, danger.Position) > 8)
                        {
                            state.Destination = null;
                            state.NextPath = Mathf.Min(state.NextPath, Time.unscaledTime + UnityEngine.Random.Range(0, .8f));
                        }

                        state.Threat = danger.Position;
                        state.Until = danger.At + 25;
                    }
                }
            }
        }

        private static bool DriveHub(BotOwner bot, State state)
        {
            // A missing hub must not fall back to the gift runner's whole-map routes.
            if (!state.Hub.HasValue || bot.BotState != EBotState.Active || bot.Mover == null)
            {
                return true;
            }

            if (bot.Mover.CurrentState != EBotMoverState.Default || bot.DoorOpener.Interacting || bot.DoorOpener.NearDoor)
            {
                // Keep the native stop/handle animation intact. Running resumes on
                // the existing route once the door opener returns to normal movement.
                return true;
            }

            // Native civilian updates can restore walking between route decisions.
            // Reapply running only while following a real path; never sprint in place
            // when a route is missing or the worker has reached its destination.
            bool followingRoute = state.Destination.HasValue && Vector3.Distance(bot.Position, state.Destination.Value) > 1f
                && bot.Mover.HasPathAndNoComplete;
            SetHubRunning(bot, followingRoute);
            if (Time.unscaledTime < state.NextPath)
            {
                return true;
            }

            state.NextPath = Time.unscaledTime + UnityEngine.Random.Range(3f, 5f);
            bool fleeing = Panicked(bot);
            if (state.Destination.HasValue && Vector3.Distance(bot.Position, state.Destination.Value) > 2f && bot.Mover
                .HasPathAndNoComplete && Time.unscaledTime - state.LastMoved < 6f)
            {
                return true;
            }

            var hub = state.Hub.Value;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * (fleeing ? 28f : 16f);
                var candidate = hub + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var floor, 2f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - hub
                    .y) > 1.2f || Vector3.Distance(floor.position, bot.Position) < 3f)
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(bot.Position, floor.position, NavMesh.AllAreas, path) || path
                    .status != NavMeshPathStatus.PathComplete || path.corners.Length < 2 || path.corners.Any(corner => Mathf.Abs(corner
                    .y - hub.y) > 1.5f))
                {
                    continue;
                }

                if (fleeing && Vector3.Distance(floor.position, state.Threat) < 8f)
                {
                    continue;
                }

                state.Destination = floor.position;
                state.Submitting = true;
                try
                {
                    bot.Mover.GoToByWay(path.corners, 0.7f);
                    SetHubRunning(bot, true);
                }
                finally
                {
                    state.Submitting = false;
                }

                Plugin.Trace($"[RR-LAB-HUB] staff={bot.ProfileId} hub={hub} target={floor.position} fleeing={fleeing}");
                return true;
            }

            SetHubRunning(bot, false);
            return true;
        }

        private static void SetHubRunning(BotOwner bot, bool hasRoute)
        {
            if (hasRoute)
            {
                bot.MovementResume();
                bot.SetTargetMoveSpeed(1f);
                bot.Mover.TargetPose = 1f;
                bot.Steering.LookToMovingDirection();
            }

            // Lab Work Day workers run even before gunfire. Threats still select
            // wider escape routes, but no longer control the normal running gait.
            bot.Sprint(hasRoute, false);
        }

        internal static bool Drive(BotOwner bot)
        {
            if (!actors.TryGetValue(bot.ProfileId, out var state))
            {
                return false;
            }

            if (LabWorkDirector.Active)
            {
                return DriveHub(bot, state);
            }

            if (!Panicked(bot))
            {
                return false;
            }

            if (bot.Mover == null || bot.Mover.CurrentState != EBotMoverState.Default)
            {
                return true;
            }

            bot.MovementResume();
            bot.SetTargetMoveSpeed(1);
            bot.Mover.TargetPose = 1;
            bot.Sprint(true, false);
            bot.Steering.LookToMovingDirection();
            if (Time.unscaledTime < state.NextPath)
            {
                return true;
            }

            state.NextPath = Time.unscaledTime + UnityEngine.Random.Range(2.5f, 4);
            if (state.Destination.HasValue && Vector3.Distance(bot.Position, state.Destination.Value) > 3 && bot.Mover
                .HasPathAndNoComplete && Time.unscaledTime - state.LastMoved < 6)
            {
                return true;
            }

            if (anchors.Length == 0)
            {
                anchors = LocalRaidDirector.Zones.SelectMany(z => z.SpawnPoints ?? Array.Empty<EFT.Game.Spawning.ISpawnPoint>())
                    .Select(p => p.Position).ToArray();
            }

            var away = (bot.Position - state.Threat);
            away.y = 0;
            if (away.sqrMagnitude < 1)
            {
                away = bot.GetPlayer.LookDirection;
            }

            away.Normalize();
            var candidates = anchors.Concat(new[] { bot.Position + away * 20, bot.Position + away * 35, bot.Position
                + Quaternion.Euler(0, 60, 0) * away * 25, bot.Position + Quaternion.Euler(0, -60, 0) * away * 25 }).Where(p =>
                 Vector3.Distance(p, bot.Position) > 10 && Vector3.Distance(p, bot.Position) < 80 && Vector3.Distance(p, state
                .Threat) > Vector3.Distance(bot.Position, state.Threat) + 8).OrderByDescending(p => Vector3.Distance(p, state
                .Threat) + (Physics.Linecast(p + Vector3.up, state.Threat + Vector3.up) ? 20 : 0)).Take(12);
            foreach (var point in candidates)
            {
                if (!NavMesh.SamplePosition(point, out var hit, 2, NavMesh.AllAreas))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(bot.Position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus
                    .PathComplete || path.corners.Length < 2)
                {
                    continue;
                }

                bool dangerous = false;
                for (int i = 1; i < path.corners.Length; i++)
                {
                    var a = path.corners[i - 1];
                    var d = path.corners[i] - a;
                    float t = d.sqrMagnitude > .01f ? Mathf.Clamp01(Vector3.Dot(state.Threat - a, d) / d.sqrMagnitude) : 0;
                    if (Vector3.Distance(a + d * t, state.Threat) < Mathf.Min(6, Vector3.Distance(bot.Position, state.Threat) - 1))
                    {
                        dangerous = true;
                    }
                }

                if (dangerous)
                {
                    continue;
                }

                state.Destination = hit.position;
                state.Submitting = true;
                try
                {
                    bot.Mover.GoToByWay(path.corners, .7f);
                    bot.MovementResume();
                }
                finally
                {
                    state.Submitting = false;
                }

                Plugin.Trace("[RR-LAB-FLEE] staff=" + bot.ProfileId + " destination=" + hit.position
                    + "; complete route away from gunfire");
                return true;
            }

            state.Destination = null;
            bot.Sprint(false, false);
            bot.SetPose(.5f);
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnMakingShot))]
    internal static class LabStaffGunfire
    {
        private static void Postfix(Player __instance) => LabStaffBehaviour.Threat(__instance.Position);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ApplyDamageInfo))]
    internal static class LabStaffThreatDamage
    {
        private static void Postfix(Player __instance, DamageInfo damageInfo)
        {
            if (LabWorkDirector.IsStaff(__instance))
            {
                LabStaffBehaviour.Threat(damageInfo.Player?.iPlayer?.Position ?? __instance.Position);
            }
        }
    }
}
