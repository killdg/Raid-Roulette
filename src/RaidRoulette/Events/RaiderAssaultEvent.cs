using RaidRoulette.Models;
using RaidRoulette.Services;

namespace RaidRoulette.Events;

public sealed class RaiderAssaultEvent : RaidEvent
{
    private SpawnGroupResult? result;

    public RaiderAssaultEvent()
        : base(
            id: "raider_assault",
            name: "Raider Assault",
            description: "A Raider invasion is spread across several map zones.",
            weight: 15)
    {
    }

    public override void Initialize(RaidContext context)
    {
        result = null;
        context.Report.AddDetail($"Requested Raider groups = {context.Config.RaiderGroups}");
    }

    public override void Apply(RaidContext context)
    {
        result = context.BossSpawnService.AddRaiderGroupsForRaid(
            context.LocationId,
            context.Config.RaiderGroups,
            context.RegisterRollback);

        if (result.IsDisabled)
        {
            context.Report.MarkSkipped($"Raider groups are disabled (configured value: {result.RequestedGroups}).");
            return;
        }

        if (!result.Success)
        {
            var reason = result.FailureReason ?? "spawn entries could not be created";
            context.Report.MarkSkipped(reason);
            context.LogWarning($"[Raid Roulette] Raider Assault skipped: {reason}.");
            return;
        }

        context.Report.AddDetail($"Added Raider spawn entries = {result.AddedGroups}");

        for (var index = 0; index < result.Entries.Count; index++)
        {
            var entry = result.Entries[index];
            context.Report.AddDetail(
                $"Raider invasion group {index + 1} -> Zone: {entry.Zone}, Bots: {entry.MinimumBots}-{entry.MaximumBots}, Chance: {entry.SpawnChance}%");
        }

        context.Report.AddDetail(
            $"Requested total Raider bots = {result.MinimumTotalBots}-{result.MaximumTotalBots}");
        context.Report.MarkApplied();
    }

    public override void Verify(RaidContext context)
    {
        if (result is null || !result.Success)
        {
            return;
        }

        context.Report.AddVerification(
            $"{result.AddedGroups} of {result.RequestedGroups} requested Raider spawn entries were added.");
    }
}
