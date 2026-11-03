using System;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class RaidRoster
    {
        internal bool InRaid => world != null;
        internal RosterEntry[] Entries => entries;
        internal string Notice => notice;

        private GameWorld? world;
        private float nextScan;
        private bool refreshMenu;
        private string notice = "";
        private Func<string> waves = () => "";
        private RosterSettings settings = null!;
        private RosterEntry[] entries = Array.Empty<RosterEntry>();
        public void Bind(ConfigFile config, Func<string> waveStatus)
        {
            waves = waveStatus;
            settings = new RosterSettings(
                config,
                () => refreshMenu = true,
                entry =>
            {
                bool expanded = (bool)entry.BoxedValue;
                if (GUILayout.Button(expanded ? "▼ Hide live raid details" : "▶ Show live raid details"))
                {
                    entry.BoxedValue = !expanded;
                }
            }
            );
            foreach (var pair in config.Where(p => p.Key.Section == "9. Advanced / Live raid information"))
            {
                foreach (var tag in pair.Value.Description.Tags.OfType<ConfigurationManagerAttributes>())
                {
                    AdvancedMenu.Adopt(tag);
                }
            }

            foreach (var pair in config.Where(pair => pair.Key.Section == "9. Advanced / Live raid information"))
            {
                foreach (var tag in pair.Value.Description.Tags.OfType<ConfigurationManagerAttributes>())
                {
                    AdvancedMenu.Adopt(tag);
                }
            }

            settings.Filter.SettingChanged += (_, _) => nextScan = 0;
            settings.AliveOnly.SettingChanged += (_, _) => nextScan = 0;
            settings.Page.SettingChanged += (_, _) => nextScan = 0;
            settings.Publish(entries, "No active raid", "No active raid", "", false);
        }

        public void Tick()
        {
            if (!AdvancedMenu.Enabled)
            {
                return;
            }

            if (refreshMenu)
            {
                refreshMenu = false;
                try
                {
                    var manager = BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Select(p => p.Instance).FirstOrDefault(p =>
                         p != null && p.GetType().FullName == "ConfigurationManager.ConfigurationManager");
                    manager?.GetType().GetMethod("BuildSettingList", Type.EmptyTypes)?.Invoke(manager, null);
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-MENU] roster refresh=" + e.GetBaseException().Message);
                }
            }

            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(current, world))
            {
                world = current;
                entries = Array.Empty<RosterEntry>();
                notice = "";
                nextScan = 0;
            }

            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + 1;
            if (current != null)
            {
                try
                {
                    var local = Plugin.Local;
                    entries = current.RegisteredPlayers.OfType<Player>().Where(p => p != null && p.IsAI).Select(p => new RosterEntry(
                            p.ProfileId ?? "?",
                            p.Profile?.Info?.Nickname ?? "Loading",
                            LabWorkDirector.IsStaff(p) ? "Civilian" : p.Profile?.Info?.Settings?.Role.ToString() ?? "Unknown",
                            p.Profile?.Info?.Side.ToString() ?? "?",
                            p.Profile?.Info?.Level ?? 0,
                            p.HealthController == null ? (bool? )null : p.HealthController.IsAlive,
                            local == null ? (float? )null : Vector3.Distance(local.Position, p.Position)
                        )).OrderByDescending(e => e.Alive == true).ThenBy(e => e.Role, StringComparer.Ordinal).ThenBy(e => e.Name,
                             StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
                    notice = "";
                }
                catch (Exception e)
                {
                    notice = "Roster refresh failed: " + e.GetBaseException().Message;
                }
            }

            settings.Publish(entries, Plugin.RaidContext, waves(), notice, current != null);
        }
    }
}
