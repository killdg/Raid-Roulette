using System.Collections.Concurrent;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace RaidRoulette.Runtime
{
    /// <summary>
    /// Removes SPT's seasonal additions only while generating a special RR raid.
    /// StartLocalRaidPatch's generation lock and rollback finalizer bound every
    /// shared-table edit; normal raids and global seasonal configuration stay intact.
    /// </summary>
    internal static class SeasonalRaidIsolation
    {
        private static LocationTable? locations;
        private static readonly HashSet<BossLocationSpawn> seasonalSpawns = new(ReferenceEqualityComparer.Instance);
        private static readonly ConcurrentDictionary<MongoId, bool> active = new();
        internal static void Initialize(LocationTable table, SeasonalEventConfig settings)
        {
            locations = table;
            seasonalSpawns.Clear();
            // SeasonalEventService.AddEventBossesToMaps inserts these same config
            // objects into map schedules. Reference identity distinguishes a
            // seasonal Tagilla from Factory's ordinary Tagilla entry.
            foreach (var eventMaps in settings.EventBossSpawns.Values)
            {
                foreach (var entries in eventMaps.Values)
                {
                    seasonalSpawns.UnionWith(entries);
                }
            }
        }

        internal static bool Active(MongoId session) => active.GetValueOrDefault(session);
        internal static void Clear(MongoId session)
        {
            active.TryRemove(session, out _);
        }

        internal static void Begin(MongoId? session, string eventId, string mapId, Action<Action> rollback)
        {
            bool isolate = eventId != "normal";
            if (session.HasValue)
            {
                active[session.Value] = isolate;
            }

            if (!isolate || locations == null)
            {
                return;
            }

            var map = locations.GetLocation(mapId)?.Base;
            if (map == null)
            {
                return;
            }

            var bosses = map.BossLocationSpawn;
            var events = map.Events;
            rollback(() =>
            {
                map.BossLocationSpawn = bosses;
                map.Events = events;
            });
            map.BossLocationSpawn = (bosses ?? []).Where(spawn => !seasonalSpawns.Contains(spawn) && !SeasonalRole(spawn
                .BossName) && !string.Equals(spawn.TriggerName, "botEvent", StringComparison.OrdinalIgnoreCase)).ToList();
            if (events != null)
            {
                // A fresh container avoids changing the globally shared Halloween
                // infection percentage or Christmas Khorovod object in place.
                map.Events = new LocationEvents
                {
                    Halloween2024 = null,
                    Khorovod = null,
                    ExtensionData = events.ExtensionData == null ? null : new(events.ExtensionData)
                };
            }
        }

        internal static bool SeasonalRole(string? role)
        {
            return role?.StartsWith("infected", StringComparison.OrdinalIgnoreCase) == true || role?.StartsWith("spirit",
                 StringComparison.OrdinalIgnoreCase) == true
                || role is "gifter" or "peacefullZryachiyEvent" or "ravangeZryachiyEvent" or "crazyAssaultEvent";
        }
    }
}
