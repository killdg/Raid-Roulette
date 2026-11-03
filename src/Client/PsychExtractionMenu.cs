using System;
using System.Collections.Generic;
using EFT.UI;
using EFT.UI.BattleTimer;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    // Presentation only: never writes ExfiltrationPoint.Settings, timers or requirements.
    // Cache row components when the native panel opens; no whole-scene discovery.
    internal sealed class PsychExtractionMenu : MonoBehaviour
    {
        private sealed class Row
        {
            internal ExitTimerPanel Panel = null!;
            internal string Original = "";
            internal string Replacement = "";
        }

        private static readonly string[] Destinations =
        {
            "De_Dust2",
            "San Andreas",
            "Nuketown",
            "Hyrule",
            "Kanto",
            "The Lands Between",
            "Skyrim",
            "Hamburger",
            "you asked for this"
        };
        private static PsychExtractionMenu? current;
        private readonly List<Row> rows = new();
        private ExtractionTimersPanel panel = null!;
        private float nextAllowed;
        private float started;
        private int testVariant;
        private bool showing;
        private bool waitingForClose;
        private static bool Eligible => Plugin.Local?.HealthController?.IsAlive == true && Plugin.RaidContext.Contains(": schizophrenia");

        internal static void Opened(ExtractionTimersPanel owner, bool showExits)
        {
            if (!showExits)
            {
                return;
            }

            var effect = owner.GetComponent<PsychExtractionMenu>();
            if (effect == null)
            {
                effect = owner.gameObject.AddComponent<PsychExtractionMenu>();
            }

            current = effect;
            effect.panel = owner;
            if (!Eligible || effect.waitingForClose || Time.unscaledTime < effect.nextAllowed)
            {
                return;
            }

            // Exactly one roll per opening. Repeated ShowTimer calls while the
            // list remains visible cannot reroll or change the selected wording.
            effect.waitingForClose = true;
            if (UnityEngine.Random.value < 0.33f)
            {
                effect.Begin(UnityEngine.Random.Range(0, 6));
            }
        }

        internal static bool Test()
        {
            if (!Eligible || current == null || current.panel == null)
            {
                return false;
            }

            current.Restore();
            current.panel.ShowTimer(true);
            current.Begin(current.testVariant++ % 6);
            return true;
        }

        private void Begin(int variant)
        {
            Restore();
            int destinationOffset = UnityEngine.Random.Range(0, Destinations.Length);
            foreach (var row in panel.GetComponentsInChildren<ExitTimerPanel>(false))
            {
                if (row._pointName == null || string.IsNullOrWhiteSpace(row._pointName.text))
                {
                    continue;
                }

                string original = row._pointName.text;
                string replacement = variant switch
                {
                    0 => Destinations[(rows.Count + destinationOffset) % Destinations.Length],
                    1 => "You were already here",
                    2 => "NO EXITS FOUND",
                    3 => Curse(original),
                    4 => @"/\/\/\/\/\/\/\/\/\/\/\/\/\",
                    _ => Symbols(row, rows.Count)};
                rows.Add(new Row { Panel = row, Original = original, Replacement = replacement });
            }

            showing = rows.Count > 0;
            waitingForClose = true;
            started = Time.unscaledTime;
            if (showing)
            {
                nextAllowed = started + 90f;
                Plugin.Trace("[RR-PSYCH] extract illusion variant=" + variant + " rows=" + rows.Count);
            }
        }

        private static string Curse(string value)
        {
            var result = new System.Text.StringBuilder();
            foreach (char letter in value)
            {
                result.Append(letter);
                if (!char.IsWhiteSpace(letter))
                {
                    result.Append("\u0334\u0307\u0323");
                }
            }

            return result.ToString();
        }

        private static string Symbols(ExitTimerPanel row, int offset)
        {
            // Do not download fonts or depend on Windows Webdings. Use only
            // glyphs already supplied by this label's native TMP font asset.
            const string candidates = "☠☢⚠✈★◆●■▲▼<>?!";
            var result = new System.Text.StringBuilder();
            for (int index = 0; index < 12; index++)
            {
                char symbol = candidates[(index + offset) % candidates.Length];
                result.Append(row._pointName.font != null && row._pointName.font.HasCharacter(symbol) ? symbol : '?');
            }

            return result.ToString();
        }

        private void LateUpdate()
        {
            if (!Eligible || panel == null || !panel.gameObject.activeInHierarchy)
            {
                Restore();
                waitingForClose = false;
                return;
            }

            bool visible = panel._timersPanel != null && panel._timersPanel.gameObject.activeInHierarchy;
            if (!visible && Time.unscaledTime - started > 0.5f)
            {
                Restore();
                waitingForClose = false;
                return;
            }

            if (showing)
            {
                foreach (var row in rows)
                {
                    if (row.Panel != null && row.Panel._pointName != null)
                    {
                        row.Panel._pointName.text = row.Replacement;
                    }
                }
            }
        }

        private void Restore()
        {
            foreach (var row in rows)
            {
                if (row.Panel != null && row.Panel._pointName != null && row.Panel._pointName.text == row.Replacement)
                {
                    row.Panel._pointName.text = row.Original;
                }
            }

            rows.Clear();
            showing = false;
        }

        private void OnDisable()
        {
            Restore();
            waitingForClose = false;
        }

        private void OnDestroy()
        {
            Restore();
            if (current == this)
            {
                current = null;
            }
        }
    }

    [HarmonyPatch(typeof(ExtractionTimersPanel), nameof(ExtractionTimersPanel.ShowTimer))]
    internal static class PsychExtractionMenuOpen
    {
        private static void Postfix(ExtractionTimersPanel __instance, bool showExits)
        {
            PsychExtractionMenu.Opened(__instance, showExits);
        }
    }
}
