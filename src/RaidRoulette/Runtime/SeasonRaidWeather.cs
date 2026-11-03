using HarmonyLib;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators.Weather;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Weather;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Weather;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace RaidRoulette.Runtime
{
    /// <summary>
    /// Changes only /client/localGame/weather for the selected session. The menu's
    /// /client/weather response, calendar override and shared forecast stay untouched.
    /// RaidRulesRuntime clears the selection at raid end and before the next raid.
    /// </summary>
    internal static class SeasonRaidWeather
    {
        private static WeatherGenerator? generator;
        internal static void Initialize(WeatherGenerator weatherGenerator)
        {
            generator = weatherGenerator;
        }

        internal static Season? Resolve(string? eventId)
        {
            return eventId switch
            {
                "spring" => Season.SPRING,
                "summer" => Season.SUMMER,
                "autumn" => Season.AUTUMN,
                "winter" => Season.WINTER,
                _ => null
            };
        }

        internal static bool IsSeasonEvent(string? eventId)
        {
            return Resolve(eventId).HasValue;
        }

        internal static bool AllowsMap(string? location)
        {
            return location?.ToLowerInvariant()is "bigmap" or "woods" or "shoreline" or "lighthouse" or "rezervbase"
                or "interchange" or "tarkovstreets" or "sandbox" or "sandbox_high" or "factory4_day" or "factory4_night";
        }

        internal static GetLocalWeatherResponseData Apply(GetLocalWeatherResponseData original, string? eventId)
        {
            var season = Resolve(eventId);
            if (!season.HasValue || generator == null)
            {
                return original;
            }

            // Copy the seasonal weights because GenerateWeather updates the previous
            // preset's weight. Never let one raid modify the global forecast/config.
            var weights = new Dictionary<WeatherPreset, double>(generator.GetWeatherPresetWeightsBySeason(season.Value));
            var weather = new List<Weather>();
            WeatherPreset? previous = null;
            foreach (var node in original.Weather ?? [])
            {
                var generated = generator.GenerateWeather(season.Value, ref weights, node.Timestamp, previous);
                weather.Add(generated);
                previous = generated.SptChosenPreset;
            }

            return new GetLocalWeatherResponseData
            {
                Season = season.Value,
                Weather = weather
            };
        }
    }

    [HarmonyPatch(typeof(WeatherController), nameof(WeatherController.GenerateLocal))]
    internal static class SeasonRaidWeatherPatch
    {
        private static void Postfix(MongoId sessionId, ref GetLocalWeatherResponseData __result)
        {
            __result = SeasonRaidWeather.Apply(__result, RaidRulesRuntime.Get(sessionId)?.EventId);
        }
    }
}
