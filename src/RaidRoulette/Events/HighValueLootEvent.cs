using RaidRoulette.Models;

namespace RaidRoulette.Events;

public sealed class HighValueLootEvent : RaidEvent
{
    public HighValueLootEvent()
        : base(
            id: "high_value_loot",
            name: "High-Value Loot",
            description: "More high-value loot opportunities are active.",
            weight: 20)
    {
    }

    public override void Apply(RaidContext context)
    {
        var multiplier = context.Config.HighValueLootMultiplier;
        context.LootService.ApplyLooseLootMultiplierForRaid(
            context.LocationId,
            multiplier,
            context.RegisterRollback);

        context.Report.AddDetail($"High-value loose-loot multiplier = x{multiplier:0.##}");
        context.Report.MarkApplied();
    }
}
