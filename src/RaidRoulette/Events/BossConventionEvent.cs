using RaidRoulette.Models;
using RaidRoulette.Services;

namespace RaidRoulette.Events;

public sealed class BossConventionEvent : RaidEvent
{
    private BossConventionResult? result;

    public BossConventionEvent()
        : base(
            id: "boss_convention",
            name: "Boss Convention",
            description: "Multiple boss groups have entered the raid.",
            weight: 25)
    {
    }

    public override void Initialize(RaidContext context)
    {
        result = null;
        var settings = context.Config.BossConvention;
        context.Report.AddDetail($"Factory boss limit = {Math.Max(0, settings.Factory.BossCount)}");
        context.Report.AddDetail($"Factory random selection = {settings.Factory.RandomSelection}");
        context.Report.AddDetail($"Other maps spawn all enabled bosses = {settings.OtherMaps.SpawnAllEnabledBosses}");
        context.Report.AddDetail($"Other maps random selection = {settings.OtherMaps.RandomSelection}");
        context.Report.AddDetail($"Global guards included = {settings.GlobalSettings.IncludeGuards}");
        context.Report.AddDetail($"Global requested boss chance = {Math.Clamp(settings.GlobalSettings.SpawnChance, 0, 100)}%");
        context.Report.AddDetail($"Spread boss groups across zones = {settings.GlobalSettings.SpreadAcrossZones}");
    }

    public override void Apply(RaidContext context)
    {
        result = context.BossSpawnService.EnableBossConventionForRaid(
            context.LocationId,
            context.Config.BossConvention,
            context.RegisterRollback);

        if (!result.Success)
        {
            var reason = result.FailureReason ?? "no boss groups could be prepared";
            context.Report.MarkSkipped(reason);
            context.LogWarning($"[Raid Roulette] Boss Convention skipped: {reason}.");
            return;
        }

        context.Report.AddDetail($"Enabled boss groups available = {result.AvailableGroups}");
        context.Report.AddDetail($"Requested boss groups = {result.RequestedGroups}");
        context.Report.AddDetail($"Added or modified boss entries = {result.Entries.Count}");

        foreach (var entry in result.Entries)
        {
            var guardText = entry.GuardsIncluded
                ? $"Guards: {entry.EscortType} ({entry.EscortAmount})"
                : "Guards: disabled";

            context.Report.AddDetail(
                $"{entry.DisplayName} [{entry.BossName}] | Zone: {entry.Zone} | {guardText} | Chance: {entry.SpawnChance:0.##}%");
        }

        context.Report.MarkApplied();
    }

    public override void Verify(RaidContext context)
    {
        if (result is null || !result.Success)
        {
            return;
        }

        context.Report.AddVerification(
            $"{result.Entries.Count} of {result.RequestedGroups} requested boss entries were prepared.");
    }
}
