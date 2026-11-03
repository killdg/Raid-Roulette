using RaidRoulette.Runtime;
using HarmonyLib;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace RaidRoulette.Patches
{
    // StartLocalRaid became async in 4.1.x. Patch its synchronous generation stage
    // instead so temporary database changes cannot leak across an await boundary.
    [HarmonyPatch(typeof(LocationLifecycleService), nameof(LocationLifecycleService.GenerateLocationAndLoot))]
    internal static class StartLocalRaidPatch
    {
        private static readonly object GenerationGate = new();
        internal sealed class GenerationState
        {
            public bool Locked;
            public MongoId SessionId;
            public RaidPatchState Raid = RaidPatchState.Empty;
        }

        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId, string name, out GenerationState __state)
        {
            __state = new GenerationState
            {
                SessionId = sessionId
            };
            if (string.Equals(name, "hideout", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Monitor.Enter(GenerationGate);
            __state.Locked = true;
            RaidRulesRuntime.Clear(sessionId);
            RaidDiagnostics.Clear(sessionId);
            __state.Raid = RaidRouletteRuntime.BeginRaid(name, sessionId);
        }

        [HarmonyPostfix]
        private static void Postfix(LocationBase __result, GenerationState __state)
        {
            if (!__state.Locked || __state.Raid.SelectedEvent is null)
            {
                return;
            }

            var id = __state.Raid.SelectedEvent.Id;
            RaidRulesRuntime.ApplyMap(__result, id);
            RaidRulesRuntime.Activate(__state.SessionId, id);
            EventIntegration.Begin(__state.SessionId, __state.Raid.LocationId, id);
            Quests.FenceEvents.Begin(__state.SessionId, id);
            RaidDiagnostics.Begin(__state.SessionId, id, __result);
            if (id is "guarded" or "smokers_lobby" or "gun_game" or "scav_concentrate" or "pmc_sweep" or "raider_assault"
                or "outbreak" or "overrun")
            {
                int nativeCap = Math.Max(0, (__result.BotMaxPvE > 0 ? __result.BotMaxPvE : __result.BotMax) ?? 0);
                int cap = id == "gun_game" ? Math.Min(200, nativeCap) : Math.Min(RaidRulesRuntime.Config.V2.MaxAliveBots, nativeCap);
                int percent = id == "gun_game" ? 50 : id == "smokers_lobby" ? Random.Shared.Next(50, 101) : 100;
                int target = Services.StartupPopulation.Target(cap, percent);
                Testing.TestingControl.SetStartupPopulation(cap, target);
                RaidDiagnostics.Log(
                    __state.SessionId,
                    "startup-population",
                    $"cap={cap} target={target} percent={percent}; client will fill only missing live AI during startup"
                );
            }

            if (id == "clone_wars" && !CloneWarsRuntime.Capture(__state.SessionId))
            {
                RaidRulesRuntime.Clear(__state.SessionId);
                Testing.TestingControl.Record(
                    "normal",
                    __state.Raid.LocationId,
                    "Clone Wars could not snapshot the PMC. Check backend RR-CLONE messages."
                );
            }
        }

        // Harmony runs finalizers on successful calls as well as failed calls.
        // One cleanup site avoids calling event Cleanup twice.
        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception, GenerationState? __state)
        {
            if (__state is null || !__state.Locked)
            {
                return __exception;
            }

            try
            {
                if (__exception is not null)
                {
                    RaidRulesRuntime.Clear(__state.SessionId);
                    RaidDiagnostics.Clear(__state.SessionId);
                }

                RaidRouletteRuntime.CompleteRaid(__state.Raid);
            }
            finally
            {
                __state.Locked = false;
                Monitor.Exit(GenerationGate);
            }

            return __exception;
        }
    }
}
