using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class HighValueLootEvent : RaidEvent
    {
        public HighValueLootEvent() : base(
            "high_value_loot",
            "Locked Loot",
            "One to three premium items in keyed rooms, using the local matching client.",
            20
        )
        {
        }

        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Local keyed-room detection replaces ordinary loot points with premium items; no global loose-loot multiplier.");
        }
    }
}
