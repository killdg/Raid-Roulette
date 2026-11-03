using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommonAssets.Scripts.ArtilleryShelling;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using CommonAssets.Scripts.ArtilleryShelling.Client;

namespace RaidRoulette.TestingClient
{
    internal sealed class RaidBehaviorDirector
    {
        private sealed class Route
        {
            internal BotOwner Owner = null!;
            internal Vector3 Target;
            internal float Expires;
            internal bool Submitting;
            internal bool? Standby;
            internal bool Shelter;
        }

        private static readonly Dictionary<string, Route> routes = new();
        private GameWorld? world;
        private BotOwner? leader;
        private float nextTick;
        private int routeCursor;
        private int mortarWindow;
        private readonly double[] mortarTimes = new double[8];
        private readonly BlueDaySiren siren = new();
        private int preparedZone;
        private float nextMortarPrep;
        private bool ownsServer;
        private bool ownsClient;
        private readonly Queue<Vector3> calledStrikes = new();
        private float nextCalledStrike;
        private readonly HashSet<string> startingMortars = new();
        internal static bool OwnsRoute(BotMover mover)
        {
            if (mover?._owner == null || !routes.TryGetValue(mover._owner.ProfileId, out var route))
            {
                return false;
            }

            var owner = route.Owner;
            return Time.unscaledTime < route.Expires && owner.HealthController?.IsAlive == true && owner.BotState == EBotState
                .Active && mover.CurrentState == EBotMoverState.Default && !owner.DoorOpener.Interacting && !owner.DoorOpener
                .NearDoor && !owner.Medecine.Using && !owner.BewareGrenade.ShallRunAway() && (route.Shelter || owner.Memory?
                .GoalEnemy?.IsVisible != true) && Vector3.Distance(owner.Position, route.Target) > (route.Shelter ? 1f : 3f);
        }

        internal static bool AllowRoute(BotOwner owner, Vector3 target)
        {
            return owner?.Mover == null || !OwnsRoute(owner.Mover) || !routes.TryGetValue(owner.ProfileId, out var route)
                || route.Submitting || Vector3.Distance(target, route.Target) < 3;
        }

        internal static void Drive(BotOwner? owner)
        {
            if (owner?.Mover == null || !OwnsRoute(owner.Mover))
            {
                return;
            }

            owner.MovementResume();
            owner.Mover.TargetPose = 1;
            owner.SetTargetMoveSpeed(1);
            owner.Steering.LookToMovingDirection();
        }

        private void Clear()
        {
            foreach (var route in routes.Values)
            {
                if (route.Owner != null && route.Standby.HasValue && route.Owner.StandBy != null)
                {
                    route.Owner.StandBy.CanDoStandBy = route.Standby.Value;
                }
            }

            routes.Clear();
            BlueDayCover.Clear();
            siren.Clear();
            if (world != null)
            {
                if (ownsServer)
                {
                    world.ServerShellingController?.Dispose();
                    world.ServerShellingController = null;
                }

                if (ownsClient)
                {
                    world.ClientShellingController?.Dispose();
                    world.ClientShellingController = null;
                }
            }

            ownsServer = ownsClient = false;
            leader = null;
            calledStrikes.Clear();
            startingMortars.Clear();
            mortarWindow = preparedZone = routeCursor = 0;
            nextTick = nextMortarPrep = 0;
        }

        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(current, world))
            {
                Clear();
                world = current;
                int[] minimumMinutes =
                {
                    3,
                    8,
                    13,
                    18,
                    24,
                    31,
                    38,
                    44
                };
                int[] maximumMinutes =
                {
                    5,
                    10,
                    17,
                    21,
                    28,
                    33,
                    40,
                    48
                };
                for (int index = 0; index < mortarTimes.Length; index++)
                {
                    mortarTimes[index] = UnityEngine.Random.Range(minimumMinutes[index] * 60f, maximumMinutes[index] * 60f);
                }
            }

            if (world == null || Plugin.Local?.HealthController?.IsAlive != true || Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + .5f;
            // Shelter is temporary. Restore the native standby permission when
            // its lease ends, as well as during the normal raid teardown.
            foreach (var entry in routes.Where(entry => entry.Value.Shelter && Time.unscaledTime >= entry.Value.Expires).ToArray())
            {
                Route expired = entry.Value;
                if (expired.Standby.HasValue && expired.Owner?.StandBy != null)
                {
                    expired.Owner.StandBy.CanDoStandBy = expired.Standby.Value;
                }

                routes.Remove(entry.Key);
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (game?.GameTimer?.StartDateTime == null || game.Status != GameStatus.Started)
            {
                return;
            }

            string context = Plugin.RaidContext;
            try
            {
                if (context.EndsWith(": blue_day", StringComparison.Ordinal))
                {
                    siren.Tick(game.GameTimer.PastTime.TotalSeconds);
                    Mortars(game.GameTimer.PastTime.TotalSeconds);
                    BlueDayCover.Tick(world);
                    return;
                }

                bool hunt = context.EndsWith(": no_safe_space", StringComparison.Ordinal);
                bool concentrate = context.EndsWith(": scav_concentrate", StringComparison.Ordinal);
                bool gunGamePressure = GunGamePressure.Applies(context) && GunGameDirector.Ready;
                if (!hunt && !concentrate && !gunGamePressure)
                {
                    return;
                }

                var bots = world.RegisteredPlayers.OfType<Player>().Where(player => player.IsAI && player.HealthController?
                    .IsAlive == true && player.Profile.Info.Settings.Role != WildSpawnType.shooterBTR).Select(player => player.AIData?
                    .BotOwner).Where(bot => bot != null && bot.BotState == EBotState.Active && bot.Mover != null).Cast<BotOwner>()
                        .ToArray();
                if (bots.Length == 0)
                {
                    return;
                }

                if (concentrate && (leader == null || leader.HealthController?.IsAlive != true))
                {
                    leader = bots.OrderBy(bot => bot.ProfileId, StringComparer.Ordinal).First();
                    Plugin.Trace("[RR-CONCENTRATE] leader=" + leader.ProfileId);
                }

                foreach (var bot in bots)
                {
                    if (hunt)
                    {
                        bot.BotsGroup.AddEnemy(Plugin.Local, EBotEnemyCause.byKill);
                        if (bot.BotsGroup.Enemies.TryGetValue(Plugin.Local, out var groupEnemy))
                        {
                            groupEnemy.EnemyLastPosition = Plugin.Local.Position;
                        }

                        if (bot.EnemiesController.EnemyInfos.TryGetValue(Plugin.Local, out var enemy))
                        {
                            enemy.IgnoreUntilAggression = false;
                            bot.Memory.GoalEnemy = enemy;
                        }
                    }

                    Drive(bot);
                }

                // At most one fresh native path per half-second; avoid pathfinding
                // for every map actor in a single frame.
                var selected = bots[routeCursor++ % bots.Length];
                if (selected.Medecine.Using || selected.BewareGrenade.ShallRunAway() || selected.Memory?.GoalEnemy?
                    .IsVisible == true || selected.Mover.CurrentState != EBotMoverState.Default)
                {
                    return;
                }

                Vector3 target;
                if (gunGamePressure)
                {
                    if (!GunGamePressure.TryTarget(selected, Plugin.Local.Position, out target))
                    {
                        return;
                    }
                }
                else if (hunt)
                {
                    target = Plugin.Local.Position;
                }
                else if (ReferenceEquals(selected, leader))
                {
                    var anchors = LocalRaidDirector.Zones.SelectMany(zone => zone.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                        .ISpawnPoint>()).Where(point => Vector3.Distance(point.Position, selected.Position) > 25 && Vector3.Distance(point
                        .Position, selected.Position) < 160).ToArray();
                    if (anchors.Length == 0 || selected.Mover.HasPathAndNoComplete)
                    {
                        return;
                    }

                    target = anchors[UnityEngine.Random.Range(0, anchors.Length)].Position;
                }
                else
                {
                    if (leader == null || Vector3.Distance(selected.Position, leader.Position) < 9 || selected.Memory?.GoalEnemy != null)
                    {
                        return;
                    }

                    float angle = (selected.ProfileId.GetHashCode() & 1023) / 1024f * Mathf.PI * 2;
                    target = leader.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 5;
                }

                if (routes.TryGetValue(selected.ProfileId, out var old) && OwnsRoute(selected.Mover) && Vector3.Distance(old
                    .Target, target) < 5)
                {
                    return;
                }

                if (!NavMesh.SamplePosition(target, out var hit, 3, NavMesh.AllAreas))
                {
                    return;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(selected.Position, hit.position, NavMesh.AllAreas, path) || path
                    .status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                {
                    return;
                }

                if (gunGamePressure && !GauntletSpawnSafety.HasUnlockedExit(path))
                {
                    return;
                }

                if (!routes.TryGetValue(selected.ProfileId, out var route))
                {
                    routes[selected.ProfileId] = route = new Route
                    {
                        Owner = selected,
                        Standby = selected.StandBy.CanDoStandBy
                    };
                }

                route.Target = hit.position;
                route.Expires = Time.unscaledTime + 30;
                route.Submitting = true;
                try
                {
                    selected.StandBy.CanDoStandBy = false;
                    selected.StandBy.StandByType = BotStandByType.active;
                    selected.Mover.GoToByWay(path.corners, .8f);
                    selected.Exfiltration?.ResetLeaveTime();
                    Drive(selected);
                }
                finally
                {
                    route.Submitting = false;
                }
            }
            catch (Exception error)
            {
                Plugin.Trace("[RR-BEHAVIOR] unavailable=" + error.GetBaseException().Message);
                nextTick = Time.unscaledTime + 5;
            }
        }

        internal static bool SeekShelter(BotOwner owner, Vector3 destination)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(owner.Position, destination, NavMesh.AllAreas, path) || !GauntletSpawnSafety.HasUnlockedExit(path))
            {
                return false;
            }

            if (!routes.TryGetValue(owner.ProfileId, out var route))
            {
                route = new Route
                {
                    Owner = owner,
                    Standby = owner.StandBy.CanDoStandBy
                };
                routes[owner.ProfileId] = route;
            }

            route.Target = destination;
            route.Expires = Time.unscaledTime + 35f;
            route.Shelter = true;
            route.Submitting = true;
            try
            {
                owner.StandBy.CanDoStandBy = false;
                owner.StandBy.StandByType = BotStandByType.active;
                owner.Mover.GoToByWay(path.corners, .8f);
                Drive(owner);
            }
            finally
            {
                route.Submitting = false;
            }

            return true;
        }

        private void Mortars(double elapsed)
        {
            var settings = Singleton<GlobalConfiguration>.Instance.ArtilleryShelling;
            if (!settings.ArtilleryMapsConfigs.TryGetValue(world!.LocationId, out var map) || map.ShellingZones == null || !map
                .ShellingZones.Any(zone => zone.UsedInPlanedShelling || zone.UseInCalledShelling))
            {
                return;
            }

            if (world.ClientShellingController == null)
            {
                world.ClientShellingController = new ArtilleryShellingControllerClient(true);
                ownsClient = true;
            }

            if (world.ServerShellingController == null)
            {
                world.ServerShellingController = new ArtilleryShellingControllerServer();
                ownsServer = true;
            }

            var controller = world.ServerShellingController;
            if (controller.CurrentMapConfiguration.ShellingZones == null)
            {
                return;
            }

            if (preparedZone < controller._currentMapConfiguration.ShellingZones.Length && Time.unscaledTime >= nextMortarPrep)
            {
                nextMortarPrep = Time.unscaledTime + 1;
                var zone = controller._currentMapConfiguration.ShellingZones[preparedZone];
                if (zone.UsedInPlanedShelling)
                {
                    controller.FindShellingPointsByMapGeometryForZone(ref zone);
                }

                controller._currentMapConfiguration.ShellingZones[preparedZone++] = zone;
                return;
            }

            if (calledStrikes.Count > 0 && Time.unscaledTime >= nextCalledStrike)
            {
                nextCalledStrike = Time.unscaledTime + 1;
                controller.StartShellingPosition(calledStrikes.Dequeue());
            }

            if (mortarWindow >= mortarTimes.Length || elapsed < mortarTimes[mortarWindow])
            {
                return;
            }

            // Consume missed windows without a catch-up burst after a clock jump.
            do
            {
                mortarWindow++;
            }
            while (mortarWindow < mortarTimes.Length && elapsed >= mortarTimes[mortarWindow]);
            int started = 0;
            foreach (var zone in controller.CurrentMapConfiguration.ShellingZones.Where(zone => zone.UsedInPlanedShelling))
            {
                if (startingMortars.Contains(zone.ID) || controller._zonesInEventShelling.Keys.Any(active => active.ID == zone.ID))
                {
                    continue;
                }

                // Direct native per-zone scheduling permits concurrent bombardments,
                // retaining native projectile damage, warnings and geometry checks.
                StartMortar(controller, zone);
                started++;
            }

            if (started == 0 && controller.CurrentMapConfiguration.ShellingZones.Any(zone => zone.UseInCalledShelling))
            {
                var points = LocalRaidDirector.NativePoints;
                var selected = new List<Vector3>();
                int first = points.Length > 0 ? UnityEngine.Random.Range(0, points.Length) : 0;
                for (int index = 0; index < Math.Min(96, points.Length); index++)
                {
                    var anchor = points[(first + index) % points.Length].Position;
                    if (Physics.Raycast(anchor + Vector3.up * 2, Vector3.up, 200, Physics.DefaultRaycastLayers, QueryTriggerInteraction
                        .Ignore))
                    {
                        continue;
                    }

                    if (selected.Any(previous => Vector3.Distance(previous, anchor) < 75))
                    {
                        continue;
                    }

                    selected.Add(anchor);
                    calledStrikes.Enqueue(anchor);
                    if (selected.Count >= 3)
                    {
                        break;
                    }
                }

                started = selected.Count;
            }

            Plugin.Trace($"[RR-BLUE-DAY] window={mortarWindow} raidMinutes={elapsed / 60:0.0} zonesStarted={started} nativeDamage=true");
        }

        private async void StartMortar(ArtilleryShellingControllerServer controller, ArtilleryShellingZone zone)
        {
            if (!startingMortars.Add(zone.ID))
            {
                return;
            }

            try
            {
                await controller.StartEventShelling(zone, 10);
            }
            catch (Exception error)
            {
                Plugin.Trace("[RR-BLUE-DAY] native-zone-failed=" + zone.ID + " error=" + error.GetBaseException().Message);
            }
            finally
            {
                startingMortars.Remove(zone.ID);
            }
        }
    }
}
