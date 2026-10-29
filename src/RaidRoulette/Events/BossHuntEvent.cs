using RaidRoulette.Models;
using RaidRoulette.Services;

namespace RaidRoulette.Events;

public sealed class BossHuntEvent : RaidEvent
{
    private BossHuntResult? result;

    public BossHuntEvent()
        : base(
            id: "boss_hunt",
            name: "Boss Hunt",
            description: "One random mobile boss receives a 100% starting spawn request and relentlessly hunts the local player until either dies; requires the local client.",
            weight: 25)
    {
    }

    public override void Initialize(RaidContext context)
    {
        result = null;
        context.Report.AddDetail("Request one random mobile boss at 100% chance, without guards; local client controls player pursuit.");
    }

    public override void Apply(RaidContext context)
    {
        result = context.BossSpawnService.EnableBossHuntForRaid(
            context.LocationId,
            context.RegisterRollback);

        if (!result.Success)
        {
            var reason = result.FailureReason ?? "a native boss could not be selected";
            context.Report.MarkSkipped(reason);
            context.LogWarning($"[Raid Roulette] Boss Hunt skipped: {reason}.");
            return;
        }

        context.Report.AddDetail($"Selected boss = {result.BossName}");
        context.Report.AddDetail($"Boss zone = {result.Zone}");
        context.Report.AddDetail(
            $"Boss chance = {result.OriginalChance:0.##}% -> {result.AppliedChance:0.##}%");
        context.Report.MarkApplied();
    }

    public override void Verify(RaidContext context)
    {
        if (result is null || !result.Success)
        {
            return;
        }

        context.Report.AddVerification(
            result.AppliedChance >= 100d
                ? $"{result.BossName} was set to a 100% spawn request."
                : $"{result.BossName} did not reach the requested 100% spawn chance.");
    }
}
