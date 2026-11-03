using System;
using System.Linq;
using EFT;
using EFT.UI;
using HarmonyLib;
using JsonType;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RaidRoulette.TestingClient
{
    internal sealed class FenceMapNotice
    {
        public string map = "";
        public string title = "";
    }

    internal static class FenceMapMarkers
    {
        // Replaced atomically by the existing settings response. Unity objects
        // are created only on the main thread by the location-button component.
        internal static FenceMapNotice[] Notices = Array.Empty<FenceMapNotice>();
        internal static bool Enabled;
        internal static string Group(string map)
        {
            return map.ToLowerInvariant() switch
            {
                "factory4_night" => "factory4_day",
                "sandbox_high" => "sandbox",
                _ => map.ToLowerInvariant()};
        }
    }

    public sealed class FenceLocationBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject? badge;
        private LocationButton owner = null!;
        private string map = "";
        private string tooltipText = "";
        private ESideType side;
        private float nextRefresh;
        private SimpleTooltip? tooltip;
        internal void Setup(LocationButton button, LocationSettings.Location location, ESideType chosenSide)
        {
            owner = button;
            map = location.Id;
            side = chosenSide;
            if (badge == null)
            {
                badge = new GameObject("RaidRouletteFenceBadge", typeof(RectTransform), typeof(Image));
                var source = (RectTransform)button._bossIcon.transform;
                var rect = (RectTransform)badge.transform;
                rect.SetParent(source.parent, false);
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.sizeDelta = new Vector2(44, 24);
                // The native boss/event icon reserves the space beside the
                // location label, even when it is hidden. Place our right edge
                // six UI units before its left edge; respecting both pivots
                // avoids overlap when the native icon changes size.
                float nativeLeftExtent = source.rect.width * source.pivot.x;
                float badgeRightExtent = rect.sizeDelta.x * (1f - rect.pivot.x);
                float horizontalOffset = -(nativeLeftExtent + badgeRightExtent + 6f);
                rect.anchoredPosition = source.anchoredPosition + new Vector2(horizontalOffset, 0f);
                badge.GetComponent<Image>().color = new Color(.08f, .08f, .06f, .95f);
                var caption = new GameObject("RR", typeof(RectTransform), typeof(Text));
                caption.transform.SetParent(rect, false);
                var captionRect = (RectTransform)caption.transform;
                captionRect.anchorMin = Vector2.zero;
                captionRect.anchorMax = Vector2.one;
                captionRect.offsetMin = captionRect.offsetMax = Vector2.zero;
                var text = caption.GetComponent<Text>();
                text.text = "[RR]";
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 17;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.9f, .85f, .65f);
                text.raycastTarget = false;
                badge.AddComponent<HoverTrigger>().Init(OnPointerEnter, OnPointerExit);
            }

            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .5f;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (badge == null)
            {
                return;
            }

            string[] titles = FenceMapMarkers.Enabled && side == ESideType.Pmc && !owner._lockedIcon.activeSelf
                ? FenceMapMarkers.Notices.Where(notice => FenceMapMarkers.Group(notice.map) == FenceMapMarkers.Group(map))
                .Select(notice => notice.title).Distinct().ToArray() : Array.Empty<string>();
            string updated = string.Join("\n", titles);
            if (updated != tooltipText)
            {
                tooltip?.Close();
                tooltip = null;
                tooltipText = updated;
            }

            badge.SetActive(titles.Length > 0);
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (tooltipText.Length > 0)
            {
                tooltip = ItemUiContext.Instance?.Tooltip;
                tooltip?.Show(tooltipText, maxWidth: 420);
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            tooltip?.Close();
            tooltip = null;
        }

        private void OnDisable()
        {
            tooltip?.Close();
            tooltip = null;
        }

        private void OnDestroy()
        {
            if (badge != null)
            {
                Destroy(badge);
            }
        }
    }

    [HarmonyPatch(typeof(LocationButton), nameof(LocationButton.Show))]
    internal static class FenceLocationMarkerPatch
    {
        private static void Postfix(LocationButton __instance, LocationSettings.Location location, ESideType chosenSideType)
        {
            var badge = __instance.GetComponent<FenceLocationBadge>() ?? __instance.gameObject.AddComponent<FenceLocationBadge>();
            badge.Setup(__instance, location, chosenSideType);
        }
    }

    [HarmonyPatch(typeof(EFT.Quests.Quest), nameof(EFT.Quests.Quest.SetStatus))]
    internal static class FenceQuestStatusRefresh
    {
        private static void Postfix()
        {
            if (FenceMapMarkers.Enabled)
            {
                Plugin.RefreshFenceMenus();
            }
        }
    }
}
