using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using RaidRoulette.Core;
using RaidRoulette.Runtime;
using RaidRoulette.Shared;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils;

namespace RaidRoulette.Quests
{
    /// <summary>
    /// Optional, reward-free Fence assignments. Native quest status persists the
    /// selection; only an actually generated assigned raid creates an end receipt.
    /// Existing campaign quest IDs and progress are never modified.
    /// </summary>
    internal static class FenceEvents
    {
        private const string Fence = "579dc571d53a0658a154fbec";
        private static readonly string[] Maps =
        {
            "bigmap",
            "woods",
            "shoreline",
            "lighthouse",
            "rezervbase",
            "interchange",
            "tarkovstreets",
            "sandbox",
            "sandbox_high",
            "factory4_day",
            "factory4_night",
            "laboratory"
        };
        private static readonly ConcurrentDictionary<MongoId, bool> enabled = new();
        private static readonly ConcurrentDictionary<MongoId, string> pending = new();
        private static readonly ConcurrentDictionary<MongoId, string> running = new();
        private sealed class TrialAttempt
        {
            internal string EventId = "";
            internal string Token = Guid.NewGuid().ToString("N");
            internal bool Completed;
        }

        private static readonly ConcurrentDictionary<MongoId, TrialAttempt> attempts = new();
        private static QuestHelper questHelper = null!;
        private static readonly ConcurrentDictionary<MongoId, bool> pmcRaids = new();
        private static readonly Dictionary<string, string> events = new();
        private static readonly Dictionary<string, string[]> eligibleMaps = new();
        private const string RespawnAssignment = "gun_game_respawns";
        internal static bool? GunGameRespawnMode(MongoId session)
        {
            // Keep the variant attached to this raid's Fence receipt, rather than
            // rewriting the user's saved respawn preference or creating a new
            // gameplay event ID that could diverge from the Gun Game ladder.
            if (!running.TryGetValue(session, out string? quest) || !events.TryGetValue(quest, out string? eventId)
                || eventId != "gun_game")
            {
                return null;
            }

            return quest == Id(RespawnAssignment);
        }

        private static IEnumerable<(Models.RaidEvent Event, string AssignmentId, string Name)> Offers()
        {
            foreach (var raidEvent in EventRegistry.GetEvents().Where(value => value.Id != "normal"))
            {
                yield return (raidEvent, raidEvent.Id, raidEvent.Name);
                if (raidEvent.Id == "gun_game")
                {
                    yield return (raidEvent, RespawnAssignment, "Gun Game [Respawns]");
                }
            }
        }

        private static SaveServer save = null!;
        private static QuestController controller = null!;
        private static Action<string> log = null!;
        internal static string Id(string eventId, string suffix = "quest")
        {
            // Stable 24-hex native MongoId, isolated from both task-pack catalogs.
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("killdg/rr/fence/v1/" + eventId + "/" + suffix));
            return Convert.ToHexString(hash)[..24].ToLowerInvariant();
        }

        internal static bool Owns(string questId) => events.ContainsKey(questId);
        internal static void Warn(Exception exception)
        {
            log("[RR-FENCE] Assignment completion failed: " + exception.GetBaseException().Message);
        }

        internal static bool Enabled(MongoId session) => enabled.GetValueOrDefault(session);
        internal static void InitializeRetries(QuestHelper helper)
        {
            questHelper = helper;
        }

        internal static bool RequiresClear(string eventId) => GauntletPolicy.Handles(eventId);
        internal static bool ActiveStatus(QuestStatusEnum status) => status is QuestStatusEnum.Started or QuestStatusEnum
            .AvailableForFinish;
        internal static string? AcceptedMarkerQuest(PmcData? profile)
        {
            return profile?.Quests?.FirstOrDefault(quest => Owns(quest.QId.ToString()) && ActiveStatus(quest.Status))?.QId.ToString();
        }

        internal static QuestStatus[] ReplaceQueuedAssignment(PmcData profile, MongoId session, string requested)
        {
            if (running.ContainsKey(session))
            {
                throw new InvalidOperationException("An assigned raid is still running; finish it before switching events.");
            }

            var replaced = profile.Quests?.Where(quest => Owns(quest.QId.ToString()) && quest.QId.ToString() != requested
                && ActiveStatus(quest.Status)).ToArray() ?? [];
            foreach (var quest in replaced)
            {
                Reoffer(profile, quest.QId);
            }

            pending.TryRemove(session, out _);
            return replaced;
        }

        internal static bool RaidRunning(MongoId session) => running.ContainsKey(session);
        internal static QuestStatus? ReconcileCompletion(PmcData profile, MongoId session, MongoId id)
        {
            var quest = profile.Quests?.FirstOrDefault(value => value.QId == id);
            bool assigned = running.TryGetValue(session, out string? active) && active == id.ToString();
            if (quest != null && !assigned && quest.Status == QuestStatusEnum.AvailableForFinish)
            {
                // Recover a stale finish button from an older client without
                // awarding completion or changing an active raid's assignment.
                Reoffer(profile, id);
            }

            return quest;
        }

        internal static string? AttemptToken(MongoId session, string eventId)
        {
            return attempts.TryGetValue(session, out var attempt) && attempt.EventId == eventId ? attempt.Token : null;
        }

        internal static bool ReportClear(MongoId session, string eventId, string token)
        {
            // A receipt belongs to one generated Fence raid, not a later retry of
            // the same event. No progress report can finish a quest before raid end.
            if (!running.ContainsKey(session) || !attempts.TryGetValue(session, out var attempt) || attempt.EventId != eventId
                || attempt.Token != token)
            {
                return false;
            }

            attempt.Completed = true;
            return true;
        }

        internal static void Side(MongoId session, bool pmc)
        {
            pmcRaids[session] = pmc;
        }

        internal static void Configure(MongoId session, bool value)
        {
            enabled[session] = value;
            if (value)
            {
                var profile = Profile(session);
                if (profile?.Quests != null)
                {
                    // Upgrade only this optional Fence catalog's finished tasks.
                    // Existing campaign IDs and currently active assignments stay
                    // untouched. Native profile refresh exposes the re-offer.
                    foreach (var quest in profile.Quests.Where(quest => Owns(quest.QId.ToString()) && quest.Status is QuestStatusEnum
                        .Success or QuestStatusEnum.Fail))
                    {
                        Reoffer(profile, quest.QId);
                    }
                }
            }

            if (!value)
            {
                pending.TryRemove(session, out _);
            }
        }

        private static void Reoffer(PmcData profile, MongoId id)
        {
            // Reset the native condition state and timers for the same stable ID.
            // Do not auto-accept: the player must choose it again at Fence.
            questHelper.ResetQuestState(profile, QuestStatusEnum.AvailableForStart, id);
            var quest = profile.Quests?.FirstOrDefault(value => value.QId == id);
            if (quest != null)
            {
                quest.StartTime = 0;
            }

            // Native ResetQuestState clears completed conditions, but leaves
            // separately stored task counters. Remove only counters owned by
            // this reward-free RR assignment; campaign counters are untouched.
            if (profile.TaskConditionCounters != null)
            {
                foreach (var counter in profile.TaskConditionCounters.Where(pair => pair.Value.SourceId == id).Select(pair => pair
                    .Key).ToArray())
                {
                    profile.TaskConditionCounters.Remove(counter);
                }
            }
        }

        internal static void ResetSession(PmcData profile, MongoId session)
        {
            pending.TryRemove(session, out _);
            running.TryRemove(session, out _);
            attempts.TryRemove(session, out _);
            pmcRaids.TryRemove(session, out _);
            // These are reusable event selectors, not story-quest progress.
            // Clear every RR ticket at login and after each raid, including a
            // ticket left accepted when the client crashed. Stable definitions
            // remain in Fence's catalog, available for a fresh manual accept.
            foreach (var quest in profile.Quests?.Where(value => Owns(value.QId.ToString())).ToArray() ?? [])
            {
                Reoffer(profile, quest.QId);
            }
        }

        internal static void StartClientSession(MongoId session)
        {
            var profile = Profile(session);
            if (profile != null)
            {
                ResetSession(profile, session);
                log("[RR-FENCE] Client startup: RR assignments restored to new, unaccepted offers.");
            }
        }

        internal static void Initialize(SaveServer saves, QuestController quests, CustomQuestService customQuests,
             JsonUtil json, Action<string> logger)
        {
            save = saves;
            controller = quests;
            log = logger;
            foreach (var offer in Offers())
            {
                var raidEvent = offer.Event;
                string[] maps = Maps.Where(map => RaidRulesRuntime.CanRun(raidEvent.Id, map)).ToArray();
                if (maps.Length == 0)
                {
                    continue;
                }

                string id = Id(offer.AssignmentId);
                events.Add(id, raidEvent.Id);
                eligibleMaps.TryAdd(raidEvent.Id, maps);
                string title = "[RR: " + offer.Name + "]";
                string objective = RequiresClear(raidEvent.Id) ? "Clear every wave." : "Play one raid.";
                var locales = new Dictionary<string, string>
                {
                    [id + " name"] = title,
                    [id + " description"] = FenceBriefings.Get(offer.AssignmentId),
                    [id + " startedMessageText"] = "It's arranged.",
                    [id + " successMessageText"] = "We're done here.",
                    [id + " failMessageText"] = "Another time, then.",
                    [id + " acceptPlayerMessage"] = "I will take that assignment.",
                    [id + " completePlayerMessage"] = "The raid is over.",
                    [Id(offer.AssignmentId, "condition")] = objective
                };
                // JsonUtil uses the same native polymorphic quest converter as SPT.
                var definition = json.Deserialize<Quest>(Definition(offer.AssignmentId, title).ToJsonString())!;
                var result = customQuests.CreateQuest(new NewQuestDetails { NewQuest = definition, Locales = new() { ["en"] = locales } });
                if (!result.Success)
                {
                    throw new InvalidOperationException("Fence event registration: " + string.Join("; ", result.Errors));
                }
            }
        }

        private static JsonObject Definition(string eventId, string title)
        {
            string id = Id(eventId);
            return new JsonObject
            {
                ["_id"] = id,
                ["QuestName"] = title,
                ["name"] = id + " name",
                ["description"] = id + " description",
                ["traderId"] = Fence,
                ["side"] = "Pmc",
                ["type"] = "Exploration",
                ["location"] = "any",
                ["image"] = FenceImages.RoutePrefix + (eventId == RespawnAssignment ? "gun_game" : eventId) + ".png",
                ["acceptanceAndFinishingSource"] = "eft",
                ["progressSource"] = "eft",
                ["startedMessageText"] = id + " startedMessageText",
                ["successMessageText"] = id + " successMessageText",
                ["failMessageText"] = id + " failMessageText",
                ["acceptPlayerMessage"] = id + " acceptPlayerMessage",
                ["completePlayerMessage"] = id + " completePlayerMessage",
                ["canShowNotificationsInGame"] = true,
                ["instantComplete"] = false,
                ["isKey"] = false,
                ["secretQuest"] = false,
                ["restartable"] = false,
                ["status"] = 0,
                ["conditions"] = new JsonObject
                {
                    ["AvailableForStart"] = new JsonArray(),
                    ["AvailableForFinish"] = new JsonArray(new JsonObject { ["id"] = Id(eventId, "condition"), ["conditionType"] =
                         "CounterCreator", ["dynamicLocale"] = false, ["completeInSeconds"] = 0, ["oneSessionOnly"] = false,
                         ["isNecessary"] = false, ["isResetOnConditionFailed"] = false, ["doNotResetIfCounterCompleted"] = false,
                         ["globalQuestCounterId"] = "", ["parentId"] = "", ["value"] = 1, ["type"] = "Exploration", ["index"] = 0,
                         ["visibilityConditions"] = new JsonArray(), ["counter"] = new JsonObject { ["id"] = Id(eventId, "counter"),
                         // An empty counter can evaluate as complete on accept.
                    // This mod-owned, nonexistent map trigger deliberately
                    // never fires. Native raid-end processing owns task
                    // completion; ordinary kills/extracts cannot finish it.
                    ["conditions"] = new JsonArray(new JsonObject { ["id"] = Id(eventId, "raid-end-placeholder"), ["conditionType"] =
                         "VisitPlace", ["dynamicLocale"] = false, ["target"] = "raidroulette_server_raid_end_only", ["value"] = 1 }) } }),
                    ["Fail"] = new JsonArray()
                },
                ["rewards"] = new JsonObject
                {
                    ["Started"] = new JsonArray(),
                    ["Success"] = new JsonArray(),
                    ["Fail"] = new JsonArray()
                }
            };
        }

        private static PmcData? Profile(MongoId session) => save?.GetProfile(session)?.CharacterData?.PmcData;
        internal static bool HasOtherAssignment(PmcData profile, string requested)
        {
            return profile.Quests?.Any(quest => Owns(quest.QId.ToString()) && quest.QId.ToString() != requested && quest
                .Status is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish) == true;
        }

        internal static string? Select(MongoId session, string? map)
        {
            pending.TryRemove(session, out _);
            if (!Enabled(session) || !pmcRaids.GetValueOrDefault(session))
            {
                return null;
            }

            var quest = Profile(session)?.Quests?.Where(value => Owns(value.QId.ToString()) && ActiveStatus(value.Status))
                .OrderBy(value => value.StartTime).FirstOrDefault();
            if (quest == null || !RaidRulesRuntime.CanRun(events[quest.QId.ToString()], map))
            {
                return null;
            }

            pending[session] = quest.QId.ToString();
            return events[quest.QId.ToString()];
        }

        internal static void Begin(MongoId session, string eventId)
        {
            running.TryRemove(session, out _);
            attempts.TryRemove(session, out _);
            if (pending.TryRemove(session, out string? quest) && events[quest] == eventId)
            {
                running[session] = quest;
                if (RequiresClear(eventId))
                {
                    attempts[session] = new TrialAttempt
                    {
                        EventId = eventId
                    };
                }

                log("[RR-FENCE] Assigned event generated: " + eventId);
            }
        }

        internal static async Task Finish(MongoId session)
        {
            try
            {
                FinishAssignedRaid(session);
            }
            finally
            {
                var profile = Profile(session);
                if (profile != null)
                {
                    // Native end-raid processing has already saved gameplay.
                    // Persist the clean selector state before the client's
                    // post-raid profile reload. This is awaited, not a job.
                    ResetSession(profile, session);
                    await save.SaveProfileAsync(session).ConfigureAwait(false);
                }
            }
        }

        private static void FinishAssignedRaid(MongoId session)
        {
            // Called only after native EndLocalRaidAsync succeeds, including death
            // and disconnect reports. A cancelled deployment never reaches here.
            if (!running.TryRemove(session, out string? id))
            {
                return;
            }

            attempts.TryRemove(session, out var attempt);
            var profile = Profile(session);
            var quest = profile?.Quests?.FirstOrDefault(value => value.QId.ToString() == id);
            if (profile == null || quest == null || !ActiveStatus(quest.Status))
            {
                return;
            }

            if (RequiresClear(events[id]) && attempt?.Completed != true)
            {
                // Native failure sends Fence's failure message. Native reset then
                // clears this assignment's condition state and makes the same ID
                // available for a fresh accept on the post-raid profile reload.
                // No other quests, stash items or campaign progress are touched.
                questHelper.FailQuest(profile, new FailQuestRequestData { QuestId = new MongoId(id) }, session);
                Reoffer(profile, new MongoId(id));
                log("[RR-FENCE] Incomplete Trials failed and re-offered: " + events[id]);
                return;
            }

            try
            {
                FenceQuestComplete.CompletingRaid = true;
                controller.CompleteQuest(profile, new CompleteQuestRequestData { QuestId = new MongoId(id) }, session);
            }
            finally
            {
                FenceQuestComplete.CompletingRaid = false;
            }

            if (quest.Status == QuestStatusEnum.Success)
            {
                Reoffer(profile, new MongoId(id));
                log("[RR-FENCE] Completed assignment and re-offered: " + events[id]);
            }
            else
            {
                log("[RR-FENCE] Native completion did not succeed; assignment remains active: " + events[id]);
            }
        }

        internal static object[] Markers(MongoId session)
        {
            if (!Enabled(session))
            {
                return[];
            }

            var profile = Profile(session);
            string? active = AcceptedMarkerQuest(profile);
            if (active == null)
            {
                return[];
            }

            return events.Where(pair => pair.Key == active).SelectMany(pair => eligibleMaps[pair.Value].Select(map =>
                 (object)new { quest = pair.Key, map, title = "RR: " + (pair.Key == Id(RespawnAssignment) ? "Gun Game [Respawns]"
                : EventRegistry.GetEvents().Single(value => value.Id == pair.Value).Name) })).ToArray();
        }
    }
}
