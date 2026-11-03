using Killdg.DeveloperTools;
using System;
using BepInEx.Configuration;

namespace RaidRoulette.TestingClient
{
    // Configuration Manager reads metadata FIELDS by name, not anonymous-object properties.
    public sealed class ConfigurationManagerAttributes
    {
        public Action<ConfigEntryBase>? CustomDrawer;
        public bool? ReadOnly;
        public bool? HideDefaultButton;
        public bool? Browsable;
        public int? Order;
    }

    public static class MenuAction
    {
        internal static Action<ConfigurationManagerAttributes>? AdoptAdvanced;
        public static ConfigEntry<bool> Bind(ConfigFile config, string section, string name, string description,
             Action action, object? metadata = null)
        {
            if ((section == "8. Stormtide") && metadata is ConfigurationManagerAttributes advancedTag)
            {
                AdoptAdvanced?.Invoke(advancedTag);
            }

            ConfigDescription details = new ConfigDescription(description, null, metadata == null ? Array.Empty<object>()
                : new[] { metadata });
            ConfigEntry<bool> entry = section == "1. Next raid" ? config.Bind(section, name, false, details)
                : DeveloperSettings.Bind(config, section, name, false, details);
            // Old versions saved these flags as true without handling clicks. Never run a stale saved action on startup.
            entry.Value = false;
            entry.SettingChanged += (_, _) =>
            {
                if (!entry.Value)
                {
                    return;
                }

                entry.Value = false;
                action();
            };
            // Fallback for managers that ignore CustomDrawer: the checkbox is a momentary action.
            return entry;
        }
    }

    // Coalesce dropdown/option edits and wait for an in-flight request before sending the latest choice.
    public sealed class AutoApplyTracker
    {
        private int revision;
        private int observed;
        private int dispatched;
        private float after;
        public bool HasPending => Revision != dispatched;
        public int Revision => System.Threading.Volatile.Read(ref revision);

        public void Changed() => System.Threading.Interlocked.Increment(ref revision);
        public bool Observe(float now)
        {
            var latest = Revision;
            if (observed == latest)
            {
                return false;
            }

            observed = latest;
            after = now + 0.35f;
            return true;
        }

        public bool Ready(float now, bool busy) => !busy && observed == Revision && observed != dispatched && now >= after;
        public void Dispatched(int value) => dispatched = value;
    }

    public static class EventSelection
    {
        public static string EventId(bool force, string selected) => force ? selected.ToLowerInvariant() : "random";
        public static bool Matches(string expected, string message) => message.StartsWith("Queued: " + expected + " (",
             StringComparison.OrdinalIgnoreCase);
    }
}
