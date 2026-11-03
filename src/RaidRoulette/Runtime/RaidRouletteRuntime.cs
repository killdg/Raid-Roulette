using RaidRoulette.Configuration;
using RaidRoulette.Core;
using RaidRoulette.Models;
using RaidRoulette.Services;

namespace RaidRoulette.Runtime
{
    internal static class RaidRouletteRuntime
    {
        private static readonly object SyncRoot = new();
        private static Action<string>? logInfo;
        private static Action<string>? logWarning;
        private static RaidRouletteConfig? config;
        private static EventManager? eventManager;
        private static LootService? lootService;
        private static BossSpawnService? bossSpawnService;
        public static void Initialize(RaidRouletteConfig loadedConfig, LootService loadedLootService,
             BossSpawnService loadedBossSpawnService, Action<string> infoLogger, Action<string> warningLogger)
        {
            lock (SyncRoot)
            {
                config = loadedConfig;
                lootService = loadedLootService;
                bossSpawnService = loadedBossSpawnService;
                eventManager = new EventManager(loadedConfig);
                logInfo = infoLogger;
                logWarning = warningLogger;
            }
        }

        public static RaidPatchState BeginRaid(string? locationId, SPTarkov.Server.Core.Models.Common.MongoId? sessionId = null)
        {
            lock (SyncRoot)
            {
                if (config is null || eventManager is null || lootService is null || bossSpawnService is null)
                {
                    return RaidPatchState.Empty;
                }

                var forced = Testing.TestingControl.Take(out var allowDisabled);
                var campaignEvent = sessionId.HasValue ? EventIntegration.Select(sessionId.Value, locationId) : null;
                forced = RaidSelectionPolicy.Resolve(Testing.TestingControl.EventsEnabled, forced, config.Debug?.ForceEvent, campaignEvent);
                if (sessionId.HasValue && Quests.FenceEvents.Enabled(sessionId.Value))
                {
                    // Fence mode owns selection; unsupported maps leave the task queued.
                    forced = Quests.FenceEvents.Select(sessionId.Value, locationId) ?? "normal";
                    allowDisabled = true;
                }

                var selection = eventManager.SelectEvent(e => RaidRulesRuntime.CanRun(e.Id, locationId), forced, allowDisabled);
                var selectedEvent = selection.Event;
                Testing.TestingControl.Record(selectedEvent.Id, locationId ?? "unknown", selection.Warning);
                var resolvedLocationId = locationId ?? "unknown";
                if (!string.IsNullOrWhiteSpace(selection.Warning))
                {
                    logWarning?.Invoke($"[Raid Roulette] {selection.Warning}");
                }

                var state = new RaidPatchState(selectedEvent, resolvedLocationId);
                var report = new EventReport(selectedEvent.Id, selectedEvent.Name, resolvedLocationId);
                var loggingLevel = GetLoggingLevel(config);
                if (loggingLevel == RaidLoggingLevel.Debug)
                {
                    logInfo?.Invoke($"[RR-CONTROL] standalone={Testing.TestingControl.EventsEnabled} campaign={campaignEvent ?? "none"} " +
                        $"forced={forced ?? "none"} selected={selectedEvent.Id} map={resolvedLocationId}");
                }

                var context = new RaidContext
                {
                    LogWarning = message => logWarning?.Invoke(message),
                    LogDebug = message =>
                    {
                        if (loggingLevel == RaidLoggingLevel.Debug)
                        {
                            logInfo?.Invoke($"[Raid Roulette] {message}");
                        }
                    },
                    LootService = lootService,
                    BossSpawnService = bossSpawnService,
                    Config = config,
                    LocationId = resolvedLocationId,
                    Report = report,
                    RegisterRollback = state.RegisterRollback
                };
                state.AttachContext(context);
                SeasonalRaidIsolation.Begin(sessionId, selectedEvent.Id, resolvedLocationId, state.RegisterRollback);
                if (loggingLevel != RaidLoggingLevel.Off)
                {
                    logInfo?.Invoke($"[Raid Roulette] " + $"{GetFriendlyMapName(resolvedLocationId)}: " + $"{selectedEvent.Name}");
                }

                try
                {
                    ExecuteEvent(selectedEvent, context, report);
                }
                catch (Exception exception)
                {
                    report.MarkFailed(exception.Message);
                    logWarning?.Invoke($"[Raid Roulette] " + $"{selectedEvent.Name} failed: " + $"{exception.Message}");
                    state.Rollback();
                }

                if (loggingLevel == RaidLoggingLevel.Debug)
                {
                    PrintReport(report);
                }

                return state;
            }
        }

        public static void CompleteRaid(RaidPatchState? state)
        {
            if (state is null || state.IsEmpty)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (state.SelectedEvent is not null && state.Context is not null)
                {
                    try
                    {
                        state.SelectedEvent.Cleanup(state.Context);
                    }
                    catch (Exception exception)
                    {
                        logWarning?.Invoke($"[Raid Roulette] " + $"{state.SelectedEvent.Name} cleanup warning: " + $"{exception.Message}");
                    }
                }

                state.Rollback();
            }
        }

        private static void ExecuteEvent(RaidEvent raidEvent, RaidContext context, EventReport report)
        {
            raidEvent.Initialize(context);
            raidEvent.Apply(context);
            raidEvent.Verify(context);
            if (report.Status == EventReportStatus.Pending)
            {
                report.MarkApplied();
            }
        }

        private static void PrintReport(EventReport report)
        {
            logInfo?.Invoke($"[Raid Roulette] Debug: Event ID = {report.EventId}");
            logInfo?.Invoke($"[Raid Roulette] Debug: Location ID = {report.LocationId}");
            foreach (var detail in report.Details)
            {
                logInfo?.Invoke($"[Raid Roulette] Debug: {detail}");
            }

            foreach (var verification in report.Verification)
            {
                logInfo?.Invoke($"[Raid Roulette] Debug: Verify: {verification}");
            }

            var status = report.Status.ToString();
            if (string.IsNullOrWhiteSpace(report.StatusMessage))
            {
                logInfo?.Invoke($"[Raid Roulette] Debug: Status = {status}");
            }
            else
            {
                logInfo?.Invoke($"[Raid Roulette] Debug: Status = " + $"{status} ({report.StatusMessage})");
            }
        }

        private static RaidLoggingLevel GetLoggingLevel(RaidRouletteConfig currentConfig)
        {
            return currentConfig.Logging.Level?.Trim().ToLowerInvariant() switch
            {
                "off" => RaidLoggingLevel.Off,
                "debug" => RaidLoggingLevel.Debug,
                _ => RaidLoggingLevel.Minimal
            };
        }

        private static string GetFriendlyMapName(string locationId)
        {
            var normalized = new string (locationId.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            return normalized switch
            {
                "bigmap" or "customs" => "Customs",
                "factory4day" or "factoryday" => "Factory Day",
                "factory4night" or "factorynight" => "Factory Night",
                "interchange" => "Interchange",
                "laboratory" or "lab" or "labs" => "Labs",
                "labyrinth" => "Labyrinth",
                "lighthouse" => "Lighthouse",
                "rezervbase" or "reserve" => "Reserve",
                "sandbox" or "groundzero" => "Ground Zero",
                "sandboxhigh" or "groundzerohigh" => "Ground Zero High Level",
                "shoreline" => "Shoreline",
                "tarkovstreets" or "streets" or "streetsoftarkov" => "Streets of Tarkov",
                "woods" => "Woods",
                _ => locationId
            };
        }
    }

    internal enum RaidLoggingLevel
    {
        Off,
        Minimal,
        Debug
    }

    internal sealed class RaidPatchState
    {
        private readonly List<Action> rollbackActions = [];
        private bool rolledBack;
        private RaidPatchState()
        {
            IsEmpty = true;
            LocationId = "unknown";
        }

        public RaidPatchState(RaidEvent selectedEvent, string locationId)
        {
            SelectedEvent = selectedEvent;
            LocationId = locationId;
        }

        public static RaidPatchState Empty { get; } = new();
        public bool IsEmpty { get; }
        public RaidEvent? SelectedEvent { get; }
        public RaidContext? Context { get; private set; }
        public string LocationId { get; }

        public void AttachContext(RaidContext context)
        {
            Context = context;
        }

        public void RegisterRollback(Action rollbackAction)
        {
            if (!IsEmpty)
            {
                rollbackActions.Add(rollbackAction);
            }
        }

        public void Rollback()
        {
            if (rolledBack)
            {
                return;
            }

            rolledBack = true;
            for (var index = rollbackActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    rollbackActions[index]();
                }
                catch
                {
                // Cleanup must never interrupt raid startup.
                }
            }

            rollbackActions.Clear();
        }
    }
}
