using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Communications;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    internal sealed class DeveloperQuickTravel
    {
        private static readonly Collider[] bodyHits = new Collider[16];
        private ConfigEntry<bool> enabled = null!;
        private ConfigEntry<KeyboardShortcut> hotkey = null!;
        internal void Bind(ConfigFile config)
        {
            enabled = AdvancedMenu.Bind(
                config,
                "9. Advanced - Navigation",
                "Enable quick travel",
                false,
                "Local testing aid. Look at reachable ground and press the shortcut to move there. "
                    + "Requires advanced options. Does not grant flight or invulnerability."
            );
            hotkey = AdvancedMenu.Bind(
                config,
                "9. Advanced - Navigation",
                "Quick travel shortcut",
                new KeyboardShortcut(KeyCode.F4),
                "Move to a clear ground position under the crosshair, up to 250 metres away."
            );
        }

        internal void Tick()
        {
            if (!AdvancedMenu.Enabled || !enabled.Value || !hotkey.Value.IsDown() || Cursor.lockState != CursorLockMode.Locked
                || Plugin.Local?.HealthController?.IsAlive != true)
            {
                return;
            }

            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            var camera = PsychWorldScares.ViewCamera;
            if (world == null || world is HideoutGameWorld || camera == null)
            {
                return;
            }

            var ray = new Ray(camera.transform.position + camera.transform.forward * 0.4f, camera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, 250, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit
                .normal.y < 0.5f)
            {
                Reject("aim at a floor", default);
                return;
            }

            if (!NavMesh.SamplePosition(hit.point, out var ground, 1, NavMesh.AllAreas) || Vector3.Distance(ground.position, hit.point) > 1)
            {
                Reject("no nearby AI navigation mesh", hit.point);
                return;
            }

            int count = Physics.OverlapCapsuleNonAlloc(
                ground.position + Vector3.up * 0.4f,
                ground.position + Vector3.up * 1.5f,
                0.25f,
                bodyHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            if (count == bodyHits.Length)
            {
                Reject("too many clearance obstacles", ground.position);
                return;
            }

            for (int index = 0; index < count; index++)
            {
                var collider = bodyHits[index];
                if (collider != null && collider.GetComponentInParent<Player>() != Plugin.Local)
                {
                    Reject("body clearance blocked by " + collider.name, ground.position);
                    return;
                }
            }

            Plugin.Local!.Teleport(ground.position + Vector3.up * 0.05f);
            Plugin.Trace("[RR-NAVIGATION] quick-travel destination=" + ground.position);
        }

        private static void Reject(string reason, Vector3 destination)
        {
            NotificationManager.DisplayMessageNotification("Quick travel: " + reason + ".");
            Plugin.Trace($"[RR-NAVIGATION] rejected reason={reason} destination={destination}");
        }
    }
}
