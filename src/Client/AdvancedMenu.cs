using Killdg.DeveloperTools;
using System;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace RaidRoulette.TestingClient
{
    internal static class AdvancedMenu
    {
        private sealed class Gate(ConfigurationManagerAttributes tag, bool? visible)
        {
            public ConfigurationManagerAttributes Tag = tag;
            public bool? Visible = visible;
        }

        private static readonly List<Gate> gates = new();
        private static ConfigEntry<bool>? enabled;
        private static bool refresh;
        internal static bool Enabled => DeveloperSettings.Available && enabled?.Value == true;

        internal static void Initialize(ConfigFile config)
        {
            MenuAction.AdoptAdvanced = Adopt;
            enabled = DeveloperSettings.Bind(
                config,
                "9. Advanced",
                "Show advanced options",
                false,
                "Show developer controls in DG Testing Tools: live AI roster, hunter tracking, "
                    + "spawn diagnostics, Ghost Review, quick travel and the F2 scare panel. "
                    + "Requires the optional DG Testing Tools plugin."
            );
            enabled.SettingChanged += (_, _) =>
            {
                foreach (var gate in gates)
                {
                    if (Enabled)
                    {
                        gate.Tag.Browsable = gate.Visible;
                    }
                    else
                    {
                        gate.Visible = gate.Tag.Browsable;
                        gate.Tag.Browsable = false;
                    }
                }

                refresh = true;
            };
        }

        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, string text) =>
             Bind(config, section, key, value, new ConfigDescription(text));
        internal static ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T value, ConfigDescription description)
        {
            if (!section.StartsWith("9. Advanced", StringComparison.Ordinal))
            {
                return config.Bind(section, key, value, description);
            }

            var tag = description.Tags.OfType<ConfigurationManagerAttributes>().FirstOrDefault() ?? new ConfigurationManagerAttributes();
            gates.Add(new Gate(tag, tag.Browsable));
            if (!Enabled)
            {
                tag.Browsable = false;
            }

            var tags = description.Tags.Contains(tag) ? description.Tags : description.Tags.Concat(new object[] { tag }).ToArray();
            return DeveloperSettings.Bind(
                config,
                section,
                key,
                value,
                new ConfigDescription(description.Description, description.AcceptableValues, tags)
            );
        }

        internal static void Adopt(ConfigurationManagerAttributes tag)
        {
            gates.Add(new Gate(tag, tag.Browsable));
            if (!Enabled)
            {
                tag.Browsable = false;
            }
        }

        internal static ConfigDescription Diagnostic(string text, AcceptableValueBase? range = null)
        {
            var tag = new ConfigurationManagerAttributes();
            return new ConfigDescription(text, range, tag);
        }

        internal static void RequestRefresh() => refresh = true;
        internal static void Tick()
        {
            if (!refresh)
            {
                return;
            }

            refresh = false;
            try
            {
                var manager = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Select(p => p.Instance).FirstOrDefault(p =>
                     p != null && p.GetType().FullName == "ConfigurationManager.ConfigurationManager");
                manager?.GetType().GetMethod("BuildSettingList", Type.EmptyTypes)?.Invoke(manager, null);
            }
            catch (Exception e)
            {
                Plugin.Trace("[RR-MENU] " + e.GetBaseException().Message);
            }
        }
    }
}
