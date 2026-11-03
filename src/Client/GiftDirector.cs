using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal sealed class GiftDirector
    {
        internal static bool Active { get; private set; }

        private static readonly Dictionary<string, Runner> runners = new();
        public string Status => !Active ? ""
            : $"{(LabWorkDirector.Active ? "Lab Work Day" : "The Thief")}: runners observed={runners.Count}, paper " +
                $"drops={runners.Values.Sum(r => r.Drops)}, drop errors={runners.Values.Sum(r => r.DropFailures)}";
        public string Details => !Active ? "" : string.Join("\n", runners.Select(p => p.Key + ": " + p.Value.Observation));

        private GameWorld? world;
        private float nextTick;
        private sealed class Runner
        {
            internal Vector3? Destination;
            internal float NextPath;
            internal float LastMoved;
            internal float NextMovementLog;
            internal Vector3 SpawnPosition;
            internal Vector3 LastPosition;
            internal int PathFailures;
            internal double NextDrop;
            internal int Drops;
            internal int DropFailures;
            internal float NextObservation;
            internal string Observation = "Waiting for actor state";
            internal readonly Queue<Vector3> RecentDestinations = new();
            internal float BestRemaining = float.MaxValue;
            internal float LastProgress;
            internal float NextConflictLog;
            internal bool SubmittingRoute;
            internal string LastBag = "";
        }

        internal static bool IsRunner(IPlayer? player) => CampaignActors.IsCivilian(player) || Active && player?
            .IsAI == true && player.Profile?.Info?.Settings?.Role == WildSpawnType.gifter && ((player.Profile.Info
            .Nickname == "The Gift" || player.Profile.Info.Nickname == "The Thief") || LabWorkDirector.IsStaff(player));
        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(current, world))
            {
                world = current;
                Active = false;
                runners.Clear();
                nextTick = 0;
            }

            if (current == null || Plugin.Local == null)
            {
                return;
            }

            Active = Plugin.RaidContext.Contains(": the_gift") || LabWorkDirector.Active;
            if (!Active || !Singleton<AbstractGame>.Instantiated || Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 1;
            var game = Singleton<AbstractGame>.Instance;
            if (game.GameTimer?.StartDateTime == null || game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus
                .SoftStopping)
            {
                return;
            }

            double elapsed = game.GameTimer.PastTime.TotalSeconds;
            foreach (var player in current.RegisteredPlayers.OfType<Player>().Where(p => IsRunner(p) && p.HealthController?
                .IsAlive == true).ToArray())
            {
                if (!runners.TryGetValue(player.ProfileId, out var runner))
                {
                    runner = new Runner
                    {
                        NextDrop = elapsed + 30,
                        NextPath = Time.unscaledTime + (LabWorkDirector.IsStaff(player) ? UnityEngine.Random.Range(0, 3f) : 0),
                        LastPosition = player.Position,
                        SpawnPosition = player.Position,
                        LastMoved = Time.unscaledTime
                    };
                    runners[player.ProfileId] = runner;
                    Plugin.Trace($"[RR-GIFT] stage=runner-live profile={player.ProfileId}; trail every 30 seconds while alive");
                }

                try
                {
                    var owner = player.AIData?.BotOwner;
                    string rawReady = owner?.WeaponManager == null ? "missing" : NativeSpawnDiagnostics.Read(owner.WeaponManager,
                         "<IsReady>k__BackingField")?.ToString() ?? "unknown";
                    runner.Observation = $"pos={player.Position} moved={Vector3.Distance(player.Position, runner.SpawnPosition):0.0}m | owner="
                        +
                        $"{(owner == null ? "missing" : owner.BotState.ToString())} ownerMatch={IsRunner(owner?.GetPlayer)} " +
                        $"hands={player.HandsController?.GetType().Name ?? "missing"} rawWeaponReady={rawReady} ownerAlive=" +
                        $"{owner?.HealthController?.IsAlive} drops={runner.Drops}";
                    if (Time.unscaledTime >= runner.NextObservation)
                    {
                        runner.NextObservation = Time.unscaledTime + 10;
                        Plugin.Trace("[RR-GIFT] stage=actor-state " + runner.Observation);
                        var bag = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem;
                        string contents = bag == null ? "missing" : string.Join(",", bag.GetAllItems().Select(i =>
                             $"{i.TemplateId}x{i.StackObjectsCount}").OrderBy(s => s));
                        if (contents != runner.LastBag)
                        {
                            runner.LastBag = contents;
                            Plugin.Trace("[RR-GIFT] stage=backpack-state items=" + contents);
                        }
                    }

                    Drive(owner);
                    if (!LabWorkDirector.IsStaff(player) && elapsed >= runner.NextDrop && runner.Drops < 240 && runner.DropFailures < 3)
                    {
                        runner.NextDrop = elapsed + 30;
                        try
                        {
                            string id = Guid.NewGuid().ToString("N").Substring(0, 24);
                            var item = Singleton<ItemFactory>.Instance.CreateItem(id, RaidRoulette.Shared.TrailItems.ToiletPaper, null);
                            var loot = current.ThrowItem(item, player, (Vector3? )(-player.LookDirection));
                            if (loot == null)
                            {
                                throw new InvalidOperationException("Native ThrowItem returned no loot object.");
                            }

                            runner.Drops++;
                            Plugin.Trace($"[RR-GIFT] stage=paper-created item={id} count={runner.Drops} position={player.Position}");
                        }
                        catch (Exception e)
                        {
                            runner.DropFailures++;
                            Plugin.Trace("[RR-GIFT] stage=paper-failed " + e.GetBaseException().Message);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-GIFT] stage=runner-error " + e.GetBaseException().Message);
                }
            }
        }

        internal static bool OwnsRoute(BotMover mover)
        {
            if (CampaignActors.RouteOwned?.Invoke(mover) == true)
            {
                return true;
            }

            var owner = mover._owner;
            if (owner != null && LabWorkDirector.IsStaff(owner.GetPlayer) && (LabWorkDirector.Active || LabStaffBehaviour.Panicked(owner)))
            {
                return LabStaffBehaviour.OwnsRoute(mover);
            }

            return owner != null && IsRunner(owner.GetPlayer) && owner.HealthController?.IsAlive == true && owner
                .BotState == EBotState.Active && mover.CurrentState == EBotMoverState.Default && runners.TryGetValue(owner
                .ProfileId, out var runner) && runner.Destination.HasValue && Vector3.Distance(owner.Position, runner.Destination
                    .Value) > 3;
        }

        internal static bool AllowRoute(BotOwner owner, Vector3 target)
        {
            if (CampaignActors.IsCivilian(owner?.GetPlayer) || (owner?.Mover != null && CampaignActors.RouteOwned?.Invoke(owner
                .Mover) == true))
            {
                return CampaignActors.RouteAllowed?.Invoke(owner!, target) ?? true;
            }

            if (LabWorkDirector.IsStaff(owner?.GetPlayer) && owner != null && (LabWorkDirector.Active || LabStaffBehaviour.Panicked(owner)))
            {
                return LabStaffBehaviour.AllowRoute(owner, target);
            }

            if (owner?.Mover == null || !OwnsRoute(owner.Mover) || !runners.TryGetValue(owner.ProfileId, out var runner)
                || runner.SubmittingRoute || Vector3.Distance(target, runner.Destination!.Value) <= 3)
            {
                return true;
            }

            if (Time.unscaledTime >= runner.NextConflictLog)
            {
                runner.NextConflictLog = Time.unscaledTime + 10;
                Plugin.Trace($"[RR-GIFT] stage=competing-route-blocked requested={target} roam={runner.Destination}");
            }

            return false;
        }

        internal static void Drive(BotOwner? bot)
        {
            if (bot == null || !IsRunner(bot.GetPlayer) || bot.HealthController?.IsAlive != true || bot.BotState != EBotState
                .Active || !runners.TryGetValue(bot.ProfileId, out var runner))
            {
                return;
            }

            var player = bot.GetPlayer;
            if (LabWorkDirector.IsStaff(player) && LabStaffBehaviour.Drive(bot))
            {
                return;
            }

            if (Vector3.Distance(runner.LastPosition, player.Position) > 1)
            {
                runner.LastPosition = player.Position;
                runner.LastMoved = Time.unscaledTime;
            }

            // Own this event actor's idle movement, while preserving native door state.
            bot.StandBy.CanDoStandBy = false;
            bot.StandBy.StandByType = BotStandByType.active;
            bot.MovementResume();
            if (Time.unscaledTime >= runner.NextMovementLog)
            {
                runner.NextMovementLog = Time.unscaledTime + 10;
                Plugin.Trace($"[RR-GIFT] stage=movement-state profile={bot.ProfileId} position={player.Position} movedFromSpawn=" +
                    $"{Vector3.Distance(player.Position, runner.SpawnPosition):0.0} botState={bot.BotState} weaponReady=" +
                    $"{bot.WeaponManager.IsReady} pause={bot.Mover.Pause} moving={bot.Mover.IsMoving} hasPath=" +
                    $"{bot.Mover.HasPathAndNoComplete} moverState={bot.Mover.CurrentState} standby={bot.StandBy.StandByType}");
            }

            // Native movement remains responsible for doors, collisions and terrain.
            if (Time.unscaledTime >= runner.NextPath && bot.Mover.CurrentState == EBotMoverState.Default)
            {
                runner.NextPath = Time.unscaledTime + 3;
                float remaining = runner.Destination.HasValue ? Vector3.Distance(player.Position, runner.Destination.Value) : float
                    .MaxValue;
                if (remaining < runner.BestRemaining - 1)
                {
                    runner.BestRemaining = remaining;
                    runner.LastProgress = Time.unscaledTime;
                }

                bool newDestination = GiftRoamPolicy.NeedsNewDestination(runner.Destination.HasValue, remaining, Time
                    .unscaledTime - runner.LastProgress);
                if (newDestination)
                {
                    if (runner.Destination.HasValue)
                    {
                        runner.RecentDestinations.Enqueue(runner.Destination.Value);
                        while (runner.RecentDestinations.Count > 12)
                        {
                            runner.RecentDestinations.Dequeue();
                        }

                        Plugin.Trace($"[RR-GIFT] stage=route-finished reason={(remaining <= 3 ? "arrived" : "no-progress")} remaining={remaining:0.0}");
                    }

                    runner.Destination = null;
                    var anchors = LocalRaidDirector.Zones.SelectMany(z => z.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                        .ISpawnPoint>()).Select(p => p.Position).Where(pos => Vector3.Distance(pos, player.Position) > 15)
                        .OrderByDescending(pos => GiftRoamPolicy.Score(
                            Vector3.Distance(pos, player.Position),
                            runner.RecentDestinations.Count == 0 ? 200 : runner.RecentDestinations.Min(v => Vector3.Distance(v, pos))
                        ) + UnityEngine.Random.value * 5).Take(LabWorkDirector.IsStaff(player) ? 12 : 48);
                    foreach (var anchor in anchors)
                    {
                        if (!NavMesh.SamplePosition(anchor, out var hit, 3, NavMesh.AllAreas))
                        {
                            continue;
                        }

                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(player.Position, hit.position, NavMesh.AllAreas, path) || path
                            .status != NavMeshPathStatus.PathComplete)
                        {
                            continue;
                        }

                        runner.Destination = hit.position;
                        runner.BestRemaining = Vector3.Distance(player.Position, hit.position);
                        runner.LastProgress = Time.unscaledTime;
                        Plugin.Trace($"[RR-GIFT] stage=roam-destination target={hit.position} distance={runner.BestRemaining:0.0} recent={runner.RecentDestinations.Count}");
                        break;
                    }
                }

                if (!runner.Destination.HasValue)
                {
                    runner.PathFailures++;
                    runner.NextPath = Time.unscaledTime + 10;
                    if (runner.PathFailures <= 3)
                    {
                        Plugin.Trace("[RR-GIFT] stage=no-reachable-route; will retry");
                    }
                }

                if (runner.Destination.HasValue && (newDestination || !bot.Mover.HasPathAndNoComplete || Vector3.Distance(bot.Mover
                    .LastDestination(), runner.Destination.Value) > 3))
                {
                    // Submit actual NavMesh corners to the native mover. A path-status return
                    // alone did not show that a path remained active in the user's first test.
                    var route = new NavMeshPath();
                    if (bot.Mover.CurrentState == EBotMoverState.Default && NavMesh.CalculatePath(player.Position, runner.Destination
                        .Value, NavMesh.AllAreas, route) && route.status == NavMeshPathStatus.PathComplete && route.corners.Length >= 2)
                    {
                        runner.SubmittingRoute = true;
                        try
                        {
                            bot.Mover.GoToByWay(route.corners, 0.5f);
                            bot.MovementResume();
                        }
                        finally
                        {
                            runner.SubmittingRoute = false;
                        }

                        Plugin.Trace($"[RR-GIFT] stage=route-applied profile={bot.ProfileId} corners={route.corners.Length} destination=" +
                            $"{runner.Destination.Value} hasPath={bot.Mover.HasPathAndNoComplete}");
                    }
                }
            }

            bot.Exfiltration?.ResetLeaveTime();
            if (player.Physical?.Stamina != null)
            {
                player.Physical.Stamina.Current = player.Physical.Stamina.TotalCapacity.Value;
                player.Physical.Stamina.Overuse = 0;
            }

            if (runner.Destination.HasValue)
            {
                bot.Mover.TargetPose = 1;
                bot.SetTargetMoveSpeed(1);
                bot.Steering.LookToMovingDirection();
                bot.Sprint(!LabWorkDirector.IsStaff(player), false);
            }
        }
    }

    [HarmonyPatch(typeof(BotPathController), "SetCurPath")]
    internal static class GiftRouteOwnership
    {
        private static bool Prefix(BotPathController __instance, VectorTargetBotPath? __0) => __0 == null ? __instance
            ._owner?.Mover == null || !(GiftDirector.OwnsRoute(__instance._owner.Mover) || BossHuntDirector
            .OwnsRoute(__instance._owner.Mover) || SmokeRoaming.OwnsRoute(__instance._owner.Mover) || RaidBehaviorDirector
            .OwnsRoute(__instance._owner.Mover)) : GiftDirector.AllowRoute(__instance._owner, __0.TargetPoint.Position)
            && BossHuntDirector.AllowRoute(__instance._owner, __0.TargetPoint.Position) && SmokeRoaming.AllowRoute(__instance
            ._owner, __0.TargetPoint.Position) && RaidBehaviorDirector.AllowRoute(__instance._owner, __0.TargetPoint.Position);
    }

    [HarmonyPatch(typeof(BotPathController), "Stop")]
    internal static class GiftPathStop
    {
        private static bool Prefix(BotPathController __instance) => __instance._owner?.Mover == null || !(GiftDirector
            .OwnsRoute(__instance._owner.Mover) || BossHuntDirector.OwnsRoute(__instance._owner.Mover) || SmokeRoaming
            .OwnsRoute(__instance._owner.Mover) || RaidBehaviorDirector.OwnsRoute(__instance._owner.Mover));
    }

    [HarmonyPatch(typeof(BotItemTaker), "HaveItemToTake")]
    internal static class GiftNoPickupTarget
    {
        private static bool Prefix(BotItemTaker __instance, ref bool __result)
        {
            Player? player = __instance._owner?.GetPlayer;
            if (!GiftDirector.IsRunner(player) && !EventLoadoutPickup.IsRestricted(player))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class GiftNoPickup
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            "Take",
            "PickupAction",
            "RefreshClosestItems"
        }.Select(name => (System.Reflection.MethodBase)AccessTools.Method(typeof(BotItemTaker), name));
        private static bool Prefix(BotItemTaker __instance)
        {
            Player? player = __instance._owner?.GetPlayer;
            return !GiftDirector.IsRunner(player) && !EventLoadoutPickup.IsRestricted(player);
        }
    }

    [HarmonyPatch(typeof(BotOwner), "UpdateManual")]
    internal static class GiftMovement
    {
        private static void Postfix(BotOwner __instance)
        {
            try
            {
                if (GiftDirector.IsRunner(__instance.GetPlayer))
                {
                    GiftDirector.Drive(__instance);
                }

                if (BossHuntDirector.IsHunter(__instance))
                {
                    BossHuntDirector.Drive(__instance);
                }

                RaidBehaviorDirector.Drive(__instance);
            }
            catch
            { /* Director Tick records failures; do not break native AI updates. */
            }
        }
    }

    [HarmonyPatch(typeof(BotExfiltrationData), "WannaLeave")]
    internal static class GiftNoExtraction
    {
        private static bool Prefix(BotExfiltrationData __instance, ref bool __result)
        {
            if (!GiftDirector.IsRunner(__instance._owner?.GetPlayer))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(BotGiftData), "HaveUngiftedPlayerNear")]
    internal static class GiftNoHandouts
    {
        private static bool Prefix(BotGiftData __instance, ref bool __result)
        {
            if (!GiftDirector.IsRunner(__instance._owner?.GetPlayer))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(BotGiftData), "UpdateGiftTarget")]
    internal static class GiftNoTarget
    {
        private static bool Prefix(BotGiftData __instance) => !GiftDirector.IsRunner(__instance._owner?.GetPlayer);
    }

    [HarmonyPatch(typeof(BotGiftData), "CalcGiftsOnStart")]
    internal static class GiftNoInitialHandouts
    {
        private static bool Prefix(BotGiftData __instance) => !GiftDirector.IsRunner(__instance._owner?.GetPlayer);
    }

    [HarmonyPatch(typeof(BotItemDropper), "RefreshItemToDrop")]
    internal static class GiftKeepPrize
    {
        private static bool Prefix(BotItemDropper __instance) => !GiftDirector.IsRunner(__instance._owner?.GetPlayer);
    }

    // The runner has a dedicated route. Ignore idle stop/pause requests only for it,
    // while the default mover owns an unfinished route. NearDoor handling is preserved.
    [HarmonyPatch(typeof(BotMover), "Stop")]
    internal static class GiftIdleStop
    {
        private static bool Prefix(BotMover __instance) => !(GiftDirector.OwnsRoute(__instance) || BossHuntDirector
            .OwnsRoute(__instance) || SmokeRoaming.OwnsRoute(__instance) || RaidBehaviorDirector.OwnsRoute(__instance));
    }

    [HarmonyPatch(typeof(BotMover), "MovementPause")]
    internal static class GiftIdlePause
    {
        private static bool Prefix(BotMover __instance) => !(GiftDirector.OwnsRoute(__instance) || BossHuntDirector
            .OwnsRoute(__instance) || SmokeRoaming.OwnsRoute(__instance) || RaidBehaviorDirector.OwnsRoute(__instance));
    }

    [HarmonyPatch(typeof(BotMover), "MovePlayer")]
    internal static class GiftBeforeMovement
    {
        private static void Prefix(BotMover __instance)
        {
            try
            {
                if (GiftDirector.IsRunner(__instance._owner?.GetPlayer))
                {
                    GiftDirector.Drive(__instance._owner);
                }

                if (BossHuntDirector.IsHunter(__instance._owner))
                {
                    BossHuntDirector.Drive(__instance._owner);
                }

                RaidBehaviorDirector.Drive(__instance._owner);
            }
            catch
            { /* Do not interrupt native movement. */
            }
        }
    }

    // Patch the caller: Mono can inline the trivial IsReady getter before a getter
    // postfix takes effect. Preserve native activation, including its NavMesh check.
    [HarmonyPatch(typeof(BotOwner), "UpdateManual")]
    internal static class GiftUnarmedActivation
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var getter = AccessTools.PropertyGetter(typeof(BotWeaponManager), nameof(BotWeaponManager.IsReady));
            var matches = code.Where(i => i.Calls(getter)).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException($"Gift activation requires exactly one readiness check in BotOwner.UpdateManual; found {matches.Length}.");
            }

            // Mutate the existing instruction to retain branch labels and exception blocks.
            matches[0].opcode = OpCodes.Call;
            matches[0].operand = AccessTools.Method(typeof(GiftUnarmedActivation), nameof(ReadyForActivation));
            Plugin.Trace("[RR-GIFT] stage=activation-hook-installed checks=1");
            return code;
        }

        private static bool ReadyForActivation(BotWeaponManager manager)
        {
            if (manager.IsReady)
            {
                return true;
            }

            var owner = manager._owner;
            if (owner == null || !GiftActivationPolicy.Allow(
                GiftDirector.IsRunner(owner.GetPlayer),
                owner.BotState == EBotState.PreActive,
                owner.HealthController?.IsAlive == true,
                owner.GetPlayer?.HandsController is Player.EmptyHandsController
            ))
            {
                return false;
            }

            Plugin.Trace($"[RR-GIFT] stage=unarmed-activation profile={owner.ProfileId}; native activation readiness check passed for empty hands");
            return true;
        }
    }
}
