using HarmonyLib;
using RaidRoulette.Runtime;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace RaidRoulette.Patches
{
    [HarmonyPatch(typeof(BotController), nameof(BotController.Generate))]
    internal static class DiagnosticResponsePatch
    {
        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId, GenerateBotsRequestData request)
        {
            foreach (var item in request.Conditions ?? [])
            {
                RaidDiagnostics.Log(sessionId, "request", $"role={item.Role} count={item.Limit} difficulty={item.Difficulty}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(MongoId sessionId, ref Task<IEnumerable<BotBase?>> __result) => __result = Trace(__result, sessionId);
        private static async Task<IEnumerable<BotBase?>> Trace(Task<IEnumerable<BotBase?>> task, MongoId session)
        {
            var bots = await task.ConfigureAwait(false);
            // BotController returns a materialized list; preserve the original result and exceptions.
            try
            {
                foreach (var bot in bots)
                {
                    RaidDiagnostics.Bot(session, "response", bot);
                }
            }
            catch (Exception e)
            {
                RaidDiagnostics.Log(session, "response-trace-error", e.Message);
            }

            return bots;
        }
    }

    [HarmonyPatch(typeof(BotGenerator), nameof(BotGenerator.PrepareAndGenerateBot))]
    internal static class DiagnosticGeneratorPatch
    {
        [HarmonyPrefix]
        private static void Prefix(MongoId sessionId, BotGenerationDetails botGenerationDetails) => RaidDiagnostics.Log(
            sessionId,
            "generator-input",
            $"role={botGenerationDetails.Role} eventRole={botGenerationDetails.EventRole} pmc={botGenerationDetails.IsPmc}" +
                $" playerScav={botGenerationDetails.IsPlayerScav}"
        );
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MongoId sessionId, BotBase __result) => RaidDiagnostics.Bot(sessionId, "generated", __result);
    }
}
