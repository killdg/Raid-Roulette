using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.UI;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    [HarmonyPatch(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableInteractionState))]
    internal static class LockedLootPickup
    {
        private static readonly Dictionary<string, float> nextLog = new();
        private static void Postfix(GamePlayerOwner owner, Item rootItem, IPlayer lootItemLastOwner, ref AvailableInteractionState __result)
        {
            if (!Plugin.IsLocal(owner.Player) || HighValueRoomDirector.Current?.OwnsSpawnedItem(rootItem.Id) != true
                || !rootItem.IsContainer || __result.Actions?.Any(action => action.Name == "Take") == true)
            {
                return;
            }

            var controller = owner.Player.InventoryController;
            if (controller.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem is not Backpack bag)
            {
                return;
            }

            bool log = !nextLog.TryGetValue(rootItem.Id, out float at) || Time.unscaledTime >= at;
            if (log)
            {
                nextLog[rootItem.Id] = Time.unscaledTime + 10;
                Plugin.Trace($"[RR-CASE-PICKUP] template={rootItem.TemplateId} size={rootItem.CalculateCellSize()} pin=" +
                    $"{rootItem.PinLockState} hands={owner.Player.HandsController.SupportPickup()}");
            }

            if (!owner.Player.HandsController.SupportPickup())
            {
                return;
            }

            foreach (Grid grid in bag.Grids)
            {
                var location = grid.FindFreeSpace(rootItem);
                if (log)
                {
                    Plugin.Trace($"[RR-CASE-PICKUP] bag={bag.TemplateId} grid={grid.GridWidth}x{grid.GridHeight} compatible=" +
                        $"{grid.CheckCompatibility(rootItem)} freeSlot={location != null}");
                }

                if (location == null)
                {
                    continue;
                }

                var move = ItemManipulator.Move(rootItem, grid.CreateItemAddress(location), controller, true);
                bool allowed = move.Succeeded && controller.CanExecute(move.Value);
                if (log)
                {
                    Plugin.Trace("[RR-CASE-PICKUP] direct-backpack-move=" + allowed + (move.Failed ? " error=" + move.Error : ""));
                }

                if (!allowed)
                {
                    continue;
                }

                // The normal quick-pickup selector may omit this backpack. Add an
                // action only after the exact native move passes simulation and
                // controller validation. smethod_10 is EFT's ordinary take-action
                // executor, retaining its pickup animation and network transaction.
                __result.Actions ??= new List<InteractionAction>();
                __result.Actions.Add(new InteractionAction { Name = "Take", TargetName = "Locked Loot case", Action = () =>
                     InteractionContextHelper.smethod_10(owner.Player, move.Value, rootItem, lootItemLastOwner) });
                return;
            }
        }
    }
}
