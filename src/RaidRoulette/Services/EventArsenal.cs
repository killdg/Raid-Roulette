using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace RaidRoulette.Services
{
    public sealed class EventArsenal(TemplateTable templates, GlobalTable globals, ICloner cloner)
    {
        public const string Rocket = "676bf44c5539167c3603e869";
        public const string Ak50 = "67d0576f29f580ebc10efd08";
        public static readonly string[] Ladder = ["pistol", "shotgun", "smg", "sniperRifle", "ak50", "assaultRifle",
             "machinegun", "rocket", "grenadeLauncher"];
        private readonly EventAttachments attachments = new(templates);
        private int Capacity(TemplateItem t) => (int)(t.Properties?.Cartridges ?? []).Select(c => c.MaxCount ?? 0).DefaultIfEmpty().Max();
        private bool Real(TemplateItem t) => t.Name?.Contains("infected", StringComparison.OrdinalIgnoreCase) != true;
        private List<Item>? Preset(MongoId id) => globals.ItemPresets.Values.FirstOrDefault(p => p.Encyclopedia == id)?.Items;
        private TemplateItem[] Mags(TemplateItem gun) => (gun.Properties?.Slots ?? []).Where(s => s.Name == "mod_magazine")
            .SelectMany(s => s.Properties?.Filters ?? []).SelectMany(f => f.Filter ?? []).Distinct().Where(templates.Items
            .ContainsKey).Select(id => templates.Items[id]).Where(t => Real(t) && (Capacity(t) > 0 || (t.Properties?.Slots
            ?? []).Any(s => s.Name?.StartsWith("camora") == true))).ToArray();
        private int Size(TemplateItem gun) => Mags(gun).Select(m => Math.Max(Capacity(m), (m.Properties?.Slots ?? [])
            .Count(s => s.Name?.StartsWith("camora") == true))).Concat((gun.Properties?.Cartridges ?? []).Select(c => (int)(c
            .MaxCount ?? 0))).DefaultIfEmpty((gun.Properties?.Chambers ?? []).Count()).Max();
        public TemplateItem Choose(string category)
        {
            if (category == "rocket")
            {
                return templates.Items[new MongoId(Rocket)];
            }

            if (category == "ak50")
            {
                return templates.Items[new MongoId(Ak50)];
            }

            var guns = templates.Items.Values.Where(t => t.Properties?.WeapClass == category && t.Properties
                .IsStationaryWeapon != true && t.Properties.DefAmmo.HasValue && Real(t) && Preset(t.Id)?.Any(i => i.Template == t
                .Id) == true).ToArray();
            if (guns.Length == 0)
            {
                throw new InvalidOperationException("No native preset for " + category);
            }

            int max = guns.Max(Size);
            var candidates = guns.Where(t => Size(t) == max).ToArray();
            return candidates[Random.Shared.Next(candidates.Length)];
        }

        public bool Available(string category)
        {
            try
            {
                return Choose(category).Properties?.DefAmmo.HasValue == true;
            }
            catch
            {
                return false;
            }
        }

        public List<Item> Kit(string category)
        {
            var gun = Choose(category);
            var kit = cloner.Clone(Preset(gun.Id)) ?? [new()
            {
                Id = new MongoId(),
                Template = gun.Id
            }

            ];
            var root = kit.First(i => i.Template == gun.Id);
            var remove = kit.Where(i => i.ParentId == root.Id.ToString() && (i.SlotId == "mod_magazine" || (gun.Properties?
                .Chambers ?? []).Any(c => c.Name == i.SlotId))).Select(i => i.Id.ToString()).ToHashSet();
            bool more;
            do
            {
                more = false;
                foreach (var i in kit)
                {
                    if (i.ParentId != null && remove.Contains(i.ParentId))
                    {
                        more |= remove.Add(i.Id.ToString());
                    }
                }
            }
            while (more);
            kit.RemoveAll(i => remove.Contains(i.Id.ToString()));
            var ids = kit.ToDictionary(i => i.Id.ToString(), _ => new MongoId());
            var oldRoot = root.Id.ToString();
            foreach (var i in kit)
            {
                string old = i.Id.ToString();
                i.Id = ids[old];
                if (old == oldRoot)
                {
                    i.ParentId = null;
                    i.SlotId = category == "pistol" ? "Holster" : "FirstPrimaryWeapon";
                    i.Location = null;
                }
                else if (i.ParentId != null && ids.TryGetValue(i.ParentId, out var parent))
                {
                    i.ParentId = parent.ToString();
                }
                else
                {
                    throw new InvalidOperationException("Invalid event weapon preset graph");
                }
            }

            var mags = Mags(gun);
            if (mags.Length > 0)
            {
                var mag = mags.OrderByDescending(m => Math.Max(Capacity(m), (m.Properties?.Slots ?? []).Count(s => s.Name?
                    .StartsWith("camora") == true))).First();
                kit.Add(new() { Id = new MongoId(), Template = mag.Id, ParentId = root.Id.ToString(), SlotId = "mod_magazine" });
            }

            attachments.Apply(kit);
            foreach (var holder in kit.ToArray())
            {
                var props = templates.Items[holder.Template].Properties;
                var slots = (props?.Cartridges ?? []).Concat(props?.Chambers ?? []).Concat((props?.Slots ?? []).Where(s => s.Name?
                    .StartsWith("camora") == true));
                foreach (var slot in slots)
                {
                    if (kit.Any(i => i.ParentId == holder.Id.ToString() && i.SlotId == slot.Name))
                    {
                        continue;
                    }

                    if (slot.MaxCount.HasValue && slot.MaxCount.Value <= 0)
                    {
                        continue;
                    }

                    int count = (int)(slot.MaxCount ?? 1);
                    kit.Add(new() { Id = new MongoId(), Template = gun.Properties!.DefAmmo!.Value, ParentId = holder.Id.ToString(),
                         SlotId = slot.Name, Location = 0, Upd = new() { StackObjectsCount = count, SpawnedInSession = true } });
                }
            }

            kit.Remove(root);
            kit.Insert(0, root);
            return kit;
        }

        public BotBase CopyAi(BotBase original, string category, bool allCombatants = false)
        {
            string role = original.Info?.Settings?.Role ?? "";
            if (allCombatants)
            {
                if (string.IsNullOrEmpty(role) || role.ToLowerInvariant()is "player" or "playerscav" or "shooterbtr")
                {
                    return original;
                }
            }
            else if (!RaidRulesService.IsScav(role) && !role.Equals("pmcUSEC", StringComparison.OrdinalIgnoreCase) && !role
                .Equals("pmcBEAR", StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }

            var copy = cloner.Clone(original)!;
            var items = copy.Inventory?.Items ?? throw new InvalidOperationException("AI items missing");
            var equipment = copy.Inventory!.Equipment!.Value;
            var remove = items.Where(i => templates.Items.TryGetValue(i.Template, out var t) && (!string.IsNullOrEmpty(t
                .Properties?.WeapClass) || Capacity(t) > 0 || (category != "ak50" && t.Properties?.ThrowType != null))).Select(i =>
                 i.Id.ToString()).ToHashSet();
            bool more;
            do
            {
                more = false;
                foreach (var i in items)
                {
                    if (i.ParentId != null && remove.Contains(i.ParentId))
                    {
                        more |= remove.Add(i.Id.ToString());
                    }
                }
            }
            while (more);
            items.RemoveAll(i => remove.Contains(i.Id.ToString()));
            var kit = Kit(category);
            kit[0].ParentId = equipment.ToString();
            items.AddRange(kit);
            copy.Inventory.FastPanel = copy.Inventory.FastPanel?.Where(p => !remove.Contains(p.Value.ToString()))
                .ToDictionary(p => p.Key, p => p.Value);
            copy.Inventory.FavoriteItems = copy.Inventory.FavoriteItems?.Where(i => !remove.Contains(i.ToString())).ToArray();
            return copy;
        }
    }
}
