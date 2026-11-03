using Comfort.Common;
using EFT;
using EFT.Quests;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    internal static class FenceNotificationPolicy
    {
        internal static bool Allow(Quest quest)
        {
            if (!FenceQuestIds.Owns(quest))
            {
                return true;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            return game?.GameTimer?.StartDateTime != null && game.Status == GameStatus.Started && Plugin.Local != null;
        }
    }

    // These methods emit presentation only: native NotificationQuest includes
    // its own sound. Quest status evaluation, inventory transactions, connector
    // updates and the server's end-raid processing run outside these hooks.
    [HarmonyPatch(typeof(QuestControllerClient), nameof(QuestControllerClient.TryNotifyConditionalStatusChanged))]
    internal static class FenceLoadingStatusNotification
    {
        private static bool Prefix(Quest quest) => FenceNotificationPolicy.Allow(quest);
    }

    [HarmonyPatch(typeof(QuestControllerClient), nameof(QuestControllerClient.TryNotifyConditionChanged))]
    internal static class FenceLoadingConditionNotification
    {
        private static bool Prefix(Quest quest) => FenceNotificationPolicy.Allow(quest);
    }
}
