using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal static class RaidEventPicker
    {
        private static bool expanded;
        private static string search = "";
        private static Vector2 scroll;
        internal static void Draw(ConfigEntryBase entry)
        {
            GUILayout.BeginVertical();
            if (Plugin.FenceSelectionActive)
            {
                GUILayout.Label("Fence controls the next raid. Disable Trader Event Activation to use this manual selection.");
            }

            if (GUILayout.Button(Label((RaidChoice)entry.BoxedValue) + (expanded ? " ▲" : " ▼")))
            {
                expanded = !expanded;
            }

            if (expanded)
            {
                GUILayout.Label("Search events");
                search = GUILayout.TextField(search);
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(220));
                foreach (var choice in Enum.GetValues(typeof(RaidChoice)).Cast<RaidChoice>())
                {
                    string label = Label(choice);
                    if (label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 && GUILayout.Button(label))
                    {
                        entry.BoxedValue = choice;
                        expanded = false;
                    }
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        internal static string Label(RaidChoice choice)
        {
            return choice.ToString().Replace('_', ' ').Replace("Pmc", "PMC");
        }
    }
}
