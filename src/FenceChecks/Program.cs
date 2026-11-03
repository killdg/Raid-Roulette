using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using RaidRoulette.Core;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }

    checks++;
    Console.WriteLine("PASS " + message);
}

var json = new JsonUtil([new SptJsonConverterRegistrator()]);
var table = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
typeof(TemplateTable).GetProperty("Quests")!.SetValue(table, new Dictionary<MongoId, Quest>());
var locales = new LocaleTable
{
    Global = new()
    {
        ["en"] = new LazyLoad<GlobalLocaleDictionary>(() => new GlobalLocaleDictionary(), true)
    },
    Menu = new(),
    Languages = new()
    {
        ["en"] = "English"
    }
};
var service = new CustomQuestService(table, locales, null!, (QuestConfig)RuntimeHelpers.GetUninitializedObject(typeof(QuestConfig)));
var runtime = typeof(EventRegistry).Assembly.GetType("RaidRoulette.Quests.FenceEvents", true)!;
var definition = runtime.GetMethod("Definition", BindingFlags.NonPublic | BindingFlags.Static)!;
var ids = new HashSet<string>();
foreach (var raidEvent in EventRegistry.GetEvents().Where(value => value.Id != "normal"))
{
    var node = (JsonObject)definition.Invoke(null, [raidEvent.Id, "RR: " + raidEvent.Name])!;
    var quest = json.Deserialize<Quest>(node.ToJsonString())!;
    Check(
        node["image"]?.GetValue<string>() == "/files/quest/raidroulette-events/briefings-v2/" + raidEvent.Id + ".png",
        raidEvent.Id + " uses its own local briefing image"
    );
    Check(ids.Add(quest.Id.ToString()), raidEvent.Id + " has a unique native quest ID");
    Check(quest.TraderId.ToString() == "579dc571d53a0658a154fbec", raidEvent.Id + " belongs to Fence");
    Check(quest.Rewards!.Values.All(rewards => !rewards.Any()), raidEvent.Id + " cannot grant farming rewards");
    var placeholder = quest.Conditions.AvailableForFinish!.Single().Counter!.Conditions!.Single();
    Check(
        placeholder.ConditionType == "VisitPlace" && json.Serialize(placeholder)!.Contains("raidroulette_server_raid_end_only"),
        raidEvent.Id + " requires the server-owned raid-end placeholder instead of an empty counter"
    );
    var result = service.CreateQuest(new NewQuestDetails { NewQuest = quest, Locales = new() { ["en"] = new() { [quest
        .Id + " name"] = quest.QuestName! } } });
    Check(result.Success, raidEvent.Id + " registers through native SPT CustomQuestService");
    Check(
        json.Deserialize<Quest>(json.Serialize(quest)!)!.Id == quest.Id,
        raidEvent.Id + " survives native serialization"
    );
}

Check(table.Quests.Count == ids.Count, "All registered assignments remain in the native catalog");
var eventLookup = (Dictionary<string, string>)runtime.GetField("events", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
foreach (var raidEvent in EventRegistry.GetEvents().Where(value => value.Id != "normal"))
{
    var questNode = (JsonObject)definition.Invoke(null, [raidEvent.Id, raidEvent.Name])!;
    eventLookup.Add(questNode["_id"]!.GetValue<string>(), raidEvent.Id);
}

string firstId = eventLookup.First().Key;
string secondId = eventLookup.Skip(1).First().Key;
var profile = new SPTarkov.Server.Core.Models.Eft.Common.PmcData
{
    Quests = new()
    {
        new QuestStatus
        {
            QId = new MongoId(firstId),
            StartTime = 1,
            Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
            StatusTimers = new()
        }
    }
};
var hasOther = runtime.GetMethod("HasOtherAssignment", BindingFlags.Static | BindingFlags.NonPublic)!;
Check(
    (bool)hasOther.Invoke(null, [profile, secondId])!,
    "An accepted assignment blocks accepting a different event"
);
profile.Quests[0].Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success;
Check(!(bool)hasOther.Invoke(null, [profile, secondId])!, "A completed assignment permits the next event");
profile.Quests[0].Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForFinish;
Check(
    (bool)hasOther.Invoke(null, [profile, secondId])!,
    "A finishing assignment still blocks conflicting accepts"
);
var session = new MongoId();
runtime.GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session, false ]);
Check(
    runtime.GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session, "bigmap"]) == null,
    "Disabled Fence activation cannot replace raid selection"
);
runtime.GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session, true ]);
runtime.GetMethod("Side", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session, false ]);
Check(
    runtime.GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session, "bigmap"]) == null,
    "A Scav raid cannot consume a Fence PMC assignment"
);
var requiresClear = runtime.GetMethod("RequiresClear", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (string trial in new[]
{
    "trials",
    "guard_trials",
    "extreme_trials",
    "total_trial",
    "snowball"
}

)
{
    Check((bool)requiresClear.Invoke(null, [trial])!, trial + " requires a successful clear for Fence");
}

Check(!(bool)requiresClear.Invoke(null, ["stormtide"])!, "Ordinary event assignments retain one-raid completion");
var pendingReceipts = (System.Collections.Concurrent.ConcurrentDictionary<MongoId, string>)runtime
    .GetField("pending", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
string trialQuest = eventLookup.Single(pair => pair.Value == "trials").Key;
var beginAttempt = runtime.GetMethod("Begin", BindingFlags.Static | BindingFlags.NonPublic)!;
var attemptToken = runtime.GetMethod("AttemptToken", BindingFlags.Static | BindingFlags.NonPublic)!;
var reportClear = runtime.GetMethod("ReportClear", BindingFlags.Static | BindingFlags.NonPublic)!;
runtime.GetField("log", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, (Action<string>)(_ =>
{
}));
pendingReceipts[session] = trialQuest;
beginAttempt.Invoke(null, [session, "trials"]);
string nonce = (string)attemptToken.Invoke(null, [session, "trials"])!;
Check(!string.IsNullOrEmpty(nonce), "Generated assigned Trials receives a raid-bound nonce");
Check(
    !(bool)reportClear.Invoke(null, [session, "snowball", nonce])!,
    "A different event cannot clear this assignment"
);
Check(
    !(bool)reportClear.Invoke(null, [session, "trials", "incorrect"])!,
    "An incorrect nonce cannot clear Trials"
);
Check(
    (bool)reportClear.Invoke(null, [session, "trials", nonce])!,
    "Matching assigned raid can acknowledge its clear"
);
Check((bool)reportClear.Invoke(null, [session, "trials", nonce])!, "Receipt resend is idempotent");
pendingReceipts[session] = trialQuest;
beginAttempt.Invoke(null, [session, "trials"]);
Check(
    !(bool)reportClear.Invoke(null, [session, "trials", nonce])!,
    "A delayed clear from an earlier attempt cannot clear a retry"
);
Check(
    attemptToken.Invoke(null, [new MongoId(), "trials"]) == null,
    "Unassigned sessions cannot obtain a Trials receipt"
);
// This fixture registers definitions directly rather than running full mod DI.
// Populate the marker map cache that normal Initialize fills before requests.
var markerMaps = (Dictionary<string, string[]>)runtime.GetField("eligibleMaps", BindingFlags.Static | BindingFlags
    .NonPublic)!.GetValue(null)!;
foreach (string eventId in eventLookup.Values)
{
    markerMaps[eventId] = new[]
    {
        "factory4_day"
    };
}

var rejectedManual = System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(
        new() { Action = "apply", Event = "gun_game", EventsEnabled = true, TraderEventActivation = true, QueueId =
             "fence-manual-rejection" },
        session
    ));
Check(
    !rejectedManual.GetProperty("ok").GetBoolean(),
    "Fence-owned selection rejects a manual Gun Game queue instead of falsely acknowledging it"
);
Check(
    !rejectedManual.GetProperty("queuePending").GetBoolean(),
    "Fence mode never advertises a pending manual queue"
);
var acceptedManual = System.Text.Json.JsonSerializer.SerializeToElement(RaidRoulette.Testing.TestingControl.Update(
        new() { Action = "apply", Event = "gun_game", EventsEnabled = true, TraderEventActivation = false, QueueId =
             "manual-mode-accepted" },
        session
    ));
Check(acceptedManual.GetProperty("ok").GetBoolean(), "Disabling Fence mode permits the manual queue");
Check(
    RaidRoulette.Testing.TestingControl.Take() == "gun_game",
    "Accepted manual Gun Game reaches the next raid selector"
);
var nativeHelper = (SPTarkov.Server.Core.Helpers.Quest.QuestHelper)RuntimeHelpers
    .GetUninitializedObject(typeof(SPTarkov.Server.Core.Helpers.Quest.QuestHelper));
var timeField = typeof(SPTarkov.Server.Core.Helpers.Quest.QuestHelper).GetFields(BindingFlags
    .Instance | BindingFlags.NonPublic | BindingFlags.Public).Single(field => field.FieldType == typeof(TimeUtil));
timeField.SetValue(nativeHelper, RuntimeHelpers.GetUninitializedObject(typeof(TimeUtil)));
runtime.GetMethod("InitializeRetries", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [nativeHelper]);
var reoffer = runtime.GetMethod("Reoffer", BindingFlags.Static | BindingFlags.NonPublic)!;
var activeStatus = runtime.GetMethod("ActiveStatus", BindingFlags.Static | BindingFlags.NonPublic)!;
var markerQuest = runtime.GetMethod("AcceptedMarkerQuest", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var status in new[]
{
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForFinish
}

)
{
    profile.Quests[0].Status = status;
    Check((bool)activeStatus.Invoke(null, [status])!, "Raid-end finalization accepts native active status " + status);
    Check(
        (string? )markerQuest.Invoke(null, [profile]) == firstId,
        "Map markers select only the accepted assignment at status " + status
    );
}

profile.Quests[0].Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart;
Check(
    markerQuest.Invoke(null, [profile]) == null,
    "Available but unaccepted Fence assignments do not populate map badges"
);
profile.Quests[0].Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success;
Check(markerQuest.Invoke(null, [profile]) == null, "Finished Fence assignments do not populate map badges");
profile.Quests[0].Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForFinish;
runtime.GetMethod("ReconcileCompletion", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [profile,
     session, profile.Quests[0].QId]);
Check(
    profile.Quests[0].Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart,
    "Stale completion reconciles to a fresh offer without granting completion"
);
foreach (var outcome in new[]
{
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Fail
}

)
{
    profile.Quests[0].Status = outcome;
    profile.Quests[0].CompletedConditions = new()
    {
        "old-progress"
    };
    reoffer.Invoke(null, [profile, profile.Quests[0].QId]);
    Check(
        profile.Quests[0].Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart,
        "Native reset re-offers " + outcome + " without accepting it automatically"
    );
    Check(
        profile.Quests[0].CompletedConditions?.Count == 0,
        "Native reset clears prior objective progress after " + outcome
    );
}

var replaceQueued = runtime.GetMethod("ReplaceQueuedAssignment", BindingFlags.Static | BindingFlags.NonPublic)!;
var unrelated = new QuestStatus
{
    QId = new MongoId(),
    StartTime = 1,
    Status = SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    StatusTimers = new()
};
profile.Quests.Add(unrelated);
foreach (var status in new[]
{
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForFinish
}

)
{
    profile.Quests[0].Status = status;
    var replaced = (QuestStatus[])replaceQueued.Invoke(null, [profile, new MongoId(), secondId])!;
    Check(replaced.Length == 1, "Replacing a queued ticket selects only the other active RR assignment: " + status);
    Check(
        profile.Quests[0].Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart,
        "Replacing a queued ticket re-offers the old assignment: " + status
    );
}

Check(
    unrelated.Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    "Switching Fence events leaves unrelated quests untouched"
);
bool runningSwitchRejected = false;
try
{
    replaceQueued.Invoke(null, [profile, session, secondId]);
}
catch (TargetInvocationException exception)when (exception.InnerException is InvalidOperationException)
{
    runningSwitchRejected = true;
}

Check(runningSwitchRejected, "An active assigned raid cannot be replaced by another event");
var resetSession = runtime.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var status in new[]
{
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForFinish,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Success,
    SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Fail
}

)
{
    profile.Quests[0].Status = status;
    profile.Quests[0].StartTime = 123;
    profile.Quests[0].CompletedConditions = new()
    {
        "previous-raid"
    };
    resetSession.Invoke(null, [profile, session]);
    Check(
        profile.Quests[0].Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.AvailableForStart && profile
            .Quests[0].StartTime == 0 && profile.Quests[0].CompletedConditions?.Count == 0,
        "Session cleanup restores a new unaccepted RR offer from " + status
    );
}

Check(
    !(bool)runtime.GetMethod("RaidRunning", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [session])!,
    "Session cleanup clears the previous assigned raid"
);
Check(
    unrelated.Status == SPTarkov.Server.Core.Models.Enums.QuestStatusEnum.Started,
    "Startup and raid-end cleanup preserve non-RR story quests"
);
var isolation = typeof(EventRegistry).Assembly.GetType("RaidRoulette.Runtime.SeasonalRaidIsolation", true)!;
var nativeBoss = new SPTarkov.Server.Core.Models.Eft.Common.BossLocationSpawn
{
    BossName = "bossTagilla"
};
var seasonalBoss = new SPTarkov.Server.Core.Models.Eft.Common.BossLocationSpawn
{
    BossName = "bossTagilla"
};
var map = new SPTarkov.Server.Core.Models.Eft.Common.LocationBase
{
    Id = "laboratory",
    BossLocationSpawn = new()
    {
        nativeBoss,
        seasonalBoss
    },
    Events = new()
    {
        Halloween2024 = new()
        {
            InfectionPercentage = 100
        },
        ExtensionData = new()
        {
            ["unrelated"] = true
        }
    }
};
var mapTable = (LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
typeof(LocationTable).GetProperty("Laboratory")!.SetValue(mapTable, new SPTarkov.Server.Core.Models.Eft.Common.Location { Base = map });
var seasonalSettings = (SeasonalEventConfig)RuntimeHelpers.GetUninitializedObject(typeof(SeasonalEventConfig));
seasonalSettings.EventBossSpawns = new()
{
    ["halloween"] = new()
    {
        ["laboratory"] = new()
        {
            seasonalBoss
        }
    }
};
isolation.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [mapTable, seasonalSettings]);
var rollbacks = new List<Action>();
var isolate = isolation.GetMethod("Begin", BindingFlags.Static | BindingFlags.NonPublic)!;
var originalBosses = map.BossLocationSpawn;
var originalEvents = map.Events;
isolate.Invoke(null, [session, "normal", "laboratory", (Action<Action>)rollbacks.Add]);
Check(
    rollbacks.Count == 0 && ReferenceEquals(map.BossLocationSpawn, originalBosses),
    "Normal raids retain seasonal schedules without shared edits"
);
isolate.Invoke(null, [session, "lab_work_day", "laboratory", (Action<Action>)rollbacks.Add]);
Check(
    map.BossLocationSpawn.Count == 1 && ReferenceEquals(map.BossLocationSpawn[0], nativeBoss),
    "RR removes the seasonal Tagilla entry while preserving an ordinary entry with the same role"
);
Check(
    map.Events!.Halloween2024 == null && map.Events.ExtensionData!.ContainsKey("unrelated"),
    "RR disables native Halloween infection without deleting unrelated location settings"
);
var rrCultist = new SPTarkov.Server.Core.Models.Eft.Common.BossLocationSpawn
{
    BossName = "sectantPriest"
};
map.BossLocationSpawn.Add(rrCultist);
Check(map.BossLocationSpawn.Contains(rrCultist), "Intentional RR actors can be added after seasonal isolation");
foreach (var rollback in rollbacks.AsEnumerable().Reverse())
{
    rollback();
}

Check(
    ReferenceEquals(map.BossLocationSpawn, originalBosses) && ReferenceEquals(map.Events, originalEvents),
    "Generation cleanup restores exact shared schedules and event objects"
);
Check(
    map.Events.Halloween2024!.InfectionPercentage == 100,
    "Next normal raid retains its original seasonal percentage"
);
var harmony = new HarmonyLib.Harmony("raidroulette.fence.native-checks");
try
{
    foreach (string name in new[]
    {
        "FenceQuestAccept",
        "FenceQuestComplete",
        "FenceRaidEnd",
        "FenceRaidSide",
        "FenceClientSessionStart"
    }

    )
    {
        var patch = typeof(EventRegistry).Assembly.GetType("RaidRoulette.Quests." + name, true)!;
        var methods = harmony.CreateClassProcessor(patch).Patch();
        Check(methods.Count > 0, name + " binds the installed native SPT method");
    }
}
finally
{
    harmony.UnpatchSelf();
}

if (args.Length > 0)
{
    var imageRoutes = new Dictionary<string, string>();
    var images = typeof(EventRegistry).Assembly.GetType("RaidRoulette.Quests.FenceImages", true)!;
    images.GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [args[0],
         new Action<string, string>((key, path) => imageRoutes.Add(key, path))]);
    Check(imageRoutes.Count == ids.Count, "Every Fence event registers one bundled image");
    foreach (var route in imageRoutes)
    {
        Check(
            File.Exists(route.Value) && !route.Key.EndsWith(".png"),
            "Native extensionless image route resolves: " + route.Key
        );
    }
}

var offers = (IEnumerable<(RaidRoulette.Models.RaidEvent Event, string AssignmentId, string Name)>)runtime
    .GetMethod("Offers", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
var offerList = offers.ToArray();
Check(
    offerList.Count(offer => offer.AssignmentId == "gun_game_respawns") == 1,
    "Fence contains exactly one dedicated Gun Game respawn offer"
);
Check(
    offerList.Single(offer => offer.AssignmentId == "gun_game_respawns").Event.Id == "gun_game",
    "Respawn ticket uses the existing Gun Game gameplay pipeline"
);
var briefings = typeof(EventRegistry).Assembly.GetType("RaidRoulette.Quests.FenceBriefings", true)!;
var getBriefing = briefings.GetMethod("Get", BindingFlags.NonPublic | BindingFlags.Static)!;
foreach (var offer in offerList)
{
    string text = (string)getBriefing.Invoke(null, [offer.AssignmentId])!;
    Check(
        text.Length > 20 && text.Length < 500 && !text.Contains("matching client", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("eligible location", StringComparison.OrdinalIgnoreCase) && !text
            .Contains("replaces the previous", StringComparison.OrdinalIgnoreCase),
        offer.AssignmentId + " has a concise player-facing briefing"
    );
}

var respawnDefinition = (JsonObject)definition.Invoke(null, ["gun_game_respawns", "[RR: Gun Game [Respawns]]"])!;
var normalDefinition = (JsonObject)definition.Invoke(null, ["gun_game", "[RR: Gun Game]"])!;
string respawnId = respawnDefinition["_id"]!.GetValue<string>();
Check(
    respawnId != normalDefinition["_id"]!.GetValue<string>(),
    "Respawn variant adds a distinct quest ID without replacing regular Gun Game"
);
Check(
    respawnDefinition["image"]!.GetValue<string>().EndsWith("/gun_game.png"),
    "Respawn variant reuses the Gun Game image route"
);
var runningAssignments = (System.Collections.Concurrent.ConcurrentDictionary<MongoId, string>)runtime
    .GetField("running", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
var respawnMode = runtime.GetMethod("GunGameRespawnMode", BindingFlags.Static | BindingFlags.NonPublic)!;
MongoId variantSession = new();
Check(respawnMode.Invoke(null, [variantSession]) == null, "Non-Fence raids retain their saved respawn preference");
eventLookup[respawnId] = "gun_game";
runningAssignments[variantSession] = respawnId;
Check((bool)respawnMode.Invoke(null, [variantSession])!, "Respawn ticket enables this raid's respawn mode");
runningAssignments[variantSession] = normalDefinition["_id"]!.GetValue<string>();
Check(!(bool)respawnMode.Invoke(null, [variantSession])!, "Regular Fence Gun Game disables respawn for this raid");
runningAssignments.TryRemove(variantSession, out _);
eventLookup.Remove(respawnId);
Check(respawnMode.Invoke(null, [variantSession]) == null, "Removing the receipt removes the respawn override");
foreach (string trial in new[]
{
    "trials",
    "guard_trials",
    "extreme_trials",
    "snowball",
    "total_trial"
}

)
{
    var durationMap = new SPTarkov.Server.Core.Models.Eft.Common.LocationBase
    {
        Id = "duration-test",
        EscapeTimeLimit = 40,
        EscapeTimeLimitPVE = 35,
        EscapeTimeLimitCoop = 30
    };
    RaidRoulette.Services.TrialsRaidDuration.Apply(durationMap, trial);
    Check(
        durationMap.EscapeTimeLimit == 80 && durationMap.EscapeTimeLimitPVE == 70 && durationMap.EscapeTimeLimitCoop == 60,
        trial + " doubles native raid durations"
    );
    RaidRoulette.Services.TrialsRaidDuration.Apply(durationMap, trial);
    Check(
        durationMap.EscapeTimeLimit == 80 && durationMap.EscapeTimeLimitPVE == 70,
        trial + " cannot multiply duration twice on the same raid copy"
    );
}

var normalDuration = new SPTarkov.Server.Core.Models.Eft.Common.LocationBase
{
    Id = "normal-duration-test",
    EscapeTimeLimit = 50
};
RaidRoulette.Services.TrialsRaidDuration.Apply(normalDuration, "normal");
Check(
    normalDuration.EscapeTimeLimit == 50 && normalDuration.EscapeTimeLimitPVE == null,
    "Normal raid timer and unspecified duration fields remain unchanged"
);
var separateDuration = new SPTarkov.Server.Core.Models.Eft.Common.LocationBase
{
    Id = "separate-duration-test",
    EscapeTimeLimit = 50
};
RaidRoulette.Services.TrialsRaidDuration.Apply(separateDuration, "trials");
Check(
    separateDuration.EscapeTimeLimit == 100 && normalDuration.EscapeTimeLimit == 50,
    "Trials duration does not leak to another location object"
);
Console.WriteLine($"{checks} Fence checks passed.");
