using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using RaidRoulette.Shared;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    /// <summary>
    /// A doorway-crossing effect shared by Wrong Door and Schizophrenia. This moves
    /// only the living local player through native Player.Teleport; no inventory,
    /// quest, bot, extraction or door-state changes are made.
    /// </summary>
    internal sealed class DoorTeleportDirector
    {
        private sealed class Portal
        {
            internal Door Door = null!;
            internal Vector3 Center;
            internal Vector3 Normal;
            internal Vector3 Along;
            internal bool Latched;
        }

        internal static DoorTeleportDirector? Current;
        private readonly List<Portal> portals = new();
        private readonly Collider[] clearance = new Collider[64];
        private IEnumerator<WorldInteractiveObject>? discovery;
        private GameWorld? world;
        private Vector3 previous;
        private bool hasPrevious;
        private bool discovered;
        private float nextCheck;
        private float cooldownUntil;
        internal static bool Active => Plugin.RaidContext.EndsWith(": wrong_door", StringComparison.Ordinal) || Plugin
            .RaidContext.EndsWith(": schizophrenia", StringComparison.Ordinal);

        internal void Stop()
        {
            discovery?.Dispose();
            discovery = null;
            portals.Clear();
            world = null;
            discovered = false;
            hasPrevious = false;
            cooldownUntil = 0f;
            Current = null;
        }

        internal void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld || !Active)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                Stop();
                world = current;
                nextCheck = 0f;
            }

            var player = Plugin.Local;
            if (world == null || player?.HealthController?.IsAlive != true)
            {
                return;
            }

            Current = this;
            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (game?.Status != GameStatus.Started)
            {
                return;
            }

            Discover();
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.1f;
            var position = player.Position;
            if (!hasPrevious)
            {
                previous = position;
                hasPrevious = true;
                return;
            }

            foreach (var portal in portals)
            {
                if (portal.Door == null)
                {
                    continue;
                }

                if ((position - portal.Center).sqrMagnitude > 16f)
                {
                    portal.Latched = false;
                    continue;
                }

                if (portal.Latched || Time.unscaledTime < cooldownUntil || !Usable(portal.Door))
                {
                    continue;
                }

                float before = Vector3.Dot(previous - portal.Center, portal.Normal);
                float after = Vector3.Dot(position - portal.Center, portal.Normal);
                float denominator = before - after;
                if (Mathf.Abs(denominator) < 0.0001f)
                {
                    continue;
                }

                var crossing = Vector3.Lerp(previous, position, Mathf.Clamp01(before / denominator));
                if (!DoorTeleportPolicy.Crossed(
                    before,
                    after,
                    Vector3.Dot(crossing - portal.Center, portal.Along),
                    crossing.y - portal.Center.y,
                    (position - previous).sqrMagnitude
                ))
                {
                    continue;
                }

                portal.Latched = true;
                bool won = DoorTeleportPolicy.Roll(UnityEngine.Random.Range(0, 100));
                Plugin.Trace($"[RR-DOOR-PORTAL] crossing={portal.Door.Id} rollSucceeded={won}");
                if (won && Travel(player, portal))
                {
                    return;
                }
            }

            previous = position;
        }

        private static bool Usable(Door door)
        {
            // Keyed rooms, extraction mechanisms and vehicle/container subclasses
            // are excluded from both ends. Only native ordinary open doors qualify.
            return door.GetType() == typeof(Door) && !door.NoInteractionsAllowed && !door.IsBroken && string.IsNullOrEmpty(door
                .KeyId) && door.DoorState == EDoorState.Open;
        }

        private void Discover()
        {
            if (discovered)
            {
                return;
            }

            // LocationScene supplies authored interactive objects in offline SPT;
            // GameWorld.WorldInteractiveObjects has no network World in solo raids.
            // Bound discovery to 32 objects per frame, once per raid, without scene scans.
            discovery ??= LocationScene.GetAllObjects<WorldInteractiveObject>(true).GetEnumerator();
            for (int budget = 0; budget < 32; budget++)
            {
                if (!discovery.MoveNext())
                {
                    discovery.Dispose();
                    discovery = null;
                    discovered = true;
                    Plugin.Trace($"[RR-DOOR-PORTAL] cached ordinary doorways={portals.Count}");
                    return;
                }

                if (discovery.Current is not Door door || door.GetType() != typeof(Door) || door.NoInteractionsAllowed || !string
                    .IsNullOrEmpty(door.KeyId))
                {
                    continue;
                }

                var leaf = door.CollisionColliders?.FirstOrDefault(c => c != null && !c.isTrigger);
                if (leaf == null)
                {
                    continue;
                }

                var hinge = door.transform.position;
                var axis = WorldInteractiveObject.GetRotationAxis(door.DoorAxis, door.transform.parent);
                var delta = Quaternion.AngleAxis(door.GetAngle(EDoorState.Shut) - door.CurrentAngle, axis);
                // Freeze the closed-leaf plane, not the moving open-door mesh. The
                // collider center rotated back around the hinge locates the doorway.
                var along = delta * WorldInteractiveObject.GetRotationAxis(door.DoorForward, door.transform);
                along.y = 0f;
                if (along.sqrMagnitude < 0.1f)
                {
                    continue;
                }

                along.Normalize();
                portals.Add(new Portal { Door = door, Center = hinge + delta * (leaf.bounds.center - hinge), Along = along,
                     Normal = Vector3.Cross(along, Vector3.up).normalized });
            }
        }

        private bool Travel(Player player, Portal source)
        {
            if (portals.Count < 2)
            {
                return false;
            }

            int start = UnityEngine.Random.Range(0, portals.Count);
            Vector3 incoming = player.Position - previous;
            incoming.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f)
            {
                incoming = player.LookDirection;
                incoming.y = 0f;
            }

            incoming.Normalize();
            Vector3 sourceExit = source.Normal * (Vector3.Dot(incoming, source.Normal) >= 0f ? 1f : -1f);
            var budget = System.Diagnostics.Stopwatch.StartNew();
            // At most 24 candidate doorways per trigger. No recurring scene search or
            // allocation of a full-map physics result set is performed.
            for (int index = 0; index < Mathf.Min(24, portals.Count) && budget.Elapsed.TotalMilliseconds < 4; index++)
            {
                var target = portals[(start + index) % portals.Count];
                if (ReferenceEquals(target, source) || target.Door == null || !Usable(target.Door) || Vector3.Distance(source
                    .Center, target.Center) < 15f)
                {
                    continue;
                }

                foreach (float side in new[]
                {
                    1f,
                    -1f
                }

                )
                {
                    Vector3 exitNormal = target.Normal * side;
                    float yawChange = Vector3.SignedAngle(sourceExit, exitNormal, Vector3.up);
                    Vector3 outgoing = Quaternion.AngleAxis(yawChange, Vector3.up) * incoming;
                    var candidate = target.Center + target.Normal * side * 1.5f;
                    candidate.y = target.Center.y - 1f;
                    if (!NavMesh.SamplePosition(candidate, out var floor, 1.5f, NavMesh.AllAreas) || Mathf.Abs(floor.position
                        .y - candidate.y) > 1f || !Clear(player, floor.position) || !Physics.Raycast(
                        floor.position + Vector3.up * 0.5f,
                        Vector3.down,
                        out var physicalFloor,
                        1f,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore
                    ) || physicalFloor.normal.y < 0.6f || Vector3.Dot(floor.position - target.Center, exitNormal) < 0.75f
                        || !ClearRunOut(player, floor.position, outgoing) || !HasExit(floor.position))
                    {
                        continue;
                    }

                    cooldownUntil = Time.unscaledTime + DoorTeleportPolicy.CooldownSeconds;
                    target.Latched = true;
                    player.Teleport(floor.position + Vector3.up * 0.05f);
                    // Player.Rotation is native movement-context yaw/pitch. Rotate yaw
                    // by the doorway frame change, preserving pitch and the relative
                    // angle of entry (including strafing/backward input). Teleport
                    // alone changes position but retains the old world-space facing.
                    var rotation = player.Rotation;
                    player.Rotation = new Vector2(rotation.x + yawChange, rotation.y);
                    previous = player.Position;
                    hasPrevious = true;
                    Plugin.Trace($"[RR-DOOR-PORTAL] source={source.Door.Id} target={target.Door.Id} landing={floor.position} yawChange={yawChange:0}; cooldown=30s");
                    return true;
                }
            }

            Plugin.Trace("[RR-DOOR-PORTAL] roll succeeded but no safe open destination was found; player left in place");
            return false;
        }

        private bool ClearRunOut(Player player, Vector3 floor, Vector3 direction)
        {
            // A safe standing capsule does not guarantee space to keep running.
            // Reject a destination with an immediate wall or navigation edge ahead;
            // this is a bounded local check, not another scene search.
            Vector3 ahead = floor + direction * 2f;
            return !NavMesh.Raycast(floor, ahead, out _, NavMesh.AllAreas) && Clear(player, floor + direction) && Clear(player, ahead);
        }

        private bool Clear(Player player, Vector3 floor)
        {
            int count = Physics.OverlapCapsuleNonAlloc(
                floor + Vector3.up * 0.4f,
                floor + Vector3.up * 1.5f,
                0.3f,
                clearance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            if (count == clearance.Length)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                var collider = clearance[index];
                if (collider != null && collider.GetComponentInParent<Player>() != player)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasExit(Vector3 floor)
        {
            // Complete navigation alone can cross locked doors. Reuse the checked
            // physical-door route validator, anchored to authored native AI spawns.
            var anchors = LocalRaidDirector.Zones.SelectMany(zone => zone.SpawnPoints ?? Array.Empty<EFT.Game.Spawning
                .ISpawnPoint>()).OrderBy(point => (point.Position - floor).sqrMagnitude).Take(3);
            foreach (var anchor in anchors)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(floor, anchor.Position, NavMesh.AllAreas, path) && GauntletSpawnSafety.HasUnlockedExit(path))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool Test()
        {
            var player = Plugin.Local;
            if (!Active || player == null || player.HealthController?.IsAlive != true)
            {
                return false;
            }

            var source = portals.Where(portal => portal.Door != null && Usable(portal.Door)).OrderBy(portal => (portal
                .Center - player.Position).sqrMagnitude).FirstOrDefault();
            return source != null && Vector3.Distance(source.Center, player.Position) < 4f && Travel(player, source);
        }
    }
}
