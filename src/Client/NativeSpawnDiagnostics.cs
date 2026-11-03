using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using EFT;

namespace RaidRoulette.TestingClient
{
    internal static class NativeSpawnDiagnostics
    {
        private static readonly TraceLimiter Limit = new();
        public static void Reset() => Limit.Reset();
        public static object? Read(object? obj, string name)
        {
            if (obj == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = obj.GetType();
            return type.GetField(name, flags)?.GetValue(obj) ?? type.GetProperty(name, flags)?.GetValue(obj);
        }

        public static bool Cultist(object? wave) => Read(wave, "BossName")?.ToString()?.StartsWith("sectant",
             StringComparison.OrdinalIgnoreCase) == true;
        public static void Write(string stage, object? wave, string extra = "")
        {
            try
            {
                if (!Plugin.TraceEnabled || !(Cultist(wave) || (wave is BossLocationSpawn s && LocalRaidDirector.Managed(s))))
                {
                    return;
                }

                int id = RuntimeHelpers.GetHashCode(wave!);
                string key = stage + id + extra;
                if (!Limit.Allow(key))
                {
                    return;
                }

                Plugin.Trace($"[RR-SPAWN] stage={stage} wave={id} role={Read(wave, "BossName")} zone={Read(wave, "BossZone")} " +
                    $"bornZone={Read(wave, "BornZone")} time={Read(wave, "Time")} chance={Read(wave, "BossChance")} force=" +
                    $"{Read(wave, "ForceSpawn")} shallSpawn={Read(wave, "ShallSpawn")} activated={Read(wave, "Activated")}" +
                    $" trigger={Read(wave, "TriggerType")} {extra}");
            }
            catch (Exception e)
            {
                Plugin.Trace("[RR-SPAWN] observer-error " + e.Message);
            }
        }

        public static void Zone(object zone, WildSpawnType role, bool blocked)
        {
            try
            {
                if (!Plugin.TraceEnabled || !role.ToString().StartsWith("sectant", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string text = $"role={role} zone={Read(zone, "name")} blocked={blocked}";
                if (Limit.Allow("zone" + text))
                {
                    Plugin.Trace("[RR-SPAWN] stage=zone-gate " + text);
                }
            }
            catch
            { /* Observers must never alter game behavior. */
            }
        }
    }

    [HarmonyPatch(typeof(BossSpawnScenario), "Run")]
    internal static class ScenarioRunTrace
    {
        private static void Prefix(BossSpawnScenario __instance, object[] __args)
        {
            NativeSpawnDiagnostics.Reset();
            foreach (var wave in __instance.BossSpawnWaves ?? Array.Empty<BossLocationSpawn>())
            {
                NativeSpawnDiagnostics.Write("scenario-run", wave, "args=" + string.Join(",", __args.Select(x => x?.ToString())));
            }
        }
    }

    [HarmonyPatch(typeof(BossLocationSpawn), "Init")]
    internal static class WaveInitTrace
    {
        private static void Postfix(BossLocationSpawn __instance) => NativeSpawnDiagnostics.Write("wave-init", __instance);
    }

    [HarmonyPatch]
    internal static class WaveActivationTrace
    {
        private static MethodBase[] TargetMethods() => typeof(BossSpawnScenario).GetMethods(BindingFlags
            .Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m
            .Name is "ActivateWave" or "ActivateWavesAfterGameStarted").Cast<MethodBase>().ToArray();
        private static void Prefix(BossLocationSpawn __0, MethodBase __originalMethod) => NativeSpawnDiagnostics
            .Write(__originalMethod.Name + "-enter", __0);
        private static void Postfix(BossLocationSpawn __0, MethodBase __originalMethod) => NativeSpawnDiagnostics
            .Write(__originalMethod.Name + "-exit", __0);
    }

    [HarmonyPatch(typeof(BotBossSpawn), "TrySpawn")]
    internal static class TrySpawnTrace
    {
        private static void Prefix(BossLocationSpawn __0, out BossLocationSpawn? __state)
        {
            __state = CultistZoneFix.Enter(__0);
            NativeSpawnDiagnostics.Write("try-spawn-enter", __0);
        }

        private static Exception? Finalizer(Exception? __exception, BossLocationSpawn? __state)
        {
            CultistZoneFix.Exit(__state);
            return __exception;
        }

        private static void Postfix(BossLocationSpawn __0, bool __result) => NativeSpawnDiagnostics.Write("try-spawn-exit",
             __0, "returned=" + __result + " (not a live-spawn count)");
    }

    [HarmonyPatch(typeof(BotZonesLeaveController), "IsZoneBlockFor")]
    internal static class ZoneGateTrace
    {
        private static void Postfix(BotZonesLeaveController __instance, BotZone __0, WildSpawnType __1, ref bool __result)
        {
            NativeSpawnDiagnostics.Zone(__0, __1, __result);
            if (__result && CultistZoneFix.TryUnblock(__instance, __0, __1))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(BotBossSpawn), "SpawnBossAndFollowers")]
    internal static class PlacementTrace
    {
        private static void Prefix(BossLocationSpawn __1) => NativeSpawnDiagnostics.Write("placement-enter", __1);
    }
}
