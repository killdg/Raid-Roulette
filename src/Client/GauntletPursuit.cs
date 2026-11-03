using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class GauntletPursuit
    {
        private sealed class Route
        {
            internal BotOwner Owner = null!;
            internal Vector3? Destination;
            internal float ReplanAt;
            internal bool Submitting;
            internal float LogAt;
            internal string Diagnostic = "not planned";
            internal Vector3 RequestedPlayerPosition;
            internal Vector3 LastPosition;
            internal float LastMoved;
            internal float RecoveryAt;
            internal int RecoveryProbe;
            internal readonly List<Vector3> FailedRecoveryPositions = new();
            internal Vector3? RecoveryPosition;
            internal Vector3 ProgressWindowStart;
            internal float ProgressWindowAt;
            internal bool Oscillating;
        }

        private static readonly Dictionary<string, Route> routes = new();
        private static float nextTick;
        private static float nextError;
        private static int cursor;
        internal static void Add(BotOwner owner)
        {
            routes[owner.ProfileId] = new Route
            {
                Owner = owner,
                LastPosition = owner.Position,
                LastMoved = Time.unscaledTime,
                ProgressWindowStart = owner.Position,
                ProgressWindowAt = Time.unscaledTime
            };
        }

        internal static void Clear()
        {
            routes.Clear();
            nextTick = 0;
            cursor = 0;
        }

        internal static bool IsHunter(BotOwner? owner)
        {
            return GauntletDirector.Active && owner != null && owner.HealthController?.IsAlive == true && routes
                .ContainsKey(owner.ProfileId);
        }

        internal static bool IsGroup(BotsGroup group)
        {
            return routes.Values.Any(route => IsHunter(route.Owner) && ReferenceEquals(route.Owner.BotsGroup, group));
        }

        internal static bool OwnsRoute(BotMover mover)
        {
            return IsHunter(mover._owner) && routes.TryGetValue(mover._owner.ProfileId, out var route) && route.Destination
                .HasValue && mover.CurrentState == EBotMoverState.Default && Vector3.Distance(mover._owner.Position, route
                .Destination.Value) > 3 && !mover._owner.Medecine.Using && !mover._owner.BewareGrenade.ShallRunAway();
        }

        internal static bool AllowRoute(BotOwner owner, Vector3 target)
        {
            return owner?.Mover == null || !OwnsRoute(owner.Mover) || !routes.TryGetValue(owner.ProfileId, out var route)
                || route.Submitting || Vector3.Distance(target, route.Destination!.Value) < 3;
        }

        internal static void Drive(BotOwner owner)
        {
            if (owner.Mover == null || !OwnsRoute(owner.Mover))
            {
                return;
            }

            owner.MovementResume();
            // Use BotLay's native setter so its rotation callbacks and prone
            // movement state are cleaned up together, rather than changing only
            // the pose slider while the sniper brain continues crawling.
            if (owner.BotLay.IsLay)
            {
                owner.BotLay.IsLay = false;
            }

            owner.BotLay.DelayPosibleLayFor(1f);
            owner.Mover.TargetPose = 1;
            owner.SetTargetMoveSpeed(1);
            bool visible = owner.EnemiesController.EnemyInfos.TryGetValue(Plugin.Local!, out var enemy) && enemy.IsVisible;
            if (!visible)
            {
                owner.Steering.LookToMovingDirection();
            }

            // Native sprint prevents normal weapon fire. Keep full-speed upright
            // pursuit during contact, leaving aim/fire to the native combat code.
            owner.Sprint(!visible, false);
        }

        internal static void Tick()
        {
            if (!GauntletDirector.Active || Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 0.25f;
            var active = routes.Values.Where(route => IsHunter(route.Owner)).ToArray();
            if (active.Length == 0 || Plugin.Local == null)
            {
                return;
            }

            // At most one bounded route-planner invocation per tick, independent
            // of the number of bosses. No full-scene searches or mass path bursts.
            var selected = active[cursor++ % active.Length];
            foreach (var route in active)
            {
                var owner = route.Owner;
                if (owner.BotState != EBotState.Active || owner.BotsGroup == null || owner.Mover == null)
                {
                    if (Time.unscaledTime >= route.LogAt)
                    {
                        route.LogAt = Time.unscaledTime + 10;
                        Plugin.Trace($"[RR-GAUNTLET] waiting for activation profile={owner.ProfileId} "
                            + $"state={owner.BotState} group={owner.BotsGroup != null} mover={owner.Mover != null}");
                    }

                    continue;
                }

                try
                {
                    var local = Plugin.Local;
                    owner.BotsGroup.AddEnemy(local, EBotEnemyCause.byKill);
                    if (owner.BotsGroup.Enemies.TryGetValue(local, out var groupEnemy))
                    {
                        groupEnemy.EnemyLastPosition = local.Position;
                    }

                    owner.EnemiesController.EnemyInfos.TryGetValue(local, out var enemy);
                    if (enemy != null)
                    {
                        enemy.IgnoreUntilAggression = false;
                        // Lighthouse followers gate CanShoot separately from the
                        // ordinary enemy list: attack-zone permission or a prior
                        // attack is required. Trials is hostile from wave start.
                        // Native hostility changes no health and leaves _canShoot,
                        // visibility and ballistics checks intact. Only this
                        // director's registered hunters reach this code.
                        if (enemy is ZryachiyEnemyInfo lighthouseEnemy && !lighthouseEnemy._getHitFromThis)
                        {
                            lighthouseEnemy.SetGetHitFrom();
                        }

                        owner.Memory.GoalEnemy = enemy;
                    }

                    owner.Exfiltration?.ResetLeaveTime();
                    owner.StandBy.CanDoStandBy = false;
                    owner.StandBy.StandByType = BotStandByType.active;
                    // Running animation and repeated one-metre reversals are not
                    // useful travel. Measure net displacement over five seconds
                    // as well as the ordinary stationary timer (no scene scans).
                    if (Time.unscaledTime - route.ProgressWindowAt >= 5f)
                    {
                        route.Oscillating = Vector3.Distance(owner.Position, route.ProgressWindowStart) < 3f;
                        route.ProgressWindowStart = owner.Position;
                        route.ProgressWindowAt = Time.unscaledTime;
                    }

                    if (Vector3.Distance(owner.Position, route.LastPosition) > 1f)
                    {
                        route.LastPosition = owner.Position;
                        route.LastMoved = Time.unscaledTime;
                    }

                    if (owner.Medecine.Using || owner.BewareGrenade.ShallRunAway())
                    {
                        route.LastMoved = Time.unscaledTime;
                        route.Oscillating = false;
                        route.ProgressWindowStart = owner.Position;
                        route.ProgressWindowAt = Time.unscaledTime;
                    }
                    else if (ReferenceEquals(route, selected) && (Time.unscaledTime - route.LastMoved >= 5f || route.Oscillating)
                        && Time.unscaledTime >= route.RecoveryAt)
                    {
                        route.RecoveryAt = Time.unscaledTime + 5f;
                        if (route.RecoveryPosition.HasValue)
                        {
                            route.FailedRecoveryPositions.Add(route.RecoveryPosition.Value);
                            route.RecoveryPosition = null;
                        }

                        TryRecover(route, local.Position);
                    }

                    if (owner.Medecine.Using || owner.BewareGrenade.ShallRunAway() || owner.Mover.CurrentState != EBotMoverState.Default)
                    {
                        route.Destination = null;
                        continue;
                    }

                    if (Vector3.Distance(owner.Position, route.LastPosition) > 1f)
                    {
                        route.LastPosition = owner.Position;
                        route.LastMoved = Time.unscaledTime;
                    }

                    bool needsRoute = !route.Destination.HasValue || Vector3.Distance(route.RequestedPlayerPosition, local
                        .Position) > 4f || !owner.Mover.HasPathAndNoComplete || Time.unscaledTime - route.LastMoved > 5f;
                    if (ReferenceEquals(route, selected) && Time.unscaledTime >= route.ReplanAt && needsRoute)
                    {
                        route.ReplanAt = Time.unscaledTime + 2;
                        route.RequestedPlayerPosition = local.Position;
                        route.Destination = null;
                        if (BossHuntRoutePlanner.TryPlan(owner.Position, local.Position, out var corners, out route.Diagnostic))
                        {
                            route.Destination = corners[corners.Length - 1];
                            route.Submitting = true;
                            try
                            {
                                owner.Mover.GoToByWay(corners, 0.8f);
                            }
                            finally
                            {
                                route.Submitting = false;
                            }
                        }
                    }

                    Drive(owner);
                    if (Time.unscaledTime >= route.LogAt)
                    {
                        route.LogAt = Time.unscaledTime + 10;
                        Plugin.Trace($"[RR-GAUNTLET] pursuit profile={owner.ProfileId} "
                            + $"role={owner.Profile.Info.Settings.Role} state={owner.BotState} "
                            + $"distance={Vector3.Distance(owner.Position, local.Position):0} "
                            + $"position={owner.Position} target={local.Position} "
                            + $"route={route.Destination.HasValue} moving={owner.Mover.IsMoving} "
                            + $"paused={owner.Mover.Pause} path={owner.Mover.HasPathAndNoComplete} " + $"planner=[{route.Diagnostic}]");
                    }
                }
                catch (Exception error)
                {
                    if (Time.unscaledTime >= nextError)
                    {
                        nextError = Time.unscaledTime + 10;
                        Plugin.Trace("[RR-GAUNTLET] pursuit error=" + error.GetBaseException().Message);
                    }
                }
            }
        }

        private static void TryRecover(Route route, Vector3 playerPosition)
        {
            // Recovery must hide both ends of the relocation. Checking only the
            // destination allowed a stationary hunter to disappear in plain view.
            // Native visibility is an additional veto; it never grants permission.
            bool visible = Plugin.Local != null && route.Owner.EnemiesController.EnemyInfos.TryGetValue(Plugin.Local,
                 out var enemy) && enemy.IsVisible;
            if (visible || !GauntletSpawnSafety.IsHiddenFromPlayer(route.Owner.Position))
            {
                return;
            }

            if (!NavMesh.SamplePosition(playerPosition, out var target, 4f, NavMesh.AllAreas) || Mathf.Abs(target.position
                .y - playerPosition.y) > 3f)
            {
                return;
            }

            // Cached native spawn points only. Limit recovery to three path and
            // clearance checks for one hunter per tick; never search the scene.
            var candidates = LocalRaidDirector.Zones.SelectMany(zone => zone.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                .ISpawnPoint>()).Where(point => !point.SpawnBlocked && Vector3.Distance(point.Position, playerPosition) >= 70f
                && Vector3.Distance(point.Position, playerPosition) <= 90f && !route.FailedRecoveryPositions.Any(failed => Vector3
                .Distance(point.Position, failed) < 8f) && Vector3.Distance(point.Position, route.Owner.Position) > 8f)
                .OrderBy(point => Mathf.Abs(Vector3.Distance(point.Position, playerPosition) - 80f)).ToArray();
            int start = candidates.Length == 0 ? 0 : route.RecoveryProbe % candidates.Length;
            route.RecoveryProbe = start + 3;
            foreach (var point in candidates.Skip(start).Take(3))
            {
                if (!NavMesh.SamplePosition(point.Position, out var floor, 2f, NavMesh.AllAreas) || Vector3.Distance(floor
                    .position, playerPosition) < 70f || Vector3.Distance(floor.position, playerPosition) > 90f)
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(floor.position, target.position, NavMesh.AllAreas, path) || path
                    .status != NavMeshPathStatus.PathComplete || !GauntletSpawnSafety.HasUnlockedExit(path) || !GauntletSpawnSafety
                    .IsHiddenFromPlayer(floor.position) || Physics.CheckCapsule(
                    floor.position + Vector3.up * .4f,
                    floor.position + Vector3.up * 1.5f,
                    .25f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    continue;
                }

                Vector3 previous = route.Owner.Position;
                // Teleport only updates the player's transform and cached mover
                // position. Explicitly discard the old path/state, then submit
                // the already validated route before returning to native AI.
                route.Owner.Mover.Stop();
                route.Owner.Mover.Teleport(floor.position);
                route.Owner.Mover.SetMoverState(EBotMoverState.Default);
                route.Owner.Mover.MovementResume();
                route.Owner.MovementResume();
                route.LastPosition = floor.position;
                route.LastMoved = Time.unscaledTime;
                route.ProgressWindowStart = floor.position;
                route.ProgressWindowAt = Time.unscaledTime;
                route.Oscillating = false;
                route.RecoveryPosition = floor.position;
                route.RequestedPlayerPosition = playerPosition;
                route.Destination = path.corners[path.corners.Length - 1];
                route.ReplanAt = Time.unscaledTime + 2f;
                route.Submitting = true;
                try
                {
                    route.Owner.Mover.GoToByWay(path.corners, .8f);
                }
                finally
                {
                    route.Submitting = false;
                }

                Drive(route.Owner);
                Plugin.Trace($"[RR-GAUNTLET] stationary recovery profile={route.Owner.ProfileId} "
                    + $"from={previous} to={floor.position} playerDistance={Vector3.Distance(floor.position, playerPosition):0}");
                return;
            }
        }
    }
}
