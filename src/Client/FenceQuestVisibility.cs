using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EFT.Quests;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    internal static class FenceQuestIds
    {
        private static readonly HashSet<string> ids = Create();
        private static HashSet<string> Create()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            using var hash = SHA256.Create();
            foreach (RaidChoice choice in Enum.GetValues(typeof(RaidChoice)))
            {
                string eventId = RaidChoiceNames.EventId(choice.ToString());
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes("killdg/rr/fence/v1/" + eventId + "/quest"));
                result.Add(BitConverter.ToString(digest, 0, 12).Replace("-", "").ToLowerInvariant());
            }

            return result;
        }

        internal static bool Visible(Quest quest)
        {
            return FenceMapMarkers.Enabled || !ids.Contains(quest.Template.Id);
        }

        internal static bool Owns(Quest quest) => ids.Contains(quest.Template.Id);
    }

    // Always cache the definitions at login; hiding them server-side would make
    // enabling the setting unable to reveal a quest until the next login.
    [HarmonyPatch(typeof(QuestBook), nameof(QuestBook.CanView))]
    internal static class FenceQuestViewGate
    {
        private static void Postfix(Quest quest, ref bool __result)
        {
            __result &= FenceQuestIds.Visible(quest);
        }
    }

    [HarmonyPatch(typeof(QuestController), nameof(QuestController.NewQuests))]
    internal static class FenceNewQuestGate
    {
        private static void Postfix(ref IEnumerable<Quest> __result)
        {
            __result = __result.Where(FenceQuestIds.Visible);
        }
    }
}
