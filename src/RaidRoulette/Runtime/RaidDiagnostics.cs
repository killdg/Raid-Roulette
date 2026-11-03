using System.Collections.Concurrent;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace RaidRoulette.Runtime
{
    public static class RaidDiagnostics
    {
        private sealed class Audit(string id)
        {
            public string Event = id;
            public int Lines;
        }

        private static readonly ConcurrentDictionary<MongoId, Audit> Raids = new();
        private static Action<string>? logger;
        private static int unmapped;
        private static TemplateTable? templates;
        public static void Initialize(Action<string> log, TemplateTable table)
        {
            logger = log;
            templates = table;
            Raids.Clear();
            unmapped = 0;
        }

        public static bool Relevant(string id) =>
             id is "boss_hunt" or "extreme_overwatch" or "time_warp" or "smokers_lobby" or "cultist_invasion"
                 or "explosive_scav_units" or "scav_concentrate" or "overrun" or "boss_convention" or "fake_cult" or "clone_wars"
                     or "the_gift";
        public static void Clear(MongoId session) => Raids.TryRemove(session, out _);
        public static void Begin(MongoId session, string id, LocationBase map)
        {
            Clear(session);
            if (!Relevant(id))
            {
                return;
            }

            Raids[session] = new(id);
            try
            {
                Log(
                    session,
                    "map-result",
                    $"map={map.Id} botMax={map.BotMax} botMaxPve={map.BotMaxPvE} waves={map.Waves?.Count ?? 0}"
                );
                foreach (var wave in map.Waves ?? [])
                {
                    Log(session, "ordinary-wave", JsonSerializer.Serialize(wave));
                }

                var cultists = (map.BossLocationSpawn ?? []).ToArray();
                Log(session, "map-result", $"cultistEntries={cultists.Length}; these are schedules, NOT confirmed live bots");
                foreach (var spawn in cultists)
                {
                    Log(session, "cultist-entry", JsonSerializer.Serialize(spawn));
                }
            }
            catch (Exception e)
            {
                Log(session, "map-trace-error", e.Message);
            }
        }

        public static void Log(MongoId session, string stage, string text)
        {
            if (!Raids.TryGetValue(session, out var raid))
            {
                if (!Raids.IsEmpty && Interlocked.Increment(ref unmapped) <= 30)
                {
                    SafeLog($"[RR-DIAG] stage=unmapped-session session={session} originalStage={stage} {text}; activeMapSessions=[" +
                        $"{string.Join(",", Raids.Keys)}]. No session mapping was assumed.");
                }

                return;
            }

            int count = Interlocked.Increment(ref raid.Lines);
            if (count <= 600)
            {
                SafeLog($"[RR-DIAG] event={raid.Event} session={session} stage={stage} {text}");
            }
            else if (count == 601)
            {
                SafeLog($"[RR-DIAG] session={session} trace limit reached (600); further records suppressed.");
            }
        }

        public static void Decision(string id, string role, string text)
        {
            if (Relevant(id))
            {
                SafeLog($"[RR-DIAG] event={id} stage=loadout-decision role={role} {text}");
            }
        }

        private static void SafeLog(string text)
        {
            try
            {
                logger?.Invoke(text);
            }
            catch
            { /* Diagnostics never interrupt generation. */
            }
        }

        public static void Bot(MongoId session, string stage, BotBase? bot)
        {
            if (bot is null)
            {
                Log(session, stage, "null bot returned");
                return;
            }

            try
            {
                var guns = (bot.Inventory?.Items ?? []).Where(x => x
                    .SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster").Select(x =>
                     $"{x.SlotId}:{x.Template}:{templates?.Items.GetValueOrDefault(x.Template)?.Properties?.WeapClass ?? "unknown"}");
                Log(
                    session,
                    stage,
                    $"bot={bot.Id} role={bot.Info?.Settings?.Role} side={bot.Info?.Side} weapons=[{string.Join(",", guns)}]"
                );
            }
            catch (Exception e)
            {
                Log(session, "bot-trace-error", e.Message);
            }
        }
    }
}
