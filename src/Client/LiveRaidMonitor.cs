using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class LiveRaidMonitor
    {
        private ConfigEntry<bool> show = null!;
        private ConfigEntry<KeyboardShortcut> shortcut = null!;
        private RaidRoster roster = null!;
        private Func<string> status = () => "";
        private Rect rect = new Rect(0, 0, 650, 420);
        private bool wasVisible;
        private int screenWidth;
        private int screenHeight;
        private bool needsDock = true;
        private Vector2 scroll;
        private Vector2 resizeDelta;
        private string filter = "";
        private bool aliveOnly = true;
        private bool resizing;
        private float nextError;
        private bool interactive;
        private string liveStatus = "";
        private string context = "";
        private string summary = "";
        private string notice = "";
        private RosterEntry[] rows = Array.Empty<RosterEntry>();
        public void Bind(ConfigFile config, RaidRoster source, Func<string> details)
        {
            roster = source;
            status = details;
            const string section = "9. Advanced / Live Raid Monitor";
            show = AdvancedMenu.Bind(
                config,
                section,
                "Show Live Raid Monitor",
                false,
                "Standalone in-game window, visible with F12 closed. F8 toggles it by default. Does not " +
                "unlock the cursor or take movement controls."
            );
            shortcut = AdvancedMenu.Bind(
                config,
                section,
                "Monitor shortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Show/hide the monitor without opening F12."
            );
            var width = AdvancedMenu.Bind(
                config,
                section,
                "Monitor width",
                650,
                new ConfigDescription(
                    "Window width. Drag the lower-right grip while a menu releases the cursor. Dragged size/position last for this game launch.",



                    new AcceptableValueRange<int>(320, 1400)
                )
            );
            var height = AdvancedMenu.Bind(
                config,
                section,
                "Monitor height",
                420,
                new ConfigDescription("Window height. Clamped to the current screen.", new AcceptableValueRange<int>(180, 1000))
            );
            rect.width = width.Value;
            rect.height = height.Value;
            width.SettingChanged += (_, _) =>
            {
                rect.width = width.Value;
                needsDock = true;
            };
            height.SettingChanged += (_, _) =>
            {
                rect.height = height.Value;
                needsDock = true;
            };
        }

        public void Tick()
        {
            if (AdvancedMenu.Enabled && shortcut != null && shortcut.Value.IsDown())
            {
                show.Value = !show.Value;
            }
        }

        public void Draw()
        {
            if (!AdvancedMenu.Enabled || show == null || !show.Value)
            {
                wasVisible = false;
                return;
            }

            if (!wasVisible || needsDock || screenWidth != Screen.width || screenHeight != Screen.height)
            {
                var dock = MonitorDock.BottomRight(rect.width, rect.height, Screen.width, Screen.height);
                rect = new Rect(dock.X, dock.Y, dock.Width, dock.Height);
                screenWidth = Screen.width;
                screenHeight = Screen.height;
                needsDock = false;
            }

            wasVisible = true;
            // Take a stable display snapshot before entering GUILayout.
            rows = roster.Entries.Where(e => RosterView.Matches(e, filter, aliveOnly)).ToArray();
            context = roster.InRaid ? Plugin.RaidContext : "No active raid";
            summary = RosterView.Summary(roster.Entries);
            notice = roster.Notice;
            liveStatus = status();
            interactive = Cursor.lockState != CursorLockMode.Locked;
            var bounds = MonitorLayout.Clamp(rect.x, rect.y, rect.width, rect.height, Screen.width, Screen.height);
            rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            bool enabled = GUI.enabled;
            int depth = GUI.depth;
            try
            {
                GUI.depth = -100;
                rect = GUI.Window(0x44524510, rect, DrawWindow, "Raid Roulette — Live Raid Monitor");
                rect.width += resizeDelta.x;
                rect.height += resizeDelta.y;
                resizeDelta = Vector2.zero;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (Time.unscaledTime >= nextError)
                {
                    nextError = Time.unscaledTime + 30;
                    Plugin.Trace("[RR-MONITOR] render-error " + e.GetBaseException().Message);
                }
            }
            finally
            {
                GUI.enabled = enabled;
                GUI.depth = depth;
            }
        }

        private void DrawWindow(int id)
        {
            var wrapped = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                fontSize = 13
            };
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && interactive;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(40));
            filter = GUILayout.TextField(filter, GUILayout.MinWidth(100));
            aliveOnly = GUILayout.Toggle(aliveOnly, "Alive only", GUILayout.Width(95));
            if (GUILayout.Button("Close", GUILayout.Width(55)))
            {
                show.Value = false;
            }

            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            GUI.enabled = enabled;
            GUILayout.Label(context, wrapped);
            GUILayout.Label(summary, wrapped);
            GUILayout.Label(liveStatus, wrapped);
            if (notice.Length > 0)
            {
                GUILayout.Label(notice, wrapped);
            }

            foreach (var entry in rows)
            {
                string life = entry.Alive == true ? "Alive" : entry.Alive == false ? "Dead" : "Loading";
                GUILayout.Label(
                    $"{entry.Name} | {entry.Role}/{entry.Side} | Lv {entry.Level} | {life} | {(entry.Distance.HasValue ? entry.Distance.Value.ToString("0") + "m" : "?m")}"
                        +
                        $"\nID {entry.Id}",

                    wrapped
                );
            }

            if (rows.Length == 0)
            {
                GUILayout.Label("No matching registered AI.", wrapped);
            }

            GUI.enabled = enabled && interactive;
            GUILayout.EndScrollView();
            GUI.enabled = enabled;
            GUILayout.Label($"Showing {rows.Length} / {roster.Entries.Length} AI. F8: hide/show.", wrapped);
            GUILayout.Label("Open F12 or pause for cursor access to drag, resize, filter or scroll.", wrapped);
            if (interactive)
            {
                var grip = new Rect(rect.width - 22, rect.height - 20, 20, 18);
                GUI.Box(grip, "↘");
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && grip.Contains(evt.mousePosition))
                {
                    resizing = true;
                    evt.Use();
                }

                if (resizing && evt.type == EventType.MouseDrag)
                {
                    resizeDelta += evt.delta;
                    evt.Use();
                }

                if (evt.rawType == EventType.MouseUp)
                {
                    resizing = false;
                }

                if (!resizing)
                {
                    GUI.DragWindow(new Rect(0, 0, rect.width - 25, 24));
                }
            }
        }
    }
}
