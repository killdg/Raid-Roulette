using System;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using HarmonyLib;
using RaidRoulette.Shared;

namespace RaidRoulette.TestingClient
{
    // Profile's constructor normally replaces the server tag with
    // CustomizationDogTag.GetItemClone(), which clones with the customization ID.
    // Every identical clone would therefore receive the same loot item ID.
    [HarmonyPatch(typeof(Profile), nameof(Profile.SetUpDogTagSlot))]
    internal static class CloneDogtagIdentity
    {
        private static bool Prefix(Profile __instance)
        {
            if (__instance.Info?.Settings?.Role is not (WildSpawnType.pmcBEAR or WildSpawnType.pmcUSEC))
            {
                return true;
            }

            var item = __instance.Inventory?.Equipment?.GetSlot(EquipmentSlot.Dogtag)?.ContainedItem;
            var tag = item?.GetItemComponent<DogtagComponent>();
            if (item == null || tag == null || !CloneTagIdentity.IsClone(item.Id) || tag.ProfileId != __instance.Id || tag
                .Nickname != __instance.Info.Nickname)
            {
                return true;
            }

            item.SpawnedInSession = true;
            Plugin.Trace("[RR-CLONE-TAG] retaining unique server tag profile=" + __instance.Id + " item=" + item.Id);
            return false;
        }
    }
}
