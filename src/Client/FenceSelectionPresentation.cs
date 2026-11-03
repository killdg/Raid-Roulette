using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.Quests;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    [HarmonyPatch(typeof(QuestControllerClientBackend), nameof(QuestControllerClientBackend.AcceptQuest))]
    internal static class FenceSelectionPresentation
    {
        private static void Postfix(QuestControllerClientBackend __instance, Quest quest, bool runNetworkTransaction,
             ref Task<IResult> __result)
        {
            if (runNetworkTransaction && FenceMapMarkers.Enabled && FenceQuestIds.Owns(quest))
            {
                __result = FinishAccept(__result, __instance, quest);
            }
        }

        private static async Task<IResult> FinishAccept(Task<IResult> native, QuestControllerClientBackend controller, Quest selected)
        {
            IResult result = await native;
            if (!result.Succeed)
            {
                return result;
            }

            // The server already retires older selectors. Native accept updates
            // only the newly selected client Quest object, leaving older cached
            // objects visually Started. Reconcile those after a successful reply.
            // No networking, rewards, campaign edits or status-evaluation hook:
            // this runs once per explicit successful RR accept, on Unity's context.
            Quest[] previous = controller.Quests.Where(quest => quest != selected && FenceQuestIds.Owns(quest) && (quest
                .QuestStatus == EQuestStatus.Started || quest.QuestStatus == EQuestStatus.AvailableForFinish)).ToArray();
            foreach (Quest quest in previous)
            {
                quest.SetStatus(EQuestStatus.AvailableForStart, notify: false, fromServer: true);
            }

            Plugin.RefreshFenceMenus();
            if (Singleton<NotificationManager>.Instantiated)
            {
                string title = selected.Template.Name.Localized();
                string message = "Next raid: " + title;
                if (previous.Length > 0)
                {
                    message += " — replaced " + string.Join(", ", previous.Select(quest => quest.Template.Name.Localized()));
                }

                NotificationManager.DisplayMessageNotification(message, ENotificationDurationType.Long, ENotificationIconType.Quest);
            }

            return result;
        }
    }
}
