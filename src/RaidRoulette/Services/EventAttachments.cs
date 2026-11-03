using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace RaidRoulette.Services
{
    // Keep the native functional preset intact. Fill its accessory slots, including
    // adapter/rail/ring chains, from the installed template's own compatibility lists.
    public sealed class EventAttachments(TemplateTable templates)
    {
        private readonly Dictionary<MongoId, HashSet<MongoId>> ancestry = new();
        private readonly Dictionary<Slot, TemplateItem[]> pools = new();
        private HashSet<MongoId> Family(MongoId id)
        {
            if (ancestry.TryGetValue(id, out var known))
            {
                return known;
            }

            var result = new HashSet<MongoId>();
            var current = id;
            while (result.Add(current) && templates.Items.TryGetValue(current, out var item))
            {
                current = item.Parent;
            }

            return ancestry[id] = result;
        }

        private static bool Accessory(string? name) => name != null && (name.StartsWith("mod_scope") || name
            .StartsWith("mod_mount") || name.StartsWith("mod_tactical") || name.StartsWith("mod_foregrip") || name
            .StartsWith("mod_muzzle") || name.StartsWith("mod_stock") || name.StartsWith("mod_handguard"));
        private TemplateItem[] Pool(Slot slot)
        {
            if (pools.TryGetValue(slot, out var pool))
            {
                return pool;
            }

            var allowed = (slot.Properties?.Filters ?? []).SelectMany(f => f.Filter ?? []).ToHashSet();
            return pools[slot] = templates.Items.Values.Where(t => t.Properties != null && string.IsNullOrEmpty(t.Properties
                .WeapClass) && t.Properties.ThrowType == null && t.Name?.Contains("infected", StringComparison
                .OrdinalIgnoreCase) != true && Family(t.Id).Overlaps(allowed)).ToArray();
        }

        private bool Compatible(TemplateItem candidate, List<Item> kit)
        {
            var family = Family(candidate.Id);
            return kit.All(i => !((templates.Items[i.Template].Properties?.ConflictingItems ?? []).Overlaps(family))
                && !((candidate.Properties?.ConflictingItems ?? []).Overlaps(Family(i.Template))));
        }

        public void Apply(List<Item> kit)
        {
            var root = kit.Single(i => i.ParentId == null);
            // The largest magazine may conflict with a default stock (for example M700).
            // Rebuild that preset branch around the magazine rather than issuing an invalid gun.
            foreach (var existing in kit.ToArray())
            {
                if (existing == root || existing.SlotId == "mod_magazine" || !kit.Contains(existing))
                {
                    continue;
                }

                if (Compatible(templates.Items[existing.Template], kit.Where(i => i.Id != existing.Id).ToList()))
                {
                    continue;
                }

                var removed = new HashSet<string>
                {
                    existing.Id.ToString()
                };
                bool more;
                do
                {
                    more = false;
                    foreach (var child in kit)
                    {
                        if (child.ParentId != null && removed.Contains(child.ParentId))
                        {
                            more |= removed.Add(child.Id.ToString());
                        }
                    }
                }
                while (more);
                kit.RemoveAll(i => removed.Contains(i.Id.ToString()));
            }

            if (!Fill(root, kit, 0, []))
            {
                throw new InvalidOperationException("Cannot complete compatible accessories for " + templates.Items[root.Template].Name);
            }
        }

        private bool Fill(Item parent, List<Item> kit, int depth, HashSet<MongoId> chain)
        {
            if (depth > 8 || !chain.Add(parent.Template))
            {
                return false;
            }

            try
            {
                foreach (var slot in templates.Items[parent.Template].Properties?.Slots ?? [])
                {
                    if (slot.Name?.StartsWith("camora") == true || slot.Name?.StartsWith("patron") == true)
                    {
                        continue;
                    }

                    var child = kit.FirstOrDefault(i => i.ParentId == parent.Id.ToString() && i.SlotId == slot.Name);
                    if (child != null)
                    {
                        if (!Fill(child, kit, depth + 1, chain) && slot.Required == true)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (slot.Required != true && !Accessory(slot.Name))
                    {
                        continue;
                    }

                    bool added = false;
                    foreach (var candidate in Pool(slot).OrderBy(_ => Random.Shared.Next()))
                    {
                        if (kit.Count >= 64 || chain.Contains(candidate.Id) || !Compatible(candidate, kit))
                        {
                            continue;
                        }

                        int before = kit.Count;
                        var item = new Item
                        {
                            Id = new MongoId(),
                            Template = candidate.Id,
                            ParentId = parent.Id.ToString(),
                            SlotId = slot.Name
                        };
                        kit.Add(item);
                        if (Fill(item, kit, depth + 1, chain))
                        {
                            added = true;
                            break;
                        }

                        kit.RemoveRange(before, kit.Count - before);
                    }

                    if (!added && slot.Required == true)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                chain.Remove(parent.Template);
            }
        }
    }
}
