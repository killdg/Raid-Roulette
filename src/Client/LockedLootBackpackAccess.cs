using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class LockedLootBackpackAccess
    {
        // Grid stores the template's shared filter array in _filters. Replace that
        // reference on the local backpack instance, never edit the shared templates.
        private static readonly FieldInfo FiltersField = AccessTools.Field(typeof(Grid), "_filters");
        private readonly Dictionary<Grid, ItemFilter[]> originals = new();
        private readonly HashSet<string> caseTemplates = new(StringComparer.Ordinal);
        private float nextCheck;
        internal void SetTemplates(IEnumerable<string> templates)
        {
            caseTemplates.Clear();
            foreach (string template in templates)
            {
                caseTemplates.Add(template);
            }
        }

        internal void Tick(Player player)
        {
            if (caseTemplates.Count == 0 || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            if (player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem is not Backpack bag)
            {
                return;
            }

            foreach (Grid grid in bag.Grids)
            {
                if (originals.ContainsKey(grid))
                {
                    continue;
                }

                ItemFilter[] original = grid.Filters;
                var replacement = original.Select(filter => new ItemFilter { Filter = LockedLootBackpackPolicy.AllowCases((filter
                    .Filter ?? Array.Empty<MongoID>()).Select(id => id.ToString()), caseTemplates).Select(id => new MongoID(id))
                    .ToArray(), ExcludedFilter = LockedLootBackpackPolicy.KeepOtherExclusions((filter.ExcludedFilter ?? Array
                    .Empty<MongoID>()).Select(id => id.ToString()), caseTemplates).Select(id => new MongoID(id)).ToArray() }).ToArray();
                FiltersField.SetValue(grid, replacement);
                originals.Add(grid, original);
                Plugin.Trace($"[RR-KEYED-ROOM] backpack-cases-enabled bag={bag.TemplateId} allowed={caseTemplates.Count}; native size and recursive checks retained");
            }
        }

        internal void Restore()
        {
            foreach (var entry in originals)
            {
                FiltersField.SetValue(entry.Key, entry.Value);
            }

            originals.Clear();
            caseTemplates.Clear();
            nextCheck = 0;
        }
    }
}
