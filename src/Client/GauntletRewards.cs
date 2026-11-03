using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace RaidRoulette.TestingClient
{
    internal static class GauntletRewards
    {
        private const string Bitcoin = "59faff1d86f7746c51718c9c";
        private const string Attack2 = "5ab8ebf186f7742d8b372e80";
        internal static int Ensure(Player player, int minimum)
        {
            int existing = player.Profile.Inventory.Equipment.GetAllItems().Where(item => item.TemplateId
                .ToString() == Bitcoin).Sum(item => item.StackObjectsCount);
            int missing = Math.Max(0, minimum - existing);
            for (int index = 0; index < missing; index++)
            {
                var item = Singleton<ItemFactory>.Instance.CreateItem(NewId(), Bitcoin, null);
                item.SpawnedInSession = true;
                if (!Store(player, item))
                {
                    var slot = player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack);
                    if (slot.ContainedItem == null)
                    {
                        var bag = Singleton<ItemFactory>.Instance.CreateItem(NewId(), Attack2, null);
                        bag.SpawnedInSession = true;
                        if (slot.AddWithoutRestrictions(bag).Succeeded)
                        {
                            slot.CreateItemAddress().RaiseAddEvent(bag, CommandStatus.Succeed, player.InventoryController);
                        }
                    }

                    if (!Store(player, item))
                    {
                        Plugin.Trace("[RR-GAUNTLET] reward waiting for inventory space profile=" + player.ProfileId + " remaining="
                            + (missing - index));
                        return missing - index;
                    }
                }
            }

            int stored = player.Profile.Inventory.Equipment.GetAllItems().Where(item => item.TemplateId.ToString() == Bitcoin)
                .Sum(item => item.StackObjectsCount);
            Plugin.Trace("[RR-GAUNTLET] reward inventory verified profile=" + player.ProfileId + " bitcoins=" + stored
                + " required=" + minimum);
            return Math.Max(0, minimum - stored);
        }

        private static string NewId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 24);
        }

        private static bool Store(Player player, Item item)
        {
            foreach (var slot in new[]
            {
                EquipmentSlot.Backpack,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Pockets
            }

            )
            {
                if (player.Profile.Inventory.Equipment.GetSlot(slot).ContainedItem is not CompoundItem container)
                {
                    continue;
                }

                foreach (var grid in container.Grids)
                {
                    var address = grid.FindLocationForItem(item);
                    if (address != null && address.AddWithoutRestrictions(item).Succeeded)
                    {
                        address.RaiseAddEvent(item, CommandStatus.Succeed, player.InventoryController);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
