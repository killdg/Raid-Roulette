using System;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class SchizophreniaInterior
    {
        private readonly InteriorScareGate gate = new();
        private float next;
        private bool ready;
        internal void Reset()
        {
            gate.Reset();
            next = 0;
            ready = false;
        }

        internal bool Tick(Player player)
        {
            if (Time.unscaledTime < next)
            {
                return ready;
            }

            next = Time.unscaledTime + .5f;
            ready = gate.Tick(Time.unscaledTime, Enclosed(player));
            return ready;
        }

        private static bool Enclosed(Player player)
        {
            // Native indoor volumes reject ordinary outdoor canopies. Bounded sight rays
            // conservatively reject open doorways, visible glass and large open spaces.
            if (player.Environment != EnvironmentType.Indoor)
            {
                return false;
            }

            var eye = PsychWorldScares.ViewCamera != null ? PsychWorldScares.ViewCamera.transform.position : player.Position
                + Vector3.up * 1.6f;
            if (!Physics.Raycast(eye, Vector3.up, 20, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 direction;
                if (i == 8)
                {
                    direction = player.LookDirection;
                    if (direction.sqrMagnitude < .1f)
                    {
                        return false;
                    }
                }
                else
                {
                    float angle = i * Mathf.PI / 4;
                    direction = new Vector3(Mathf.Cos(angle), .05f, Mathf.Sin(angle));
                }

                if (!Physics.Raycast(eye, direction, out var hit, 35, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                var surface = hit.collider.GetComponentInParent<BallisticCollider>();
                if (surface != null && surface.TypeOfMaterial.ToString().IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
