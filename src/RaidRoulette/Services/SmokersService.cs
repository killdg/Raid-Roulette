using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace RaidRoulette.Services
{
    public sealed class SmokersService(TemplateTable templates, ICloner cloner)
    {
        public const string Smoke = "617aa4dd8166f034d57de9c5";
        public const string Pockets = "64cbd95a29b9b4283e216ff5";
        public bool Available => templates.Items.ContainsKey(new MongoId(Smoke)) && templates.Items
            .TryGetValue(new MongoId(Pockets), out var p) && p.Properties?.Grids?.Any() == true;

        public BotBase CopyForResponse(BotBase original)
        {
            // Only generated AI response profiles have a native bot role. Never resolve or edit a player save.
            string role = original.Info?.Settings?.Role ?? "";
            if (string.IsNullOrEmpty(role) || role.Equals("playerscav", StringComparison.OrdinalIgnoreCase) || role
                .Equals("player", StringComparison.OrdinalIgnoreCase))
            {
                return original;
            }

            if (!Available)
            {
                throw new InvalidOperationException("Smoke grenade or roomy native pockets template is unavailable.");
            }

            var copy = cloner.Clone(original)!;
            var items = copy.Inventory?.Items ?? throw new InvalidOperationException("AI inventory missing.");
            var root = copy.Inventory!.Equipment ?? throw new InvalidOperationException("AI equipment root missing.");
            var remove = items.Where(i => templates.Items.TryGetValue(i.Template, out var t) && t.Properties?
                .ThrowType != null).Select(i => i.Id.ToString()).ToHashSet();
            bool changed;
            do
            {
                changed = false;
                foreach (var i in items)
                {
                    if (i.ParentId != null && remove.Contains(i.ParentId))
                    {
                        changed |= remove.Add(i.Id.ToString());
                    }
                }
            }
            while (changed);
            items.RemoveAll(i => remove.Contains(i.Id.ToString()));
            copy.Inventory.FastPanel = copy.Inventory.FastPanel?.Where(p => !remove.Contains(p.Value.ToString()))
                .ToDictionary(p => p.Key, p => p.Value);
            copy.Inventory.FavoriteItems = copy.Inventory.FavoriteItems?.Where(i => !remove.Contains(i.ToString())).ToArray();
            var pockets = items.FirstOrDefault(i => i.ParentId == root.ToString() && i.SlotId == "Pockets");
            if (pockets == null)
            {
                pockets = new()
                {
                    Id = new MongoId(),
                    ParentId = root.ToString(),
                    SlotId = "Pockets"
                };
                items.Add(pockets);
            }

            pockets.Template = new MongoId(Pockets);
            var carried = items.Where(i => i.ParentId == pockets.Id.ToString()).ToArray();
            int count = Random.Shared.Next(5, 11);
            var smokes = Enumerable.Range(0, count).Select(_ => new Item { Id = new MongoId(), Template = new MongoId(Smoke),
                 ParentId = pockets.Id.ToString(), Upd = new() { SpawnedInSession = true, StackObjectsCount = 1 } }).ToArray();
            var grids = templates.Items[pockets.Template].Properties!.Grids!.Select(g => (Grid: g, Used: new bool[g.Properties?
                .CellsH ?? 0, g.Properties?.CellsV ?? 0])).ToArray();
            // Repack existing pocket contents and smoke into native grids; never stack grenades or overlap cells.
            foreach (var item in carried.Concat(smokes).OrderByDescending(i => Size(i).Width * Size(i).Height))
            {
                var size = Size(item);
                bool placed = false;
                foreach (var(grid, used)in grids)
                {
                    for (int rotation = 0; rotation < 2 && !placed; rotation++)
                    {
                        int w = rotation == 0 ? size.Width : size.Height;
                        int h = rotation == 0 ? size.Height : size.Width;
                        for (int y = 0; y <= used.GetLength(1) - h && !placed; y++)
                        {
                            for (int x = 0; x <= used.GetLength(0) - w && !placed; x++)
                            {
                                bool free = true;
                                for (int dx = 0; dx < w; dx++)
                                {
                                    for (int dy = 0; dy < h; dy++)
                                    {
                                        free &= !used[x + dx, y + dy];
                                    }
                                }

                                if (!free)
                                {
                                    continue;
                                }

                                for (int dx = 0; dx < w; dx++)
                                {
                                    for (int dy = 0; dy < h; dy++)
                                    {
                                        used[x + dx, y + dy] = true;
                                    }
                                }

                                item.SlotId = grid.Name;
                                item.Location = new
                                {
                                    x,
                                    y,
                                    r = rotation,
                                    isSearched = true
                                };
                                placed = true;
                            }
                        }
                    }

                    if (placed)
                    {
                        break;
                    }
                }

                if (!placed)
                {
                    throw new InvalidOperationException("AI pocket contents cannot fit alongside smoke supply; original AI retained.");
                }
            }

            items.AddRange(smokes);
            return copy;
        }

        private (int Width, int Height) Size(Item item) => templates.Items.TryGetValue(item.Template, out var t) ? (Math
            .Max(1, t.Properties?.Width ?? 1), Math.Max(1, t.Properties?.Height ?? 1)) : (1, 1);
    }
}
