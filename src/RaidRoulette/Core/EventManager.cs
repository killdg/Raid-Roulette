using RaidRoulette.Configuration;
using RaidRoulette.Models;
using RaidRoulette.Shared;

namespace RaidRoulette.Core;

public sealed class EventManager
{
    private readonly IReadOnlyList<RaidEvent> events;
    private readonly RaidRouletteConfig config;

    public EventManager(RaidRouletteConfig config)
    {
        this.config = config;
        events = EventRegistry.GetEvents();
    }

    public EventSelectionResult SelectEvent(Func<RaidEvent, bool>? canRun = null, string? forcedOverride = null, bool allowDisabledForced = false)
    {
        var forcedEventName = forcedOverride ?? config.Debug?.ForceEvent?.Trim();
        if (!string.IsNullOrWhiteSpace(forcedEventName))
        {
            var forcedEvent = FindEvent(forcedEventName);
            if (forcedEvent is not null && (IsEnabledByConfig(forcedEvent.Id) || allowDisabledForced) && (canRun?.Invoke(forcedEvent) ?? true))
            {
                return new EventSelectionResult(forcedEvent, null);
            }

            return new EventSelectionResult(
                events.First(x => x.Id == "normal"),
                $"Forced event '{forcedEventName}' was not found, disabled, or unavailable on this map; a Normal Raid was selected.");
        }

        return new EventSelectionResult(SelectWeightedEvent(canRun), null);
    }

    private RaidEvent SelectWeightedEvent(Func<RaidEvent, bool>? canRun)
    {
        var normalRaid = events.First(raidEvent => raidEvent.Id == "normal");
        var profile=Testing.TestingControl.Chances;
        if(profile!=null)
        {
            var allocation=RaidChances.Allocate(profile,events.Select(e=>e.Id),id=>canRun?.Invoke(events.Single(e=>e.Id==id))??true);
            var id=RaidChances.Pick(allocation,Random.Shared.NextDouble()*100);
            return events.Single(e=>e.Id==id);
        }
        var eventChance = Math.Clamp(config.EventChance, 0, 100);

        if (Random.Shared.Next(1, 101) > eventChance)
        {
            return normalRaid;
        }

        var weightedEvents = events
            .Where(raidEvent =>
                raidEvent.Enabled &&
                IsEnabledByConfig(raidEvent.Id) &&
                raidEvent.Id != "normal" && (canRun?.Invoke(raidEvent) ?? true))
            .Select(raidEvent => new WeightedRaidEvent(
                raidEvent,
                Math.Clamp(config.V2.Events.TryGetValue(raidEvent.Id, out var v2) ? v2.Weight : (config.Weights ?? new EventWeightConfig()).GetWeight(raidEvent.Id), 0, 1000000)))
            .Where(entry => entry.Weight > 0)
            .ToList();

        if (weightedEvents.Count == 0)
        {
            return normalRaid;
        }

        var totalWeight = weightedEvents.Sum(entry => entry.Weight);
        var roll = Random.Shared.Next(totalWeight);

        foreach (var entry in weightedEvents)
        {
            if (roll < entry.Weight)
            {
                return entry.Event;
            }

            roll -= entry.Weight;
        }

        return weightedEvents[^1].Event;
    }


    private bool IsEnabledByConfig(string eventId)
    {
        if (config.V2.Events.TryGetValue(eventId, out var rule)) return rule.Enabled;
        return eventId switch
        {
            "boss_convention" => config.BossConvention?.Enabled ?? true,
            "lab_work_day" => false,
            _ => true
        };
    }

    private RaidEvent? FindEvent(string value)
    {
        var normalized = Normalize(value);

        return events.FirstOrDefault(raidEvent =>
            Normalize(raidEvent.Id) == normalized ||
            Normalize(raidEvent.Name) == normalized ||
            Normalize(raidEvent.GetType().Name) == normalized ||
            Normalize(raidEvent.GetType().Name.Replace("Event", string.Empty)) == normalized);
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private sealed record WeightedRaidEvent(RaidEvent Event, int Weight);
}

public sealed record EventSelectionResult(
    RaidEvent Event,
    string? Warning);
