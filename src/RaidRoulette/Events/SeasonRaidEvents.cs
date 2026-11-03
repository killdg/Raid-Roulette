using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    /// <summary>
    /// Season-only assignments keep the map's ordinary population and player kit.
    /// Native season materials and weather are selected before the raid scene loads.
    /// </summary>
    public abstract class SeasonRaidEvent(string id, string name) : RaidEvent(
        id,
        name,
        $"Use native {name.ToLowerInvariant()} scenery and weather for one raid. All maps except Labs, " +
            $"including both Factory variants. Visible seasonal changes depend on the map's native assets. Normal " +
            $"AI and player equipment remain unchanged.",

        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.AddDetail(Description);
            context.Report.MarkApplied("The raid-local weather response will select the native season.");
        }
    }

    public sealed class SpringRaidEvent() : SeasonRaidEvent("spring", "Spring");
    public sealed class SummerRaidEvent() : SeasonRaidEvent("summer", "Summer");
    public sealed class AutumnRaidEvent() : SeasonRaidEvent("autumn", "Autumn");
    public sealed class WinterRaidEvent() : SeasonRaidEvent("winter", "Winter");
}
