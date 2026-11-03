using RaidRoulette.Core;
using RaidRoulette.Shared;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace RaidRoulette.Testing
{
    // A local solo testing override. Deliberately in memory; config.json is never rewritten.
    public static class TestingControl
    {
        private static readonly object Gate = new();
        private static string? selection;
        private static bool repeat;
        private static bool eventsEnabled = true;
        public static bool EventsEnabled
        {
            get
            {
                lock (Gate)
                {
                    return eventsEnabled;
                }
            }
        }

        private static RaidChanceProfile? chances;
        private static string? lastQueueId;
        public static RaidChanceProfile? Chances
        {
            get
            {
                lock (Gate)
                {
                    return chances?.Copy();
                }
            }
        }

        private static bool active;
        private static bool allowDisabled;
        private static string last = "No raid generated yet";
        private static int startupTarget;
        private static int startupCap;
        public static int PopulationCap
        {
            get
            {
                lock (Gate)
                {
                    return startupCap;
                }
            }
        }

        public static int PopulationTarget
        {
            get
            {
                lock (Gate)
                {
                    return startupTarget;
                }
            }
        }

        private static string huntBossRole = "";
        public static void SetHuntBoss(string role)
        {
            lock (Gate)
            {
                huntBossRole = role;
            }
        }

        private static string[] overwatchZones = [];
        private static Services.RooftopPoint[] overwatchPoints = [];
        private static int overwatchPerZone;
        private static int overwatchCap;
        public static void SetOverwatchPlan(Services.RooftopPoint[] points, int perZone, int cap)
        {
            lock (Gate)
            {
                overwatchPoints = points.ToArray();
                overwatchZones = points.Select(p => p.Zone).Distinct().ToArray();
                overwatchPerZone = Math.Clamp(perZone, 1, 10);
                overwatchCap = Math.Max(0, cap);
            }
        }

        public static void SetStartupPopulation(int cap, int target)
        {
            lock (Gate)
            {
                startupCap = Math.Max(0, cap);
                startupTarget = Math.Clamp(target, 0, startupCap);
            }
        }

        public static object Update(TestingRequest request, SPTarkov.Server.Core.Models.Common.MongoId? session = null)
        {
            lock (Gate)
            {
                if (request.Chances != null && !Runtime.EventIntegration.Ready)
                {
                    return new
                    {
                        ok = false,
                        message = "Enable Raid Roulette on the server before applying F12 chances."
                    };
                }

                bool proposedEnabled = request.EventsEnabled ?? eventsEnabled;
                if (session.HasValue && request.TraderEventActivation.HasValue)
                {
                    Quests.FenceEvents.Configure(session.Value, proposedEnabled && request.TraderEventActivation.Value);
                }

                bool fenceMode = session.HasValue && Quests.FenceEvents.Enabled(session.Value);
                bool queueRejected = proposedEnabled && fenceMode && request.Action == "apply";
                string? chanceError = null;
                bool validChances = request.Chances == null || RaidChances.Valid(request.Chances, EventRegistry.GetEvents()
                    .Select(e => e.Id), out chanceError);
                if (proposedEnabled && !validChances)
                {
                    return new
                    {
                        ok = false,
                        message = chanceError
                    };
                }

                if (request.Action == "apply" && request.Event != "random" && request.Event != "config_file" && !EventRegistry
                    .GetEvents().Any(e => e.Id == request.Event))
                {
                    return new
                    {
                        ok = false,
                        message = "Unknown event ID; selection unchanged."
                    };
                }

                if (request.Chances != null && validChances)
                {
                    chances = request.Chances.Copy();
                    active = true;
                    if (selection == null)
                    {
                        selection = "";
                    }
                }

                eventsEnabled = proposedEnabled;
                if (!eventsEnabled)
                {
                    selection = "";
                    repeat = true;
                    allowDisabled = false;
                }

                if (!queueRejected && eventsEnabled && request.Action == "apply" && (string.IsNullOrEmpty(request.QueueId)
                    || request.QueueId != lastQueueId))
                {
                    if (request.Event != "random" && request.Event != "config_file" && !EventRegistry.GetEvents().Any(e => e
                        .Id == request.Event))
                    {
                        return new
                        {
                            ok = false,
                            message = "Unknown event ID; selection unchanged."
                        };
                    }

                    if (request.Event == "config_file")
                    {
                        chances = null;
                    }

                    lastQueueId = request.QueueId;
                    active = request.Event != "config_file";
                    selection = request.Event == "random" ? "" : request.Event;
                    repeat = request.Repeat;
                    allowDisabled = request.AllowDisabled;
                }

                return new
                {
                    ok = !queueRejected,
                    message = queueRejected ? "Fence controls raid selection. Disable Trader Event Activation before queuing a manual event."
                        : fenceMode ? "Fence activation active: accept an eligible RR assignment; otherwise the raid is Normal."
                        : !eventsEnabled ? "Raid Roulette disabled; task packs remain active." : active
                        ? $"Queued: {(selection == "" ? "Random" : selection)} ({(repeat ? "repeat" : "next raid only")}). " +
                            $"Map-ineligible events fall back to Normal. Disabled events require the explicit testing override."
                        : "Using config.json selection.",
                    eventsEnabled,
                    queuePending = !fenceMode && eventsEnabled && active && !string.IsNullOrEmpty(selection),
                    fenceMarkers = session.HasValue ? Quests.FenceEvents.Markers(session.Value) : [],
                    lastRaid = last,
                    startupTarget,
                    startupCap,
                    overwatchZones,
                    overwatchPerZone,
                    overwatchCap,
                    overwatchPoints,
                    huntBossRole
                };
            }
        }

        public static string? Take() => Take(out _);
        public static string? Take(out bool permitDisabled)
        {
            lock (Gate)
            {
                permitDisabled = eventsEnabled && active && allowDisabled;
                if (!eventsEnabled)
                {
                    return "";
                }

                if (!active)
                {
                    return null;
                }

                var value = selection;
                // A one-shot returns to random afterwards, rather than resurrecting an old ForceEvent.
                if (!repeat)
                {
                    selection = "";
                    repeat = true;
                }

                return value;
            }
        }

        public static void Record(string id, string map, string? warning)
        {
            lock (Gate)
            {
                last = $"{map}: {id}" + (warning is null ? "" : $" — {warning}");
                startupCap = startupTarget = 0;
                overwatchZones = [];
                overwatchPoints = [];
                overwatchPerZone = overwatchCap = 0;
                huntBossRole = "";
            }
        }
    }

    public sealed class TestingRequest : IRequestData
    {
        [JsonPropertyName("traderEventActivation")]
        public bool? TraderEventActivation { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = "status";

        [JsonPropertyName("event")]
        public string Event { get; set; } = "random";

        [JsonPropertyName("repeat")]
        public bool Repeat { get; set; }

        [JsonPropertyName("queueId")]
        public string? QueueId { get; set; }

        [JsonPropertyName("eventsEnabled")]
        public bool? EventsEnabled { get; set; }

        [JsonPropertyName("chances")]
        public RaidChanceProfile? Chances { get; set; }

        [JsonPropertyName("allowDisabled")]
        public bool AllowDisabled { get; set; }
    }
}
