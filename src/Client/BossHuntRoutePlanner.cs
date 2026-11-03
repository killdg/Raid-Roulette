using System;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class BossHuntRoutePlanner
    {
        internal static bool TryPlan(Vector3 hunterPosition, Vector3 playerPosition, out Vector3[] corners, out string diagnostic)
        {
            corners = Array.Empty<Vector3>();
            if (!NavMesh.SamplePosition(hunterPosition, out var source, 2f, NavMesh.AllAreas))
            {
                diagnostic = "hunter-sample-missing";
                return false;
            }

            // Player transforms can be above the baked walking surface. The old
            // two-metre target sample and unsampled source could reject a normal
            // street. Bound vertical displacement to avoid selecting another floor.
            bool sampledTarget = NavMesh.SamplePosition(playerPosition, out var target, 4f, NavMesh.AllAreas);
            var partial = Array.Empty<Vector3>();
            string directResult = sampledTarget ? "target-height-rejected" : "target-sample-missing";
            if (sampledTarget && Math.Abs(target.position.y - playerPosition.y) <= 3f)
            {
                var path = new NavMeshPath();
                bool calculated = NavMesh.CalculatePath(source.position, target.position, NavMesh.AllAreas, path);
                directResult = $"calculated={calculated},status={path.status},corners={path.corners.Length}";
                if (calculated && path.status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
                {
                    corners = path.corners;
                    diagnostic = $"complete sourceOffset={Vector3.Distance(source.position, hunterPosition):0.00} "
                        + $"targetOffset={Vector3.Distance(target.position, playerPosition):0.00}";
                    return true;
                }

                // A partial path is still a native, connected walking route up to
                // its final corner. Never bridge its missing section by direct
                // movement or teleportation. Replan as the hunter reaches that edge.
                if (calculated && path.status == NavMeshPathStatus.PathPartial && HasUsablePartialRoute(source.position, path.corners))
                {
                    partial = path.corners;
                }
            }

            // A player's exact point can lie on an isolated small surface. Try a
            // bounded set of nearby approaches, each with a complete native path.
            // At most five path calculations per replan; no whole-scene scanning.
            var offsets = new[]
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left
            };
            foreach (var offset in offsets)
            {
                if (!NavMesh.SamplePosition(playerPosition + offset * 8f, out var approach, 2f, NavMesh.AllAreas) || Math
                    .Abs(approach.position.y - playerPosition.y) > 3f)
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(source.position, approach.position, NavMesh.AllAreas, path) && path
                    .status == NavMeshPathStatus.PathComplete && path.corners.Length >= 2)
                {
                    corners = path.corners;
                    diagnostic = $"nearby-approach direct=[{directResult}] "
                        + $"remaining={Vector3.Distance(approach.position, playerPosition):0.0}";
                    return true;
                }
            }

            corners = partial;
            diagnostic = partial.Length >= 2 ? $"partial-frontier direct=[{directResult}]" : $"no-connected-route direct=[{directResult}]";
            return corners.Length >= 2;
        }

        internal static bool HasUsablePartialRoute(Vector3 source, Vector3[] corners)
        {
            return corners != null && corners.Length >= 2 && Vector3.Distance(source, corners[corners.Length - 1]) > 3f;
        }
    }
}
