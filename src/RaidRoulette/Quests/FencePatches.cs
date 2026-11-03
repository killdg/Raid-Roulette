using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;

namespace RaidRoulette.Quests
{
    // GameStart is native SPT's authenticated client-login lifecycle, not a
    // periodic poll. Reset before the client requests its initial quest list;
    // this also handles client crashes while the server stayed running.
    [HarmonyPatch(typeof(GameController), nameof(GameController.GameStart))]
    internal static class FenceClientSessionStart
    {
        private static void Postfix(MongoId sessionId)
        {
            FenceEvents.StartClientSession(sessionId);
        }
    }

    internal static class FenceErrors
    {
        internal static ItemEventRouterResponse Reject(QuestController controller, MongoId session, string message)
        {
            // SPT owns its output accumulator and error envelope; reuse them so
            // rejected accepts/completions cannot partially mutate the profile.
            var fields = AccessTools.GetDeclaredFields(typeof(QuestController));
            var output = (EventOutputHolder)fields.Single(field => field.FieldType == typeof(EventOutputHolder)).GetValue(controller)!;
            var http = (HttpResponseUtil)fields.Single(field => field.FieldType == typeof(HttpResponseUtil)).GetValue(controller)!;
            return http.AppendErrorToOutput(output.GetOutput(session), "Raid Roulette: " + message);
        }

        internal static ItemEventRouterResponse Snapshot(QuestController controller, PmcData profile, MongoId session, MongoId id)
        {
            var holder = (EventOutputHolder)AccessTools.GetDeclaredFields(typeof(QuestController)).Single(field => field
                .FieldType == typeof(EventOutputHolder)).GetValue(controller)!;
            var output = holder.GetOutput(session);
            var quest = FenceEvents.ReconcileCompletion(profile, session, id);
            if (quest != null && output.ProfileChanges?.TryGetValue(session, out var changes) == true)
            {
                changes.QuestsStatus ??= new List<QuestStatus>();
                changes.QuestsStatus.Add(quest);
            }

            return output;
        }
    }

    [HarmonyPatch(typeof(QuestController), nameof(QuestController.AcceptQuest))]
    internal static class FenceQuestAccept
    {
        private static bool Prefix(QuestController __instance, PmcData pmcData, AcceptQuestRequestData acceptedQuest,
             MongoId sessionID, ref ItemEventRouterResponse __result)
        {
            string id = acceptedQuest.QuestId.ToString();
            if (!FenceEvents.Owns(id))
            {
                return true;
            }

            if (FenceEvents.Enabled(sessionID) && !FenceEvents.RaidRunning(sessionID))
            {
                foreach (var previous in FenceEvents.ReplaceQueuedAssignment(pmcData, sessionID, id))
                {
                    // Native accept reuses this output accumulator. Include each
                    // retired ticket so the client removes it from its active list.
                    FenceErrors.Snapshot(__instance, pmcData, sessionID, previous.QId);
                }

                return true;
            }

            __result = FenceErrors.Reject(
                __instance,
                sessionID,
                FenceEvents.RaidRunning(sessionID) ? "Finish the current assigned raid before switching events."
                    : "Enable Trader Event Activation before accepting a Fence RR event."
            );
            return false;
        }
    }

    [HarmonyPatch(typeof(QuestController), nameof(QuestController.CompleteQuest))]
    internal static class FenceQuestComplete
    {
        [ThreadStatic]
        internal static bool CompletingRaid;
        private static bool Prefix(QuestController __instance, PmcData pmcData, CompleteQuestRequestData request,
             MongoId sessionId, ref ItemEventRouterResponse __result)
        {
            if (!FenceEvents.Owns(request.QuestId.ToString()) || CompletingRaid)
            {
                return true;
            }

            // A stale native finish button is not a critical request failure.
            // Acknowledge the authoritative status without executing rewards or
            // completing the task; assigned raid-end processing still owns it.
            __result = FenceErrors.Snapshot(__instance, pmcData, sessionId, request.QuestId);
            return false;
        }
    }

    [HarmonyPatch(typeof(MatchController), nameof(MatchController.EndLocalRaidAsync))]
    internal static class FenceRaidEnd
    {
        private static void Postfix(MongoId sessionId, ref Task __result)
        {
            __result = Finish(__result, sessionId);
        }

        private static async Task Finish(Task native, MongoId session)
        {
            await native.ConfigureAwait(false);
            try
            {
                await FenceEvents.Finish(session).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A task failure must never turn a successfully saved native raid
                // into a failed end-raid request or trap the player on deployment.
                FenceEvents.Warn(exception);
            }
        }
    }

    [HarmonyPatch(typeof(SPTarkov.Server.Core.Services.InRaid.LocationLifecycleService), "StartLocalRaidAsync")]
    internal static class FenceRaidSide
    {
        private static void Prefix(MongoId sessionId, StartLocalRaidRequestData request)
        {
            FenceEvents.Side(sessionId, string.Equals(request.PlayerSide, "pmc", StringComparison.OrdinalIgnoreCase));
        }
    }
}
