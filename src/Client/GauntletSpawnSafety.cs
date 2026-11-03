using EFT;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal static class GauntletSpawnSafety
    {
        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static readonly RaycastHit[] denseRouteHits = new RaycastHit[128];
        private static readonly RaycastHit[] visibilityHits = new RaycastHit[32];
        internal static bool IsHiddenFromPlayer(Vector3 position)
        {
            Camera? camera = PsychWorldScares.ViewCamera;
            if (camera == null)
            {
                return false;
            }

            // Require solid cover at feet, torso and head height, even behind
            // the camera. A quick turn must not reveal a newly placed hunter.
            Vector3 origin = camera.transform.position;
            for (int sample = 0; sample < 3; sample++)
            {
                float height = sample == 0 ? .3f : sample == 1 ? 1f : 1.7f;
                Vector3 delta = position + Vector3.up * height - origin;
                int count = Physics.RaycastNonAlloc(
                    origin,
                    delta.normalized,
                    visibilityHits,
                    delta.magnitude,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                );
                if (count == visibilityHits.Length)
                {
                    // Incomplete results cannot establish safe concealment.
                    return false;
                }

                bool covered = false;
                for (int index = 0; index < count; index++)
                {
                    Collider collider = visibilityHits[index].collider;
                    // A hunter's own body, the player or another actor is not
                    // scenery cover. Otherwise the source check would authorize
                    // its disappearance merely because the ray hit that hunter.
                    if (collider != null && collider.GetComponentInParent<Player>() == null)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool HasUnlockedExit(NavMeshPath path)
        {
            return HasUnlockedExit(path, out _);
        }

        internal static bool HasUnlockedExit(NavMeshPath path, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            // Navigation meshes may cross locked doors. Check the actual route's
            // door colliders rather than assuming PathComplete means the AI can
            // leave a keyed room. A saturated query is rejected conservatively.
            Vector3[] corners = path.corners;
            if (path.status != NavMeshPathStatus.PathComplete || corners.Length < 2 || corners.Length > 64)
            {
                rejectionReason = $"path-{path.status}-corners-{corners.Length}";
                return false;
            }

            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 start = corners[index - 1] + Vector3.up;
                Vector3 delta = corners[index] - corners[index - 1];
                if (delta.sqrMagnitude < .01f)
                {
                    continue;
                }

                int count = Physics.SphereCastNonAlloc(
                    start,
                    .3f,
                    delta.normalized,
                    hits,
                    delta.magnitude,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore
                );
                RaycastHit[] routeHits = hits;
                if (count == hits.Length)
                {
                    // Factory's long routes intersect many ordinary scenery
                    // colliders. Retry with a fixed, reused larger buffer so
                    // scenery cannot hide a locked door beyond the first hits.
                    // A second saturation still rejects the route; never treat
                    // incomplete physics results as proof that a door is clear.
                    routeHits = denseRouteHits;
                    count = Physics.SphereCastNonAlloc(
                        start,
                        .3f,
                        delta.normalized,
                        routeHits,
                        delta.magnitude,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore
                    );
                }

                if (count == routeHits.Length)
                {
                    rejectionReason = "collider-query-full";
                    return false;
                }

                for (int hit = 0; hit < count; hit++)
                {
                    var door = routeHits[hit].collider?.GetComponentInParent<WorldInteractiveObject>();
                    if (door != null && door.DoorState == EDoorState.Locked)
                    {
                        rejectionReason = "locked-door";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
