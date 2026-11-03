using SPTarkov.Server.Core.Models.Common;

namespace RaidRoulette.Runtime
{
    // Optional campaign contract. The event engine never loads quest definitions itself.
    public interface IEventCampaign
    {
        string? Select(MongoId session, string? map);
        void Begin(MongoId session, string map, string eventId);
        void GunGameWin(MongoId session);
    }

    public static class EventIntegration
    {
        private sealed record Provider(IEventCampaign Campaign, int Priority);
        private static readonly object Gate = new();
        private static readonly List<Provider> providers = [];
        private static readonly Dictionary<MongoId, (IEventCampaign Campaign, string Event)> selected = [];
        private static readonly Dictionary<MongoId, IEventCampaign> active = [];
        public static bool Ready { get; internal set; }

        public static bool Active
        {
            get
            {
                lock (Gate)
                {
                    return providers.Count > 0;
                }
            }
        }

        public static void Register(IEventCampaign value) => Register(value, 0);
        public static void Register(IEventCampaign value, int priority)
        {
            if (!Ready)
            {
                throw new InvalidOperationException("Raid Roulette must be enabled and loaded before its quest pack.");
            }

            ArgumentNullException.ThrowIfNull(value);
            lock (Gate)
            {
                if (providers.Any(p => p.Campaign.GetType() == value.GetType()))
                {
                    throw new InvalidOperationException("This campaign provider is already registered.");
                }

                providers.Add(new(value, priority));
                providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        public static string? Select(MongoId session, string? map)
        {
            Provider[] candidates;
            lock (Gate)
            {
                selected.Remove(session);
                candidates = providers.ToArray();
            }

            foreach (var provider in candidates)
            {
                var id = provider.Campaign.Select(session, map);
                if (id == null)
                {
                    continue;
                }

                lock (Gate)
                {
                    selected[session] = (provider.Campaign, id);
                }

                return id;
            }

            return null;
        }

        public static void Begin(MongoId session, string map, string eventId)
        {
            IEventCampaign? owner = null;
            lock (Gate)
            {
                active.Remove(session);
                if (selected.Remove(session, out var pending) && pending.Event == eventId)
                {
                    active[session] = owner = pending.Campaign;
                }
            }

            owner?.Begin(session, map, eventId);
        }

        public static void GunGameWin(MongoId session)
        {
            IEventCampaign? owner;
            lock (Gate)
            {
                active.TryGetValue(session, out owner);
            }

            owner?.GunGameWin(session);
        }
    }
}
