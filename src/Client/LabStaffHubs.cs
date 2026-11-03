using System;
using System.Collections.Generic;
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
            "5c1e2a1e86f77431ea0ea84c" // Manager's glass office key.
        };
        private static readonly List<Vector3> Hubs = new();
        private static readonly List<Vector3> Placements = new();
        internal static bool SurveyComplete { get; set; }

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
                    Hubs.Add(floor.position);
                    Plugin.Trace($"[RR-LAB-HUB] landmark={door.KeyId} floor={floor.position}");
                    return;
                }
            }
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
