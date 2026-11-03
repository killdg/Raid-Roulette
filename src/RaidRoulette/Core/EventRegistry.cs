using System.Reflection;
using RaidRoulette.Models;

namespace RaidRoulette.Core
{
    public static class EventRegistry
    {
        private static readonly Lazy<IReadOnlyList<RaidEvent>> Events = new(DiscoverEvents, LazyThreadSafetyMode.ExecutionAndPublication);
        public static IReadOnlyList<RaidEvent> GetEvents() => Events.Value;
        private static IReadOnlyList<RaidEvent> DiscoverEvents()
        {
            var eventType = typeof(RaidEvent);
            var events = Assembly.GetExecutingAssembly().GetTypes().Where(type => !type.IsAbstract && eventType
                .IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes)is not null).Select(type => (RaidEvent)Activator
                .CreateInstance(type)!).Where(raidEvent => raidEvent.Id != "loot_surge").OrderBy(raidEvent => raidEvent
                .Id == "normal" ? 0 : 1).ThenBy(raidEvent => raidEvent.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var duplicateId = events.GroupBy(raidEvent => raidEvent.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateId is not null)
            {
                throw new InvalidOperationException($"Duplicate raid event ID detected: {duplicateId.Key}");
            }

            if (events.All(raidEvent => raidEvent.Id != "normal"))
            {
                throw new InvalidOperationException("The Normal Raid event could not be discovered.");
            }

            return events;
        }
    }
}
