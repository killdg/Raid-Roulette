using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using RaidRoulette.Shared;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal enum RaidPreset
    {
        Mostly_Normal,
        Half_And_Half,
        Custom
    }

    internal sealed class RaidChanceSettings
    {
        private ConfigEntry<RaidPreset> preset = null!;
        private ConfigEntry<string> summary = null!;
        private readonly Dictionary<string, ConfigEntry<float>> entries = new();
        private readonly Dictionary<string, ConfigEntry<bool>> enabled = new();
        private readonly Dictionary<string, string> labels = new();
        private ConfigEntry<float> normalChance = null!;
        private Vector2 scroll;
        private string filter = "";
        private ConfigFile file = null!;
        private readonly Dictionary<string, string> drafts = new();
        private readonly List<ConfigurationManagerAttributes> customRows = new();
        internal void Bind(ConfigFile config, Action changed)
        {
            file = config;
            // Migrate the previous two chance modes without discarding a user's
            // saved allocation. Presets only change the outgoing snapshot.
            string saved = File.Exists(config.ConfigFilePath) ? File.ReadAllText(config.ConfigFilePath) : "";
            bool legacy = saved.Contains("Chance mode =") && !saved.Contains("Raid preset =");
            var oldMode = config.Bind("2. Raid chances", "Chance mode", RaidChanceMode.OverallChance, Hidden());
            var oldFrequency = config.Bind("2. Raid chances", "Any event chance (%)", 40f, Hidden());
            config.Bind("2. Raid chances", "Event percentages", false, Hidden());
            preset = config.Bind(
                "2. Raid chances",
                "Raid preset",
                legacy ? RaidPreset.Custom : RaidPreset.Mostly_Normal,
                "Mostly Normal: 90% normal raids, 10% events. Half And Half: 50% normal, "
                    + "50% events. Custom: choose relative weights; zero excludes an event. "
                        + "Unsupported map shares become normal raids."
            );
            summary = config.Bind(
                "2. Raid chances",
                "Chance status",
                "",
                new ConfigDescription(
                    "Current selection balance.",
                    null,
                    new ConfigurationManagerAttributes { ReadOnly = true, HideDefaultButton = true }
                )
            );
            var choices = Enum.GetValues(typeof(RaidChoice)).Cast<RaidChoice>().Where(choice => choice != RaidChoice.Random).ToArray();
            int poolSize = choices.Count(choice => choice != RaidChoice.Normal && choice != RaidChoice.Snowball_Trials
                && choice != RaidChoice.Trials && choice != RaidChoice.Extreme_Trials && choice != RaidChoice.Guard_Trials
                && choice != RaidChoice.Total_Trial);
            foreach (var choice in choices)
            {
                string id = RaidChoiceNames.EventId(choice.ToString());
                float initial = id == "normal" ? 90f : id == "snowball" || id == "trials" || id == "extreme_trials"
                    || id == "guard_trials" || id == "total_trial" ? 0f : 10f / poolSize;
                var tag = new ConfigurationManagerAttributes
                {
                    Browsable = false
                };
                entries[id] = config.Bind(
                    "3. Raid percentages",
                    choice.ToString().Replace('_', ' ') + " (%)",
                    initial,
                    new ConfigDescription(
                        "Custom relative weight. Zero disables automatic selection. "
                            + "Weights are normalized; for example Normal 10, Clone Wars 10, Stormtide 80 "
                            + "means 10%, 10%, 80%. Unsupported shares become Normal.",
                        new AcceptableValueRange<float>(0, 100),
                        tag
                    )
                );
                entries[id].SettingChanged += (_, _) =>
                {
                    Refresh();
                    changed();
                };
            }

            if (legacy && oldMode.Value == RaidChanceMode.OverallChance)
            {
                double total = entries.Where(pair => pair.Key != "normal").Sum(pair => pair.Value.Value);
                foreach (var pair in entries)
                {
                    pair.Value.Value = pair.Key == "normal" ? 100 - oldFrequency.Value : total > 0 ? (float)(oldFrequency.Value * pair
                        .Value.Value / total) : 0;
                }
            }

            if (legacy && oldMode.Value == RaidChanceMode.IndividualPercentages)
            {
                // The old exact mode already gave every unused percentage to
                // Normal, regardless of the saved Normal row itself.
                double events = entries.Where(pair => pair.Key != "normal").Sum(pair => pair.Value.Value);
                entries["normal"].Value = (float)Math.Max(0, 100 - events);
            }

            // Derive the old effective Normal share once. Subsequent changes
            // to event weights never move this independent Normal slider.
            double eventTotal = entries.Where(pair => pair.Key != "normal").Sum(pair => pair.Value.Value);
            double totalWeight = eventTotal + entries["normal"].Value;
            float previousNormal = totalWeight > 0 ? (float)(100 * entries["normal"].Value / totalWeight) : 100;
            var normalTag = new ConfigurationManagerAttributes();
            customRows.Add(normalTag);
            normalChance = config.Bind(
                "2. Raid chances",
                "Normal raid chance (%)",
                previousNormal,
                new ConfigDescription(
                    "Custom mode: the remaining chance is shared among enabled events. " + "Event weights do not need to add up to 100.",
                    new AcceptableValueRange<float>(0, 100),
                    normalTag
                )
            );
            foreach (var choice in choices.Where(choice => choice != RaidChoice.Normal))
            {
                string id = RaidChoiceNames.EventId(choice.ToString());
                labels[id] = RaidEventPicker.Label(choice);
                enabled[id] = config.Bind("Event mix preferences", id + " enabled", entries[id].Value > 0, Hidden());
                enabled[id].SettingChanged += (_, _) =>
                {
                    Refresh();
                    changed();
                };
            }

            var mixTag = new ConfigurationManagerAttributes
            {
                CustomDrawer = _ => DrawMix(),
                HideDefaultButton = true
            };
            customRows.Add(mixTag);
            config.Bind(
                "2. Raid chances",
                "Custom event mix",
                false,
                new ConfigDescription(
                    "Enable events and adjust their relative weights. "
                        + "The list scrolls independently and displays calculated raid chances.",
                    null,
                    mixTag
                )
            );
            normalChance.SettingChanged += (_, _) =>
            {
                Refresh();
                changed();
            };
            preset.SettingChanged += (_, _) =>
            {
                Refresh();
                AdvancedMenu.RequestRefresh();
                changed();
            };
            Refresh();
        }

        private static ConfigDescription Hidden()
        {
            return new ConfigDescription("Legacy setting retained for migration.", null,
                 new ConfigurationManagerAttributes { Browsable = false });
        }

        internal RaidChanceProfile Snapshot()
        {
            if (preset.Value == RaidPreset.Custom)
            {
                return EventMixPolicy.Create(
                    normalChance.Value,
                    entries.ToDictionary(pair => pair.Key, pair => (double)pair.Value.Value),
                    enabled.ToDictionary(pair => pair.Key, pair => pair.Value.Value)
                );
            }

            return new RaidChanceProfile
            {
                Mode = "OverallChance",
                EventChance = preset.Value == RaidPreset.Half_And_Half ? 50 : 10,
                Percentages = entries.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Key == "normal" || pair.Key == "snowball" || pair.Key == "trials" || pair.Key == "extreme_trials"
                        || pair.Key == "guard_trials" || pair.Key == "total_trial" ? 0d : 1d
                )
            };
        }

        private void Batch(Action action)
        {
            bool save = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            try
            {
                action();
            }
            finally
            {
                file.SaveOnConfigSet = save;
            }

            file.Save();
            drafts.Clear();
        }

        private void DrawMix()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Enable events • Weight controls relative frequency");
            filter = GUILayout.TextField(filter);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable all"))
            {
                Batch(() =>
                {
                    foreach (string id in enabled.Keys)
                    {
                        if (entries[id].Value <= 0)
                        {
                            entries[id].Value = 1;
                        }

                        enabled[id].Value = true;
                    }
                });
            }

            if (GUILayout.Button("Disable all"))
            {
                Batch(() =>
                {
                    foreach (var value in enabled.Values)
                    {
                        value.Value = false;
                    }
                });
            }

            if (GUILayout.Button("Equal weights"))
            {
                Batch(() =>
                {
                    foreach (string id in enabled.Keys)
                    {
                        entries[id].Value = 1;
                    }
                });
            }

            GUILayout.EndHorizontal();
            double total = enabled.Where(pair => pair.Value.Value).Sum(pair => entries[pair.Key].Value);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(260));
            foreach (string id in enabled.Keys.OrderBy(id => labels[id], StringComparer.OrdinalIgnoreCase))
            {
                if (labels[id].IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                bool selected = GUILayout.Toggle(enabled[id].Value, labels[id]);
                if (selected != enabled[id].Value)
                {
                    if (selected && entries[id].Value <= 0)
                    {
                        entries[id].Value = 1;
                    }

                    enabled[id].Value = selected;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Weight", GUILayout.Width(44));
                if (GUILayout.Button("−", GUILayout.Width(24)))
                {
                    entries[id].Value = Math.Max(0, entries[id].Value - 1);
                    drafts.Remove(id);
                }

                string previous = drafts.TryGetValue(id, out var draft) ? draft : entries[id].Value.ToString("0.##");
                string value = GUILayout.TextField(previous, GUILayout.Width(44));
                drafts[id] = value;
                if (value != previous && float.TryParse(value, out float weight) && !float.IsNaN(weight) && !float.IsInfinity(weight))
                {
                    entries[id].Value = Mathf.Clamp(weight, 0, 100);
                }

                if (GUILayout.Button("+", GUILayout.Width(24)))
                {
                    entries[id].Value = Math.Min(100, entries[id].Value + 1);
                    drafts.Remove(id);
                }

                double chance = selected && total > 0 ? (100 - normalChance.Value) * entries[id].Value / total : 0;
                GUILayout.Label(chance.ToString("0.#") + "% of raids", GUILayout.Width(86));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            if (total <= 0)
            {
                GUILayout.Label("No enabled event weight: all raids will be Normal.");
            }

            GUILayout.EndVertical();
        }

        private void Refresh()
        {
            if (preset == null || summary == null || normalChance == null)
            {
                return;
            }

            foreach (var tag in customRows)
            {
                tag.Browsable = preset.Value == RaidPreset.Custom;
            }

            var profile = Snapshot();
            summary.Value = $"{100 - profile.EventChance:0.##}% Normal / "
                + $"{profile.EventChance:0.##}% events. Unsupported event shares become Normal.";
        }
    }
}
