using RaidRoulette.Models;
using RaidRoulette.Services;

namespace RaidRoulette.Events;

public sealed class CultistInvasionEvent : RaidEvent
{
    private SpawnGroupResult? result;

    public CultistInvasionEvent()
        : base(
            id: "cultist_invasion",
            name: "Cultist Invasion",
            description: "Cultist groups are active in this raid.",
            weight: 15)
    {
    }

    public override void Initialize(RaidContext context)
    {
        result = null;
        context.Report.AddDetail($"Requested Cultist groups = {context.Config.CultistGroups}");
    }

    public override void Apply(RaidContext context)
    {
        result = context.BossSpawnService.AddCultistGroupsForRaid(
            context.LocationId,
            context.Config.CultistGroups,
            context.RegisterRollback);

        if (result.IsDisabled)
        {
            context.Report.MarkSkipped($"Cultist groups are disabled (configured value: {result.RequestedGroups}).");
            return;
        }

        if (!result.Success)
        {
            var reason = result.FailureReason ?? "spawn entries could not be created";
            context.Report.MarkSkipped(reason);
            context.LogWarning($"[Raid Roulette] Cultist Invasion skipped: {reason}.");
            return;
        }

        context.Report.AddDetail($"Added Cultist spawn entries = {result.AddedGroups}");

        for (var index = 0; index < result.Entries.Count; index++)
        {
            var entry = result.Entries[index];
            context.Report.AddDetail(
                $"Cultist group {index + 1} -> Zone: {entry.Zone}, Bots: {entry.MinimumBots}-{entry.MaximumBots}, Chance: {entry.SpawnChance}%");
        }

        context.Report.AddDetail(
            $"Requested total Cultist bots = {result.MinimumTotalBots}-{result.MaximumTotalBots}");
        context.Report.MarkApplied();
    }

    public override void Verify(RaidContext context)
    {
        if (result is null || !result.Success)
        {
            return;
        }

        context.Report.AddVerification(
            $"{result.AddedGroups} of {result.RequestedGroups} requested Cultist spawn entries were added.");
    }
}
