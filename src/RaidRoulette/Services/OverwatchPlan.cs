using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
namespace RaidRoulette.Services;
public sealed record RooftopPoint(string Zone,float X,float Y,float Z);
public static class OverwatchPlan
{
    public static RooftopPoint[] Points(LocationBase map)
    {
        var zones=Zones(map).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (map.SpawnPointParams??[]).Where(p=>p.Position!=null&&p.BotZoneName!=null&&zones.Contains(p.BotZoneName)&&p.Categories?.Contains("Bot")==true).Select(p=>new RooftopPoint(p.BotZoneName!,p.Position!.Value.X,p.Position.Value.Y,p.Position.Value.Z)).Distinct().ToArray();
    }
    public static string[] Zones(LocationBase map)
    {
        var declared=(map.Waves??[]).Where(w=>w.WildSpawnType==WildSpawnType.marksman).SelectMany(w=>(w.SpawnPoints??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
            .Concat((map.BossLocationSpawn??[]).Where(b=>b.BossName=="marksman").SelectMany(b=>(b.BossZone??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (map.SpawnPointParams??[]).Where(p=>p.Position!=null&&!string.IsNullOrWhiteSpace(p.BotZoneName)&&(declared.Contains(p.BotZoneName)||p.BotZoneName.Contains("snipe",StringComparison.OrdinalIgnoreCase))&&p.Categories?.Contains("Bot")==true).Select(p=>p.BotZoneName!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
