using System;
using EFT;
namespace RaidRoulette.TestingClient;
internal static class CultistZoneFix
{
    [ThreadStatic] private static BossLocationSpawn? current;
    public static BossLocationSpawn? Enter(BossLocationSpawn wave)
    {
        var previous=current;
        current=CultistGatePolicy.IsEventWave(Plugin.ConfirmedCultistRaid,wave.ForceSpawn,wave.IgnoreMaxBots,wave.BossName,wave.BossEscortType,wave.Time)?wave:null;
        return previous;
    }
    public static void Exit(BossLocationSpawn? previous)=>current=previous;
    public static bool TryUnblock(BotZonesLeaveController controller,BotZone zone,WildSpawnType role)
    {
        if(current==null || zone==null || !CultistGatePolicy.IsCultistRole(role.ToString()))return false;
        try
        {
            // Remove only these roles in the zone currently selected by this event wave.
            // This also prevents BotSpawned from immediately telling newly created warriors to leave.
            controller.UnBlockZoneFor(zone,WildSpawnType.sectantPriest);
            controller.UnBlockZoneFor(zone,WildSpawnType.sectantWarrior);
            NativeSpawnDiagnostics.Write("event-zone-unblocked",current,"roles=sectantPriest,sectantWarrior");
            return true;
        }
        catch(Exception e) {Plugin.Trace("[RR-SPAWN] zone-fix-error "+e.Message);return false;}
    }
}
