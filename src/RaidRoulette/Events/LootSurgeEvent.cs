using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class LootSurgeEvent : RaidEvent
    {
        public LootSurgeEvent() : base(id: "loot_surge", name: "Loot Surge", description
            : "Loose-loot generation has increased.", weight: 25)
        {
        }

        public override void Apply(RaidContext context)
        {
            var multiplier = context.Config.LootSurgeMultiplier;
            context.LootService.ApplyLooseLootMultiplierForRaid(context.LocationId, multiplier, context.RegisterRollback);
            context.Report.AddDetail($"Loose-loot multiplier = x{multiplier:0.##}");
            context.Report.MarkApplied();
        }
    }
}
