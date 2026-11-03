namespace RaidRoulette.Events
{
    public sealed class GuardedEvent() : ServerRuleEvent(
        "guarded",
        "Guarded",
        "Replace combat spawns with conventional boss guards using their native equipment and AI. "
            + "Keep the map's normal population limit; remove ordinary Scavs, PMCs, sniper and boss schedules."
    );
}
