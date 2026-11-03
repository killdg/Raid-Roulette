using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class RaiderAssaultEvent : RaidEvent
    {
        public RaiderAssaultEvent() : base(
            "raider_assault",
            "Raider Assault",
            "Replace combat spawns with native Raiders and their native appearance/loadouts.",
            15
        )
        {
        }

        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Raid-only schedules and bot generation use native Raider templates.");
        }
    }
}
