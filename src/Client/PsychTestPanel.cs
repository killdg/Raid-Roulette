using Killdg.DeveloperTools;
using System;
using System.Linq;
using EFT;
using EFT.InputSystem;
using EFT.Interactive;
using EFT.UI;
using Comfort.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    public sealed class PsychPanelInput : InputNode
    {
        public override ETranslateResult TranslateCommand(ECommand command) => ETranslateResult.Block;
        public override void TranslateAxes(ref float[] axes)
        {
            Array.Clear(axes, 0, axes.Length);
        }

        public override ECursorResult ShouldLockCursor() => ECursorResult.ShowCursor;
    }

    internal sealed partial class SchizophreniaDirector
    {
        private bool panelOpen;
        private bool pauseScares;
        private Rect panelRect = new Rect(30, 90, 470, 690);
        private Vector2 panelScroll;
        private IInputTree? panelTree;
        private PlayerOwner? panelOwner;
        private bool panelOnOwner;
        private PsychPanelInput? panelInput;
        private string panelMessage = "Select a test. Placement and asset requirements still apply.";
        private float panelNext;
        private void PanelTick()
        {
            if (!DeveloperSettings.Available || !AdvancedMenu.Enabled)
            {
                if (panelOpen)
                {
                    ClosePanel();
                }

                return;
            }

            if (new BepInEx.Configuration.KeyboardShortcut(KeyCode.F2).IsDown())
            {
                if (panelOpen)
                {
                    ClosePanel();
                    return;
                }

                panelOwner = Plugin.Local?.GetComponent<GamePlayerOwner>() ?? Plugin.Local?.GetComponent<PlayerOwner>();
                if (panelOwner == null)
                {
                    Log("stage=test-panel local-player-input-owner-unavailable");
                    return;
                }

                panelTree = panelOwner.InputTree;
                panelInput = new GameObject("RR_PsychPanelInput").AddComponent<PsychPanelInput>();
                panelOnOwner = false;
                if (panelTree != null)
                {
                    panelTree.Add(panelInput);
                    panelOnOwner = !panelTree.Contains(panelInput);
                }
                else
                {
                    panelOnOwner = true;
                }

                if (panelOnOwner)
                {
                    panelOwner.Add(panelInput);
                }

                panelOpen = true;
                Log("stage=test-panel-open owner=" + panelOwner.GetType().Name + " tree=" + (panelTree?.GetType().Name ?? "none")
                    + " attachment=" + (panelOnOwner ? "player-owner" : "input-tree"));
            }
        }

        private void ClosePanel()
        {
            panelOpen = false;
            if (panelInput != null)
            {
                if (panelOnOwner)
                {
                    if (panelOwner != null)
                    {
                        panelOwner.Remove(panelInput);
                    }
                }
                else
                {
                    panelTree?.Remove(panelInput);
                }

                Object.Destroy(panelInput.gameObject);
            }

            panelInput = null;
            panelTree = null;
            panelOwner = null;
            panelOnOwner = false;
        }

        internal void Draw()
        {
            if (!active || !panelOpen)
            {
                return;
            }

            panelRect.height = Mathf.Min(690, Screen.height - 100);
            panelRect = GUI.Window(734891, panelRect, Panel, "Schizophrenia testing - F2 to close");
        }

        private void Panel(int id)
        {
            GUILayout.Label("Raid keeps running. F2 closes this panel and returns movement controls.");
            pauseScares = GUILayout.Toggle(pauseScares, "Pause automatic scares while testing");
            GUILayout.Label("Injury frequency boost: +" + (injuryBoost * 100).ToString("0") + "% (each condition type counts once)");
            GUILayout.Label(medical.Status);
            GUILayout.Label("Rapid-fire sound illusion: changes after 10 shots with gaps under 0.20 seconds. Turn OFF "
                + "Pause automatic scares, then close F2 to test.");
            GUILayout.Label(panelMessage);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            bool ready = Now >= panelNext;
            void Button(string text, Action action)
            {
                GUI.enabled = ready;
                if (GUILayout.Button(text))
                {
                    panelNext = Now + 1;
                    panelMessage = "Requested: " + text + ". Check the scene and RR-PSYCH log.";
                    Attempt("manual-" + text, action);
                }

                GUI.enabled = true;
            }

            Button(
                "Extract menu illusion - next variant",
                () =>
            {
                if (!PsychExtractionMenu.Test())
                {
                    panelMessage = "Open the native extract list once in this Schizophrenia raid, then retry.";
                }
            }
            );
            Button("Visible door open / close", DoorPulse);
            Button(
                "Shatter nearby glass / restore after 4 seconds",
                () =>
            {
                if (!worldScares.Glass.Trigger(Plugin.Local!))
                {
                    panelMessage = "No supported intact glass in the forward 12-metre cone, or an illusion is already active.";
                }
            }
            );
            Button("Scav or PMC voice - random distance", Voice);
            Button("Phantom gunfire - random distance", Gunfire);
            Button("Glass breaking sound - random distance", GlassSound);
            Button("Grenade pin / fuze - random distance", Tripwire);
            Button("Visual grenade", Grenade);
            Button(
                "Phantom magazines (current weapon)",
                () => Reloaded((Plugin.Local?.HandsController?.Item as EFT.InventoryLogic.Weapon)?.GetCurrentMagazine(), true)
            );
            Button("Preview alternate gun sound (no bullet)", TestGunSound);
            Button("Fake jam warning (weapon still works)", FakeJamNotice);
            Button("Moving decoy", Decoy);
            Button(
                "Human jukebox",
                () =>
            {
                if (musicVisual == null)
                {
                    if (!TryMusicDecoy())
                    {
                        panelMessage = "No clear reachable placement yet. Close the panel, face open ground, then retry.";
                    }
                }
                else
                {
                    panelMessage = "A jukebox encounter is already active. Clear it below to test again.";
                }
            }
            );
            Button(
                "Clear decoys / rearm jukebox",
                () =>
            {
                StopMusic();
                foreach (var v in visuals)
                {
                    Destroy(v);
                }

                visuals.Clear();
            }
            );
            Button(
                "Did you hear that? notice",
                () => EFT.Communications.NotificationManager.DisplayMessageNotification(
                    "Did you hear that?",
                    EFT.Communications.ENotificationDurationType.Long,
                    EFT.Communications.ENotificationIconType.Quest
                )
            );
            Button("False death sound + reassurance", FalseDeath);
            Button(
                "12-hour shift / restore (bypasses room check)",
                () =>
            {
                if (timeJump.Active)
                {
                    timeJump.Stop();
                }
                else
                {
                    timeJump.Reset();
                    timeJump.Tick(0, 0, true);
                }
            }
            );
            Button(
                "Nearby map lights - 4-second pulse",
                () =>
            {
                if (!worldScares.LightScare(Plugin.Local!))
                {
                    panelMessage = "No controllable map lights nearby, or another pulse is active.";
                }
            }
            );
            Button(
                "Nearby car trunk burst",
                () =>
            {
                if (!worldScares.CarScare(Plugin.Local!))
                {
                    panelMessage = "No visible usable trunk nearby, or another pulse is active.";
                }
            }
            );
            Button("Cold / frost - 10 seconds", () => worldScares.TestCold(Plugin.Local!));
            Button("Backpack - random pattern", () => worldScares.TestShuffle(Plugin.Local!));
            Button("Backpack - attempt full shuffle", () => worldScares.TestShuffle(Plugin.Local!, 1));
            Button("Backpack - group by item type", () => worldScares.TestShuffle(Plugin.Local!, 2));
            Button("Backpack - largest items first", () => worldScares.TestShuffle(Plugin.Local!, 3));
            Button("Backpack - reverse rows", () => worldScares.TestShuffle(Plugin.Local!, 4));
            Button("Reverse phantom plane", () => FastPlaneDirection(true));
            Button("Upside-down phantom plane", () => FastPlaneDirection(false, PhantomFlight.UpsideDown));
            Button("360 rolling phantom plane", () => FastPlaneDirection(false, PhantomFlight.BarrelRoll));
            Button("Spinning / tumbling phantom plane", () => FastPlaneDirection(false, PhantomFlight.Tumble));
            Button("Seven-plane blob", () => FastPlaneDirection(false, PhantomFlight.Blob));
            Button("Rear stationary crowd", () =>
            {
                StationaryCrowd();
            });
            Button(
                "Wrong Door - near an open doorway",
                () =>
            {
                if (DoorTeleportDirector.Current?.Test() != true)
                {
                    panelMessage = "No nearby open source doorway or safe open destination. Active Wrong Door/Schizophrenia raid required.";
                }
            }
            );
            Button("Nearby glass burst", () => worldScares.Glass.Trigger(Plugin.Local!, true));
            Button("Fast phantom airdrop plane - outdoors", FastPlane);
            GUILayout.Label("Medical prototype: every blacked-out-limb CMS/Surv12 repair attempts cosmetic extra rigs; "
                + "only the selected item treats you.");
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close - F2"))
            {
                ClosePanel();
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void FalseDeath()
        {
            if (falseDeathShown)
            {
                panelMessage = "False-death scare already played this raid.";
                return;
            }

            if (!Singleton<GUISounds>.Instantiated)
            {
                return;
            }

            var gui = Singleton<GUISounds>.Instance;
            var bank = HarmonyLib.AccessTools.Field(typeof(GUISounds), "_UISounds")?.GetValue(gui) as UISoundsWrapper;
            var clip = bank?.GetEndGameClip(EEndGameSoundType.ArenaLose);
            float delay = (float)PsychScarePolicy.DeathNoticeDelay(clip?.length ?? 0);
            falseDeathShown = true;
            incidentAt = Now + Mathf.Max(12, delay + 3);
            gui.PlayEndGameSound(EEndGameSoundType.ArenaLose);
            scheduled.Add((Now + delay, () => EFT.Communications.NotificationManager.DisplayMessageNotification(
                    "....you're not dead",
                    EFT.Communications.ENotificationDurationType.Long,
                    EFT.Communications.ENotificationIconType.Quest
                )));
            Log("stage=false-death-cue once=true raidUnaffected=true clipSeconds=" + (clip?.length ?? 0).ToString("0.00")
                + " noticeDelay=" + delay.ToString("0.00"));
        }
    }
}
