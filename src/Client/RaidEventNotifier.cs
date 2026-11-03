using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.UI.SessionEnd;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class RaidEventNotifier
    {
        internal static RaidEventNotifier? Current;
        private readonly RaidEventNotice notice = new();
        private readonly RaidEventNotice startNotice = new();
        private GameWorld? world;
        private string? pending;
        private float showAt;
        private float expireAt;
        private ConfigEntry<bool> enabled = null!;
        private ConfigEntry<bool> before = null!;
        private bool showBefore;
        public void Bind(ConfigFile config)
        {
            Current = this;
            before = config.Bind(
                "6. Raid summary",
                "Show raid event before raid",
                false,
                "Reveal the event once when the raid begins. Off by default so the event stays a surprise. " +
                "Frozen when the raid world loads."
            );
            enabled = config.Bind(
                "6. Raid summary",
                "Show raid event after raid",
                true,
                "Show the actual completed raid event as a native notification on the post-raid screen, including Normal raids."
            );
        }

        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (current != null && !ReferenceEquals(world, current))
            {
                world = current;
                notice.Begin();
                startNotice.Begin();
                showBefore = before.Value;
                pending = null;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (current != null && game?.GameTimer?.StartDateTime != null)
            {
                notice.Observe(Plugin.RaidContext);
                if (showBefore && game.Status == GameStatus.Started && game.GameTimer.PastTime.TotalSeconds >= 1
                    && Singleton<NotificationManager>.Instantiated)
                {
                    startNotice.Observe(Plugin.RaidContext);
                    var message = startNotice.Consume();
                    if (message != null)
                    {
                        NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Long, ENotificationIconType
                            .Quest);
                        Plugin.Trace("[RR-NOTICE] stage=start message=" + message);
                    }
                }
            }

            if (pending == null)
            {
                return;
            }

            if (!enabled.Value || Time.unscaledTime > expireAt)
            {
                pending = null;
                return;
            }

            if (Time.unscaledTime < showAt || !Singleton<NotificationManager>.Instantiated)
            {
                return;
            }

            NotificationManager.DisplayMessageNotification(pending, ENotificationDurationType.Long, ENotificationIconType.Quest);
            Plugin.Trace("[RR-NOTICE] stage=shown message=" + pending);
            pending = null;
        }

        internal void Queue()
        {
            string? message = notice.Consume();
            if (message == null || !enabled.Value)
            {
                return;
            }

            message += LabStaffExperience.Summary;
            pending = message;
            showAt = Time.unscaledTime + 1;
            expireAt = showAt + 60;
            Plugin.Trace("[RR-NOTICE] stage=queued message=" + message);
        }
    }

    [HarmonyPatch]
    internal static class RaidSummaryNotification
    {
        private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(SessionResultExitStatus))
            .Single(m => m.Name == "Show" && m.GetParameters().Length == 7);
        private static void Postfix() => RaidEventNotifier.Current?.Queue();
    }
}
