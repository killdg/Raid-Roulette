using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public sealed class SnowballEvent() : RaidEvent(
        "snowball",
        "Snowball Trials",
        "Only the thirteen Snowball bosses spawn and hunt you. " + "Eliminate the entire roster without dying.",
        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Exclusive client-managed Snowball hunters requested.");
        }
    }

    public sealed class TrialsEvent() : RaidEvent(
        "trials",
        "Trials",
        "Ten five-bot hunting waves progress from level-10 Scavs "
            + "through PMC, Raider, Rogue, guard and cultist combat archetypes. "
                + "Every member must die before the next squad can spawn.",
        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Exclusive client-managed sequential Trials squads requested.");
        }
    }

    public sealed class GuardTrialsEvent() : RaidEvent(
        "guard_trials",
        "Guard Trials",
        "Twelve five-guard hunting waves cover each conventional guard variant and Zryachiy guards.",
        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Exclusive guard Trials squads requested.");
        }
    }

    public sealed class TotalTrialEvent() : RaidEvent("total_trial", "Total Trial",
         "The full twenty-wave progression: one hundred opponents, five at a time.", 0)
    {
        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Exclusive full twenty-wave Trials roster requested.");
        }
    }

    public sealed class ExtremeTrialsEvent() : RaidEvent(
        "extreme_trials",
        "Extreme Trials",
        "Ten exclusive hunting waves of five identical bosses, ending with "
            + "Labyrinth Tagilla and Labyrinth Killa. Every boss must die before the next wave.",
        0
    )
    {
        public override void Apply(RaidContext context)
        {
            context.Report.MarkApplied("Exclusive client-managed Extreme Trials squads requested.");
        }
    }
}
