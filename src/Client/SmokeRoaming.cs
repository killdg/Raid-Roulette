using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class SmokeRoaming
    {
        private sealed class Route
        {
            internal BotOwner? Owner;
            internal bool? Standby;
            internal Vector3? Target;
            internal Vector3 Last;
            internal float Next;
            internal float Progress;
            internal bool Submitting;
            internal readonly Queue<Vector3> Recent = new();
        }

        private static readonly Dictionary<string, Route> routes = new();
        private static int budget;
        private static void Restore(Route route)
        {
            try
            {
                if (route.Standby.HasValue && route.Owner?.StandBy != null)
                {
                    route.Owner.StandBy.CanDoStandBy = route.Standby.Value;
                }
            }
            catch (MissingReferenceException)
            {
            }

            route.Standby = null;
        }

        private static void Wake(BotOwner bot, Route route)
        {
            route.Standby ??= bot.StandBy.CanDoStandBy;
            bot.StandBy.CanDoStandBy = false;
            bot.StandBy.StandByType = BotStandByType.active;
        }

        internal static void Clear()
        {
            foreach (var route in routes.Values)
            {
                Restore(route);
            }

            routes.Clear();
        }

        internal static void BeginTick() => budget = 2;
        private static bool Idle(BotOwner? bot) => SmokersDirector.Active && bot != null && bot.BotState == EBotState
            .Active && bot.HealthController?.IsAlive == true && bot.Memory?.GoalEnemy == null && bot.Mover?
            .CurrentState == EBotMoverState.Default && bot.WeaponManager?.Grenades?.ThrowindNow != true && bot.GetPlayer
            .HandsController is not Player.GrenadeHandsController && bot.WeaponManager?.Reload?.Reloading != true && bot
            .Medecine?.Using != true && bot.WeaponManager?.Selector?.IsChanging != true;
        internal static bool OwnsRoute(BotMover mover) => Idle(mover._owner) && routes.TryGetValue(mover._owner.ProfileId,
             out var route) && route.Target.HasValue && Time.unscaledTime - route.Progress < 15 && Vector3.Distance(mover
            ._owner.Position, route.Target.Value) > 3;
        internal static bool AllowRoute(BotOwner owner, Vector3 target) => owner?.Mover == null || !OwnsRoute(owner.Mover)
            || !routes.TryGetValue(owner.ProfileId, out var route) || route.Submitting || Vector3.Distance(target, route.Target!.Value) < 3;
        internal static void Tick(BotOwner bot)
        {
            if (!Idle(bot))
            {
                if (routes.TryGetValue(bot.ProfileId, out var old))
                {
                    Restore(old);
                    old.Target = null;
                    old.Next = Time.unscaledTime + 5;
                }

                return;
            }

            if (!routes.TryGetValue(bot.ProfileId, out var route))
            {
                routes[bot.ProfileId] = route = new()
                {
                    Owner = bot,
                    Last = bot.Position,
                    Progress = Time.unscaledTime,
                    Next = Time.unscaledTime + UnityEngine.Random.Range(3f, 15f)
                };
            }

            if (Vector3.Distance(bot.Position, route.Last) > 1)
            {
                route.Last = bot.Position;
                route.Progress = Time.unscaledTime;
            }

            if (route.Target.HasValue && OwnsRoute(bot.Mover))
            {
                Wake(bot, route);
                bot.MovementResume();
                bot.Mover.TargetPose = 1;
                bot.SetTargetMoveSpeed(1);
            }

            if (Time.unscaledTime < route.Next || budget <= 0)
            {
                return;
            }

            route.Next = Time.unscaledTime + 5;
            if (route.Target.HasValue && Vector3.Distance(bot.Position, route.Target.Value) > 3 && Time.unscaledTime - route
                .Progress < 15 && bot.Mover.HasPathAndNoComplete)
            {
                return;
            }

            if (route.Target.HasValue)
            {
                route.Recent.Enqueue(route.Target.Value);
                while (route.Recent.Count > 6)
                {
                    route.Recent.Dequeue();
                }
            }

            route.Target = null;
            Restore(route);
            budget--;
            var anchors = LocalRaidDirector.Zones.SelectMany(z => z.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                .ISpawnPoint>()).Select(p => p.Position).Where(p => Vector3.Distance(p, bot.Position) > 25 && Vector3.Distance(p,
                 bot.Position) < 140).OrderByDescending(p => (route.Recent.Count == 0 ? 50 : route.Recent.Min(v => Vector3
                .Distance(p, v))) + UnityEngine.Random.value * 30).Take(3);
            foreach (var anchor in anchors)
            {
                if (!NavMesh.SamplePosition(anchor, out var hit, 3, NavMesh.AllAreas))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(bot.Position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus
                    .PathComplete || path.corners.Length < 2)
                {
                    continue;
                }

                route.Target = hit.position;
                route.Progress = Time.unscaledTime;
                route.Last = bot.Position;
                route.Submitting = true;
                try
                {
                    Wake(bot, route);
                    bot.Mover.GoToByWay(path.corners, 0.5f);
                    bot.MovementResume();
                    bot.Mover.TargetPose = 1;
                    bot.SetTargetMoveSpeed(1);
                }
                finally
                {
                    route.Submitting = false;
                }

                Plugin.Trace($"[RR-SMOKE] stage=roam profile={bot.ProfileId} distance={Vector3.Distance(bot.Position, hit.position):0} target={hit.position}");
                return;
            }

            route.Next = Time.unscaledTime + 15;
        }
    }
}
