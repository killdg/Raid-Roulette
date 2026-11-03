using System.Collections.Concurrent;
using RaidRoulette.Configuration;
using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace RaidRoulette.Runtime
{
    public sealed class RaidRuleSession(string eventId)
    {
        public string EventId { get; } = eventId;
        public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
        public ConcurrentDictionary<string, Customization> Clothing { get; } = new();
        public ConcurrentDictionary<string, byte> RunnerBitcoins { get; } = new();
        public ConcurrentDictionary<string, byte> Warnings { get; } = new();
    }

    public static class RaidRulesRuntime
    {
        private static readonly ConcurrentDictionary<MongoId, RaidRuleSession> Sessions = new();
        public static RaidRouletteConfig Config { get; private set; } = new();
        public static RaidRulesService? Service { get; private set; }

        public static void Initialize(RaidRouletteConfig config, RaidRulesService service)
        {
            config.V2 ??= new();
            config.V2.Events ??= new();
            config.V2.Events = new(config.V2.Events.Where(x => x.Value != null), StringComparer.OrdinalIgnoreCase);
            foreach (string id in new[]
            {
                "wrong_door",
                "infrastructure_decay",
                "winter",
                "spring",
                "summer",
                "autumn",
                "snowball",
                "trials",
                "extreme_trials",
                "guard_trials",
                "total_trial",
                "blue_day",
                "gun_game",
                "stormtide",
                "ak50",
                "grenadier",
                "lab_work_day"
            }

            )
            {
                config.V2.Events.TryAdd(id, new() { Weight = 0 });
            }

            config.V2.Events.TryAdd("schizophrenia", new() { Weight = 0 });
            config.V2.Events.TryAdd("smokers_lobby", new() { Weight = 0 });
            config.V2.Events.TryAdd("time_warp", new() { Weight = 0 });
            config.V2.MaxAliveBots = Math.Clamp(config.V2.MaxAliveBots, 1, 200);
            config.V2.MaxScheduledBots = Math.Clamp(config.V2.MaxScheduledBots, 1, 1000);
            config.V2.ConcentratedScavs = Math.Clamp(config.V2.ConcentratedScavs, 1, config.V2.MaxScheduledBots);
            config.V2.ExplosiveScavChancePercent = Math.Clamp(config.V2.ExplosiveScavChancePercent, 0, 100);
            config.V2.MinimumMagazineCapacity = Math.Clamp(config.V2.MinimumMagazineCapacity, 100, 200);
            Config = config;
            Service = service;
            Sessions.Clear();
        }

        public static bool IsV2(string id) =>
             id is "infrastructure_decay" or "wrong_door" or "winter" or "spring" or "summer" or "autumn" or "guard_trials"
                 or "total_trial" or "snowball" or "trials" or "extreme_trials" or "guarded" or "high_value_loot" or "blue_day"
                 or "raider_assault" or "lab_work_day" or "gun_game" or "stormtide" or "ak50" or "grenadier" or "schizophrenia"
                 or "time_warp" or "smokers_lobby" or "the_gift" or "clone_wars" or "scav_concentrate" or "pmc_sweep"
                 or "explosive_scav_units" or "outbreak" or "geared" or "get_handy" or "i_have_a_shot" or "marksman"
                 or "extreme_overwatch" or "overloaded" or "matrix_glitch" or "no_safe_space" or "posers_everywhere" or "fake_cult";
        public static bool CanRun(string id, string? map) => id == "wrong_door" ? SeasonRaidWeather.AllowsMap(map)
            || map == "laboratory" : SeasonRaidWeather.IsSeasonEvent(id) ? SeasonRaidWeather.AllowsMap(map)
            : id is "loot_surge" or "extreme_overwatch" ? false : id == "lab_work_day" ? map == "laboratory" && GiftRuntime
            .StaffAvailable : id == "gun_game" ? IsGunGameMap(map) && GunGameRuntime.Available(id) : id == "ak50"
            ? GunGameRuntime.Available(id) : id == "grenadier" ? GrenadierRuntime.Available : id == "overloaded"
            ? OverloadedRuntime.Available(Config.V2.MinimumMagazineCapacity) : id == "smokers_lobby" ? SmokersRuntime
            .Available : id == "the_gift" ? GiftRuntime.Available && (Service?.CanRun("scav_concentrate", map, Config.V2)
            ?? false) : !IsV2(id) || (Service?.CanRun(id, map, Config.V2) ?? false);
        // One eligibility rule is shared by random rolls, forced selections and Fence map badges.
        // Both Factory time variants and both Ground Zero level variants are supported.
        public static bool IsGunGameMap(string? map)
        {
            return map is "factory4_day" or "factory4_night" or "laboratory" or "sandbox" or "sandbox_high";
        }

        public static void Clear(MongoId sessionId)
        {
            Sessions.TryRemove(sessionId, out _);
            CloneWarsRuntime.Clear(sessionId);
        }

        public static void Activate(MongoId sessionId, string id)
        {
            Clear(sessionId);
            foreach (var pair in Sessions)
            {
                if (DateTimeOffset.UtcNow - pair.Value.Created > TimeSpan.FromHours(6))
                {
                    Clear(pair.Key);
                }
            }

            if (IsV2(id))
            {
                Sessions[sessionId] = new(id);
            }
        }

        public static RaidRuleSession? Get(MongoId sessionId)
        {
            if (!Sessions.TryGetValue(sessionId, out var state))
            {
                return null;
            }

            if (DateTimeOffset.UtcNow - state.Created <= TimeSpan.FromHours(6))
            {
                return state;
            }

            Clear(sessionId);
            return null;
        }

        public static void ApplyMap(LocationBase map, string id)
        {
            Service?.ApplyMap(map, id, Config.V2);
            if (id == "the_gift")
            {
                GiftRuntime.ConfigureMap(map);
            }

            if (id == "lab_work_day")
            {
                GiftRuntime.ConfigureLab(map);
            }
        }
    }
}
