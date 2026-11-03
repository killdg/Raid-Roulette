using System;
using System.Collections.Generic;
using System.Linq;

namespace RaidRoulette.Shared
{
    public enum RaidChanceMode
    {
        OverallChance,
        IndividualPercentages
    }

    public sealed class RaidChanceProfile
    {
        public string Mode { get; set; } = "OverallChance";
        public double EventChance { get; set; } = 40;
        public Dictionary<string, double> Percentages { get; set; } = new();

        public RaidChanceProfile Copy() => new()
        {
            Mode = Mode,
            EventChance = EventChance,
            Percentages = new(Percentages)
        };
    }

    public static class RaidChances
    {
        public static bool Valid(RaidChanceProfile p, IEnumerable<string> known, out string error)
        {
            bool Number(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0 && v <= 100;
            error = "";
            if (p.Mode != "OverallChance" && p.Mode != "IndividualPercentages")
            {
                error = "Unknown chance mode.";
                return false;
            }

            var ids = new HashSet<string>(known);
            if (!Number(p.EventChance) || p.Percentages == null || p.Percentages.Any(x => !ids.Contains(x.Key) || !Number(x.Value)))
            {
                error = "Use known raid types and percentages from 0 to 100.";
                return false;
            }

            if (p.Mode == "IndividualPercentages" && p.Percentages.Values.Sum() > 100.00001)
            {
                error = "Individual percentages exceed 100%. Previous chances remain active.";
                return false;
            }

            return true;
        }

        public static Dictionary<string, double> Allocate(RaidChanceProfile p, IEnumerable<string> known, Func<string, bool> canRun)
        {
            var ids = known.Where(id => id != "normal").OrderBy(id => id, StringComparer.Ordinal).ToArray();
            double total = ids.Sum(id => p.Percentages.TryGetValue(id, out var n) ? n : 0);
            var result = new Dictionary<string, double>
            {
                {
                    "normal",
                    100
                }
            };
            foreach (var id in ids)
            {
                double configured = p.Percentages.TryGetValue(id, out var n) ? n : 0;
                double chance = p.Mode == "OverallChance" ? (total > 0 ? p.EventChance * configured / total : 0) : configured;
                result[id] = canRun(id) ? chance : 0;
                result["normal"] -= result[id];
            }

            result["normal"] = Math.Max(0, result["normal"]);
            return result;
        }

        public static string Pick(IReadOnlyDictionary<string, double> allocation, double roll)
        {
            double at = Math.Max(0, Math.Min(99.999999999, roll));
            foreach (var pair in allocation.Where(p => p.Key != "normal").OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (at < pair.Value)
                {
                    return pair.Key;
                }

                at -= pair.Value;
            }

            return "normal";
        }
    }

    // UI-independent event-mix calculation. The Normal share stays fixed while
    // weights determine how the remaining probability is divided among events.
    public static class EventMixPolicy
    {
        public static RaidChanceProfile Create(double normalPercent, IReadOnlyDictionary<string, double> weights,
             IReadOnlyDictionary<string, bool> enabled)
        {
            double normal = double.IsNaN(normalPercent) || double.IsInfinity(normalPercent) ? 100 : Math.Max(0, Math.Min(100,
                 normalPercent));
            var selected = weights.ToDictionary(
                pair => pair.Key,
                pair =>
            {
                bool active = pair.Key != "normal" && enabled.TryGetValue(pair.Key, out bool value) && value;
                return active && !double.IsNaN(pair.Value) && !double.IsInfinity(pair.Value) ? Math.Max(0, Math.Min(100, pair.Value)) : 0;
            }
            );
            return new RaidChanceProfile
            {
                Mode = "OverallChance",
                EventChance = selected.Values.Sum() > 0 ? 100 - normal : 0,
                Percentages = selected
            };
        }
    }
}
