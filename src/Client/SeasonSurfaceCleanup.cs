using System.Collections.Generic;
using System.Reflection;
using EFT.Weather;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    /// <summary>
    /// SnowWetRenderer.OnDisable releases render callbacks but leaves global snow
    /// shader values behind. These survive scene unload and can coat the next raid's
    /// hands and weapon even after the native season controller returns to summer.
    /// </summary>
    internal static class SeasonSurfaceCleanup
    {
        // Native SnowWetRenderer shader property: strength of accumulated surface snow.
        private static readonly int SurfaceSnowIntensity = Shader.PropertyToID("_SurfaceSnowIntensity");
        internal static void Clear(RainController controller)
        {
            if (controller._snowWetRenderer != null)
            {
                controller._snowWetRenderer.Wetting = 0f;
                controller._snowWetRenderer.WinterShow = false;
                controller._snowWetRenderer.StormEnabled = false;
                controller._snowWetRenderer.enabled = false;
            }

            if (controller._snowRippleController != null)
            {
                controller._snowRippleController.Intensity = 0f;
                controller._snowRippleController.enabled = false;
            }

            if (controller._snowFlakes != null)
            {
                controller._snowFlakes.Intensity = 0f;
                controller._snowFlakes.gameObject.SetActive(false);
            }

            // These are native global keywords, not per-material copies. Disabling a
            // snow renderer alone cannot reset them. Never call this during Winter
            // or SpringEarly, which intentionally retain snow rendering.
            Shader.DisableKeyword("WINTER_SNOW");
            Shader.DisableKeyword("SNOW_GLITTERS");
            Shader.SetGlobalFloat(SurfaceSnowIntensity, 0f);
            WetRenderer.WetOffMaskUsed = false;
        }
    }

    [HarmonyPatch]
    internal static class SeasonWarmSurfacePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(RainController), nameof(RainController.SetupSummer));
            yield return AccessTools.Method(typeof(RainController), nameof(RainController.SetupSpring));
            yield return AccessTools.Method(typeof(RainController), nameof(RainController.SetupAutumn));
            yield return AccessTools.Method(typeof(RainController), nameof(RainController.SetupAutumnLate));
        }

        private static void Postfix(RainController __instance)
        {
            SeasonSurfaceCleanup.Clear(__instance);
            Plugin.Trace("[RR-SEASON] Cleared stale snow surfaces for native non-snow season");
        }
    }

    [HarmonyPatch(typeof(RainController), nameof(RainController.OnDestroy))]
    internal static class SeasonSurfaceTeardownPatch
    {
        private static void Postfix(RainController __instance)
        {
            var current = WeatherController.Instance != null ? WeatherController.Instance.RainController : null;
            // A delayed destruction callback from the previous scene must not clear
            // a newly initialized winter controller's legitimate snow state.
            if (current != null && current != __instance)
            {
                return;
            }

            SeasonSurfaceCleanup.Clear(__instance);
        }
    }
}
