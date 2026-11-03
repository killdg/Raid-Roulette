using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace RaidRoulette.TestingClient
{
    // Native config entries retain their saved values while their rows are collapsed.
    public sealed class SettingsFoldout
    {
        public readonly ConfigEntry<bool> Header;
        private readonly List<ConfigurationManagerAttributes> rows = new();
        public SettingsFoldout(ConfigFile config, string section, string key, Action refresh, Action<ConfigEntryBase>? drawer = null)
        {
            Header = config.Bind(
                section,
                key,
                false,
                new ConfigDescription(
                    "Expand or collapse the individual event percentages.",
                    null,
                    new ConfigurationManagerAttributes { CustomDrawer = drawer, HideDefaultButton = true }
                )
            );
            Header.SettingChanged += (_, _) =>
            {
                foreach (var row in rows)
                {
                    row.Browsable = Header.Value;
                }

                refresh();
            };
        }

        public ConfigurationManagerAttributes Row()
        {
            var tag = new ConfigurationManagerAttributes
            {
                Browsable = Header.Value
            };
            rows.Add(tag);
            return tag;
        }
    }
}
