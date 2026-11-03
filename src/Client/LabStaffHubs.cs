using System;
using System.Collections.Generic;
using System.Linq;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    // Resolve native keyed-door landmarks once from the existing Lab Work Day door scan.
    // Only navigation positions beside those landmarks are retained; no scene objects are cloned.
    internal static class LabStaffHubs
    {
        private static readonly HashSet<string> OfficeKeys = new(StringComparer.Ordinal)
        {
            "5c1d0dc586f7744baf2e7b79", // Green laboratory keycard.
            "5c1d0efb86f7744baf2e7b7b", // Red laboratory keycard.
            "5c1d0f4986f7744bb01837fa", // Black laboratory keycard.
            "5c1e495a86f7743109743dfb", // Violet laboratory keycard.
            "5c1e2a1e86f77431ea0ea84c" // Manager's glass office key.
        };
        private static readonly List<Vector3> Hubs = new();
        private static readonly List<Vector3> Placements = new();
        internal static bool SurveyComplete
        {
            get; set;
        }

        internal static void Reset()
        {
            SurveyComplete = false;
            Hubs.Clear();
            Placements.Clear();
        }

        internal static void Capture(Door door)
        {
            if (!OfficeKeys.Contains(door.KeyId))
            {
                return;
            }

            var origin = door.transform.position;
            // Search both sides of the doorway for an actual navigation floor. Never
            // synthesize walkability inside an office that has no native AI mesh.
            foreach (float side in new[]
            {
                -1f,
                1f
            }

            )
            {
                var approach = origin + door.transform.forward * side * 3f;
                if (NavMesh.SamplePosition(approach, out var floor, 2f, NavMesh.AllAreas) && Mathf.Abs(floor.position.y - origin.y) < 2f)
                {
                    if (Hubs.Exists(existing => Vector3.Distance(existing, floor.position) < 8f))
                    {
                        return;
                    }

                    Hubs.Add(floor.position);
                    Plugin.Trace($"[RR-LAB-HUB] landmark={door.KeyId} floor={floor.position}");
                    return;
                }
            }
        }

        internal static bool TryPatrol(
            Vector3 origin,
            ref int cursor,
            out Vector3 destination,
            out NavMeshPath path)
        {
            destination = default;
            path = new NavMeshPath();
            if (Hubs.Count < 2)
            {
                return false;
            }

            // These are cached office landmarks, not a scene search. Rotate through
            // at most three endpoints per decision and require a complete native
            // route. Different floor heights are expected at the Labs stairways.
            // Independent leg choices prevent workers from sharing a fixed
            // landmark order. Native pathfinding still controls the real corridor.
            cursor = (cursor + UnityEngine.Random.Range(1, Hubs.Count)) % Hubs.Count;
            float lowestFloor = Hubs.Min(point => point.y) - 1.5f;
            float highestFloor = Hubs.Max(point => point.y) + 1.5f;
            for (int probe = 0; probe < Math.Min(3, Hubs.Count); probe++)
            {
                Vector3 hub = Hubs[Math.Abs(cursor++ % Hubs.Count)];
                if (!LabStaffPolicy.PatrolDistance(Vector3.Distance(origin, hub)))
                {
                    continue;
                }

                Vector2 offset = UnityEngine.Random.insideUnitCircle * 4f;
                Vector3 candidate = hub + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var floor, 1.5f, NavMesh.AllAreas)
                    || Mathf.Abs(floor.position.y - hub.y) > 1f
                    || !NavMesh.CalculatePath(origin, floor.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete
                    || path.corners.Length < 2)
                {
                    continue;
                }

                // Allow stairs between the office floors while excluding routes
                // that leave their vertical range for the basement/sewer level.
                if (path.corners.Any(point => point.y < lowestFloor || point.y > highestFloor)
                    || !GauntletSpawnSafety.HasUnlockedExit(path))
                {
                    continue;
                }

                destination = floor.position;
                return true;
            }

            return false;
        }

        internal static bool TryAssign(int index, out Vector3 position)
        {
            position = default;
            if (Hubs.Count == 0)
            {
                return false;
            }

            var hub = Hubs[Math.Abs(index) % Hubs.Count];
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * 6f;
                var candidate = hub + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var floor, 1.5f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - hub
                    .y) > 1f || Placements.Exists(other => Vector3.Distance(other, floor.position) < 1.5f))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(hub, floor.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                position = floor.position;
                Placements.Add(position);
                return true;
            }

            return false;
        }
    }
}
