using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class InfrastructureDecayEvent() : RaidEvent(
        "infrastructure_decay",
        "Infrastructure Decay",
        "Unstable electricity brings brief flickers and blackouts during day and night raids, including Labs and Factory. "
            + "Electrical buttons and switches stop working until power returns.",
        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.AddDetail(Description);
            context.Report.MarkApplied("Raid-local power interruptions selected.");
        }
    }
}
