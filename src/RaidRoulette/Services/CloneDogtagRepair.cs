using RaidRoulette.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace RaidRoulette.Services
{
    public static class CloneDogtagRepair
    {
        // The broken tag uses the appearance template ID instead of an item ID.
        // Rebase only that known collision, retaining the item, location and metadata.
        public static Dictionary<string, string> Apply(PmcData pmc)
        {
            var changed = new Dictionary<string, string>();
            var items = pmc.Inventory?.Items;
            if (items == null || string.IsNullOrWhiteSpace(pmc.Info?.Nickname) || pmc.Customization?.DogTag is not { } appearance)
            {
                return changed;
            }

            foreach (var item in items.Where(i => i.Id == appearance && i.Upd is { SpawnedInSession: true, Dogtag: { } tag }
                && tag.Nickname == pmc.Info!.Nickname && tag.ProfileId != pmc.Id.ToString()))
            {
                var old = item.Id.ToString();
                item.Id = new MongoId(CloneTagIdentity.NewId());
                changed[old] = item.Id.ToString();
            }

            if (changed.Count == 0)
            {
                return changed;
            }

            foreach (var item in items)
            {
                if (item.ParentId != null && changed.TryGetValue(item.ParentId, out var parent))
                {
                    item.ParentId = parent;
                }
            }

            pmc.Inventory!.FastPanel = pmc.Inventory.FastPanel?.ToDictionary(p => p.Key, p => changed.TryGetValue(p.Value
                .ToString(), out var id) ? new MongoId(id) : p.Value);
            pmc.Inventory.FavoriteItems = pmc.Inventory.FavoriteItems?.Select(id => changed.TryGetValue(id.ToString(),
                 out var value) ? new MongoId(value) : id).ToArray();
            return changed;
        }
    }
}
