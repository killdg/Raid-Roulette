using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class NormalRaidEvent : RaidEvent
    {
        public NormalRaidEvent() : base(id: "normal", name: "Normal Raid", description: "No special event is active.", weight: 50)
        {
        }

        public override void Apply(RaidContext context)
        {
            context.Report.AddDetail("No temporary raid changes were requested.");
            context.Report.MarkApplied();
        }
    }
}
