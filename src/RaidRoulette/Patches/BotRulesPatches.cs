using System.Reflection;
using RaidRoulette.Runtime;
using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace RaidRoulette.Patches
{
    [HarmonyPatch(typeof(BotGenerator), nameof(BotGenerator.PrepareAndGenerateBot))]
    internal static class BotRoleAndAppearancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId, ref BotGenerationDetails botGenerationDetails)
        {
            if (SeasonalRaidIsolation.Active(sessionId))
            {
                // EventRole is SPT's seasonal role override, separate from the
                // role explicitly requested by RR (including RR Outbreak).
                botGenerationDetails.EventRole = null;
            }

            var state = RaidRulesRuntime.Get(sessionId);
            var service = RaidRulesRuntime.Service;
            if (state is null || service is null)
            {
                return;
            }

            try
            {
                botGenerationDetails = service.PrepareRole(botGenerationDetails, state);
            }
            catch (Exception error)
            {
                service.Warning(state, "role", error);
            }
        }

        [HarmonyPostfix]
        private static void Postfix(MongoId sessionId, BotGenerationDetails botGenerationDetails, BotBase __result)
        {
            var state = RaidRulesRuntime.Get(sessionId);
            var service = RaidRulesRuntime.Service;
            if (state is null || service is null || __result is null)
            {
                return;
            }

            try
            {
                service.ApplyAppearance(__result, botGenerationDetails, state, RaidRulesRuntime.Config.V2);
                service.VerifyGeneratedBot(__result, botGenerationDetails, state, RaidRulesRuntime.Config.V2);
            }
            catch (Exception error)
            {
                service.Warning(state, "appearance", error);
            }
        }
    }

    [HarmonyPatch(typeof(BotInventoryGenerator), nameof(BotInventoryGenerator.GenerateInventory))]
    internal static class BotInventoryRulesPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId, ref BotType botJsonTemplate, BotGenerationDetails botGenerationDetails)
        {
            var state = RaidRulesRuntime.Get(sessionId);
            var service = RaidRulesRuntime.Service;
            if (state is null || service is null)
            {
                return;
            }

            try
            {
                if (RaidRoulette.Shared.GauntletPolicy.UsesEquipmentProgression(state.EventId))
                {
                    int wave = RaidRoulette.Shared.GauntletPolicy.FindStage(state.EventId, botGenerationDetails.Role);
                    if (wave >= 0)
                    {
                        botGenerationDetails.BotLevel = RaidRoulette.Shared.GauntletPolicy.StageLevel(state.EventId, wave);
                    }
                }

                botJsonTemplate = service.PrepareInventory(botJsonTemplate, botGenerationDetails, state, RaidRulesRuntime.Config.V2);
            }
            catch (Exception error)
            {
                service.Warning(state, "inventory", error);
            }
        }
    }

    [HarmonyPatch(typeof(MatchController), nameof(MatchController.EndLocalRaidAsync))]
    internal static class RaidRuleCleanupPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId)
        {
            RaidRulesRuntime.Clear(sessionId);
            RaidDiagnostics.Clear(sessionId);
            SeasonalRaidIsolation.Clear(sessionId);
        }
    }

    // Clone at the response boundary so previously cached PMCs are covered without
    // mutating shared native caches or another session's profiles.
    [HarmonyPatch(typeof(BotController), nameof(BotController.Generate))]
    internal static class CloneWarsResponsePatch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void Postfix(MongoId sessionId, ref Task<IEnumerable<BotBase?>> __result) => __result =
             GunGameRuntime.TransformResponse(
            sessionId,
            GrenadierRuntime.TransformResponse(
                sessionId,
                WeaponCategoryRuntime.TransformResponse(
                    sessionId,
                    OverloadedRuntime.TransformResponse(
                        sessionId,
                        SmokersRuntime.TransformResponse(
                            sessionId,
                            GiftRuntime.TransformResponse(sessionId, CloneWarsRuntime.TransformResponse(sessionId, __result))
                        )
                    )
                )
            )
        );
    }
}
