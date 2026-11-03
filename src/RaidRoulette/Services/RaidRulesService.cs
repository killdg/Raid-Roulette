using RaidRoulette.Configuration;
using RaidRoulette.Runtime;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;
using Customization = SPTarkov.Server.Core.Models.Eft.Common.Tables.Customization;
using ModsPool = System.Collections.Generic.Dictionary<SPTarkov.Server.Core.Models.Common.MongoId, System
    .Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<SPTarkov.Server.Core.Models.Common.MongoId>>>;

namespace RaidRoulette.Services
{
    public sealed class RaidRulesService
    {
        private readonly TemplateTable templates;
        private readonly BotTable bots;
        private readonly LocationTable locations;
        private readonly ICloner cloner;
        private readonly Action<string> warn;
        private readonly Dictionary<EquipmentSlots, Dictionary<MongoId, double>> equipment = new();
        private readonly Dictionary<string, Dictionary<MongoId, double>> ammo = new();
        private readonly ModsPool mods = new();
        private readonly HashSet<MongoId> weapons = new();
        public RaidRulesService(TemplateTable templates, BotTable bots, LocationTable locations, ICloner cloner, Action<string> warn)
        {
            this.templates = templates;
            this.bots = bots;
            this.locations = locations;
            this.cloner = cloner;
            this.warn = warn;
            foreach (var bot in bots.Types.Values.Where(b => b?.BotInventory is not null))
            {
                var inventory = bot!.BotInventory;
                foreach (var slot in inventory.Equipment ?? [])
                {
                    if (!equipment.TryGetValue(slot.Key, out var pool))
                    {
                        equipment[slot.Key] = pool = new();
                    }

                    foreach (var item in slot.Value ?? [])
                    {
                        if (templates.Items.ContainsKey(item.Key))
                        {
                            pool.TryAdd(item.Key, 1);
                        }
                    }
                }

                foreach (var caliber in inventory.Ammo ?? [])
                {
                    if (!ammo.TryGetValue(caliber.Key, out var pool))
                    {
                        ammo[caliber.Key] = pool = new();
                    }

                    foreach (var item in caliber.Value ?? [])
                    {
                        if (templates.Items.ContainsKey(item.Key))
                        {
                            pool.TryAdd(item.Key, 1);
                        }
                    }
                }

                foreach (var parent in inventory.Mods ?? [])
                {
                    if (!mods.TryGetValue(parent.Key, out var slots))
                    {
                        mods[parent.Key] = slots = new();
                    }

                    foreach (var slot in parent.Value)
                    {
                        if (!slots.TryGetValue(slot.Key, out var pool))
                        {
                            slots[slot.Key] = pool = new();
                        }

                        pool.UnionWith(slot.Value);
                    }
                }
            }

            foreach (var slot in new[]
            {
                EquipmentSlots.FirstPrimaryWeapon,
                EquipmentSlots.SecondPrimaryWeapon,
                EquipmentSlots.Holster
            }

            )
            {
                if (equipment.TryGetValue(slot, out var pool))
                {
                    weapons.UnionWith(pool.Keys);
                }
            }
        }

        public static bool IsScav(string role) => role.ToLowerInvariant()is "assault" or "assaultgroup" or "cursedassault" or "marksman";
        public static bool IsOrdinary(BotGenerationDetails details) => !details.IsPlayerScav && (details.IsPmc
            || IsScav(details.Role) || details.Role.Equals("pmcusec", StringComparison.OrdinalIgnoreCase) || details.Role
            .Equals("pmcbear", StringComparison.OrdinalIgnoreCase));
        private BotType? Template(string role) => bots.Types.TryGetValue(role, out var value) ? value : bots.Types
            .FirstOrDefault(x => x.Key.Equals(role, StringComparison.OrdinalIgnoreCase)).Value;
        private LocationBase? Map(string? name)
        {
            try
            {
                return name is null ? null : locations.GetLocation(name)?.Base;
            }
            catch
            {
                return null;
            }
        }

        public static List<string> Zones(LocationBase map) => (map.SpawnPointParams ?? []).Select(x => x.BotZoneName)
            .Concat((map.BossLocationSpawn ?? []).Select(x => x.BossZone)).Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => x!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        private Dictionary<MongoId, double> WeaponPool(params string[] classes) => weapons.Where(id => templates.Items
            .TryGetValue(id, out var t) && classes.Contains(t.Properties?.WeapClass, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(id => id, _ => 1d);
        private List<MongoId> DrumOptions(MongoId weapon, int minimum) => (templates.Items.GetValueOrDefault(weapon)?
            .Properties?.Slots ?? []).Where(s => s.Name == "mod_magazine").SelectMany(s => s.Properties?.Filters ?? [])
            .SelectMany(f => f.Filter ?? []).Where(id => templates.Items.TryGetValue(id, out var mag) && mag.Name?
            .Contains("infected", StringComparison.OrdinalIgnoreCase) != true && (mag.Properties?.Cartridges ?? []).Any(c => (c
            .MaxCount ?? 0) >= minimum)).Distinct().ToList();
        private Dictionary<MongoId, double> DrumWeapons(int minimum) => weapons.Where(id => templates.Items[id].Properties?
            .IsStationaryWeapon != true && templates.Items[id].Properties?.WeapClass is not ("pistol" or "revolver")
            && DrumOptions(id, minimum).Count > 0).ToDictionary(x => x, _ => 1d);
        internal int ArmorClass(MongoId id) => templates.Items.GetValueOrDefault(id)?.Properties?.ArmorClass ?? 0;
        private int HeadwearClass(MongoId id)
        {
            // Modern helmets store protection in required Helmet_top/back/ears
            // inserts; the root itself has class zero. Optional face shields do
            // not determine the helmet's class.
            var integratedClasses = (templates.Items.GetValueOrDefault(id)?.Properties?.Slots ?? []).Where(slot => slot
                .Required == true && slot.Name?.StartsWith("Helmet_", StringComparison.OrdinalIgnoreCase) == true)
                .SelectMany(slot => slot.Properties?.Filters ?? []).SelectMany(filter => filter.Filter ?? []).Select(ArmorClass);
            return Math.Max(ArmorClass(id), integratedClasses.DefaultIfEmpty(0).Max());
        }

        internal HashSet<MongoId> GearedPlateCandidates(TemplateItem armor, string slotName, int minimum)
        {
            // Use the carrier's own slot filters, not the global armor pool.
            // Soft inserts and helmet components retain their native handling.
            if (!slotName.Contains("plate", StringComparison.OrdinalIgnoreCase))
            {
                return[];
            }

            return (armor.Properties?.Slots ?? []).Where(slot => string.Equals(slot.Name, slotName, StringComparison
                .OrdinalIgnoreCase)).SelectMany(slot => slot.Properties?.Filters ?? []).SelectMany(filter => filter.Filter ?? [])
                .Where(template => ArmorClass(template) >= minimum).ToHashSet();
        }

        private int BodyArmorClass(MongoId id) => Math.Max(
            ArmorClass(id),
            (templates.Items.GetValueOrDefault(id)?.Properties?.Slots ?? []).Where(s => s.Name?.Contains("plate",
                 StringComparison.OrdinalIgnoreCase) == true).SelectMany(s => s.Properties?.Filters ?? []).SelectMany(f => f
                .Filter ?? []).Select(ArmorClass).DefaultIfEmpty(0).Max()
        );
        private Dictionary<MongoId, double> ArmorPool(EquipmentSlots slot, int minimum) => (equipment
            .GetValueOrDefault(slot) ?? []).Where(x => BodyArmorClass(x.Key) >= minimum).ToDictionary(x => x.Key, _ => 1d);
        private Dictionary<MongoId, double> GearedWeapons(V2Settings settings) => weapons.Where(id => templates.Items
            .TryGetValue(id, out var t) && (settings.GearedWeaponNameTokens ?? []).Any(token => !string
            .IsNullOrWhiteSpace(token) && t.Name?.Contains(token, StringComparison.OrdinalIgnoreCase) == true) && t.Properties?
            .WeapClass is "assaultRifle" or "marksmanRifle" or "sniperRifle").ToDictionary(x => x, _ => 1d);
        private static bool HasClothing(BotType? template) => template?.BotAppearance?.Body?.Count > 0 && template
            .BotAppearance.Feet?.Count > 0;
        public bool CanRun(string id, string? location, V2Settings settings)
        {
            var map = Map(location);
            return id switch
            {
                "time_warp" or "stormtide" => map != null && new[]
                {
                    "bigmap",
                    "woods",
                    "shoreline",
                    "lighthouse",
                    "rezervbase",
                    "interchange",
                    "tarkovstreets",
                    "sandbox",
                    "sandbox_high"
                }.Contains(map.Id.ToLowerInvariant()),
                "snowball" or "trials" or "extreme_trials" or "guard_trials" or "total_trial" => map != null && Zones(map).Count > 0,
                "guarded" => map != null && Zones(map).Count > 0 && GuardRoles().Length > 0,
                "scav_concentrate" or "pmc_sweep" or "raider_assault" => map != null && Zones(map).Count > 0,
                "outbreak" => map != null && Zones(map).Count > 0 && Template("infectedassault") != null,
                "extreme_overwatch" => map != null && OverwatchPlan.Zones(map).Length > 0,
                "no_safe_space" => map != null,
                "blue_day" => map != null && new[]
                {
                    "bigmap",
                    "woods",
                    "shoreline",
                    "lighthouse",
                    "rezervbase",
                    "interchange",
                    "tarkovstreets",
                    "sandbox",
                    "sandbox_high"
                }.Contains(map.Id.ToLowerInvariant()),
                "get_handy" => WeaponPool("pistol", "revolver").Count > 0,
                "i_have_a_shot" => WeaponPool("shotgun").Count > 0,
                "marksman" => WeaponPool("sniperRifle").Count > 0,
                "explosive_scav_units" => WeaponPool("grenadeLauncher").Count > 0,
                "overloaded" => DrumWeapons(settings.MinimumMagazineCapacity).Count > 0,
                "geared" => GearedWeapons(settings).Count > 0 && ArmorPool(EquipmentSlots.ArmorVest, settings.GearedBodyArmorClass)
                    .Count > 0,
                "posers_everywhere" => Template("bosskilla")?.BotInventory != null && HasClothing(Template("bosskilla")),
                "fake_cult" => HasClothing(Template("sectantwarrior")) || HasClothing(Template("sectantpriest")),
                "matrix_glitch" => true,
                _ => true
            };
        }

        // Only modifies the raid-specific clone, never the map database.
        public void ApplyMap(LocationBase map, string id, V2Settings settings)
        {
            map.Waves ??= [];
            map.BossLocationSpawn ??= [];
            switch (id)
            {
                case "snowball":
                case "trials":
                case "extreme_trials":
                case "guard_trials":
                case "total_trial":
                    TrialsRaidDuration.Apply(map, id);
                    // This is the generated raid copy, after seasonal schedules
                    // have been assembled. The client alone requests event actors.
                    map.Waves = [];
                    map.BossLocationSpawn = [];
                    break;
                case "scav_concentrate":
                    // Build every replacement entry before touching native schedules. Native boss-group
                    // scheduling supports ordinary assault roles, and has timed activation on PvE.
                    var zone = Zones(map).OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
                    if (string.IsNullOrEmpty(zone))
                    {
                        break; // keep baseline waves when setup is unavailable
                    }

                    var groups = new List<BossLocationSpawn>();
                    int population = Math.Clamp((int)((map.BotMaxPvE > 0 ? map.BotMaxPvE : map.BotMax) ?? 20), 1, settings.MaxAliveBots);
                    int remaining = population;
                    while (remaining > 0)
                    {
                        int count = Math.Min(4, remaining);
                        groups.Add(new BossLocationSpawn { BossName = "assault", BossEscortType = "assault", BossEscortAmount = (count - 1)
                            .ToString(), BossChance = 100, BossZone = zone, BossDifficulty = "normal", BossEscortDifficulty = "normal", Time =


                             15 + groups.Count * 5, ForceSpawn = true, IgnoreMaxBots = false, SptId = "rr-concentrate-" + groups.Count,
                             Supports = [], TriggerId = "", TriggerName = "", IsRandomTimeSpawn = false });
                        remaining -= count;
                    }

                    if (groups.Count == 0)
                    {
                        break;
                    }

                    map.Waves = [];
                    map.BossLocationSpawn = groups;
                    map.BotMax = population;
                    map.BotMaxPvE = map.BotMax;
                    break;
                case "pmc_sweep":
                case "outbreak":
                case "raider_assault":
                case "guarded":
                    if (map.Waves.Count == 0)
                    {
                        map.Waves.Add(new Wave { SlotsMin = 10, SlotsMax = 15, TimeMin = 0, TimeMax = 90, BotPreset = "normal",
                             SpawnPoints = string.Join(',', Zones(map)), SpawnMode = ["regular", "pve"] });
                    }

                    for (int i = 0; i < map.Waves.Count; i++)
                    {
                        var wave = map.Waves[i];
                        wave.WildSpawnType = id == "guarded" ? Enum.Parse<WildSpawnType>(GuardRoles()[i % GuardRoles().Length], true)
                            : id == "raider_assault" ? WildSpawnType.pmcBot : id == "outbreak" ? WildSpawnType.infectedAssault
                                : (i % 2 == 0
                            ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR);
                        // SPT 4.1.6 returns AI PMC profiles on the Savage
                        // simulation side, while their roles still distinguish
                        // USEC/BEAR. Native profile lookup also matches this side.
                        wave.BotSide = "Savage";
                        wave.SptId = $"rr-v2-{id}-{i}";
                    }

                    map.BossLocationSpawn = [];
                    break;
                case "no_safe_space":
                    break; // Client directs all mobile combat AI toward the player.
                case "extreme_overwatch":
                    var roofZones = OverwatchPlan.Zones(map);
                    if (roofZones.Length == 0)
                    {
                        break;
                    }

                    // Preserve all baseline waves and caps. Extras have their own finite request budget.
                    Testing.TestingControl.SetOverwatchPlan(OverwatchPlan.Points(map), 2, Math.Min(settings.MaxScheduledBots, 200));
                    break;
            }
        }

        private static void Scale(LocationBase map, double multiplier, V2Settings settings, bool sniperOnly)
        {
            multiplier = double.IsFinite(multiplier) ? Math.Clamp(multiplier, 1, 10) : 1;
            int remaining = settings.MaxScheduledBots;
            foreach (var wave in map.Waves ?? [])
            {
                var scale = !sniperOnly || wave.WildSpawnType == WildSpawnType.marksman;
                int oldMin = Math.Max(0, wave.SlotsMin ?? 0);
                int oldMax = Math.Max(oldMin, wave.SlotsMax ?? oldMin);
                int maximum = Math.Min(remaining, (int)Math.Ceiling(oldMax * (scale ? multiplier : 1)));
                wave.SlotsMax = maximum;
                wave.SlotsMin = Math.Min(maximum, (int)Math.Ceiling(oldMin * (scale ? multiplier : 1)));
                remaining -= maximum;
            }

            map.BotMax = Math.Clamp((int)Math.Ceiling(Math.Max(1, map.BotMax) * multiplier), 1, settings.MaxAliveBots);
            map.BotMaxPvE = map.BotMax;
        }

        private string[] GuardRoles() => RaidRoulette.Shared.GuardedPolicy.Roles.Where(role => Template(role
            .ToLowerInvariant()) != null).ToArray();
        public BotGenerationDetails PrepareRole(BotGenerationDetails original, RaidRuleSession state)
        {
            if (RaidRoulette.Shared.GauntletPolicy.Handles(state.EventId) && !original.IsPlayerScav)
            {
                var gauntletDetails = cloner.Clone(original)
                    ?? throw new InvalidOperationException("Could not clone gauntlet generation details.");
                // Seasonal event roles must not replace the requested hunter.
                gauntletDetails.EventRole = null;
                return gauntletDetails;
            }

            bool replacement = state.EventId is "guarded" or "pmc_sweep" or "outbreak" or "raider_assault" or "scav_concentrate";
            if (original.IsPlayerScav || original.Role.Equals("shooterBTR", StringComparison.OrdinalIgnoreCase)
                || (!replacement && !IsOrdinary(original)))
            {
                return original;
            }

            var details = cloner.Clone(original) ?? throw new InvalidOperationException("Could not clone bot generation details.");
            if (state.EventId == "guarded" && GuardRoles()is { Length: > 0 } guards)
            {
                details.IsPmc = false;
                // Native profile selection matches the exact requested role.
                // Rerolling a scheduled guard can leave that request without a
                // matching profile and fail raid loading in BotProfileClient.
                details.Role = guards.FirstOrDefault(role => role.Equals(original.Role, StringComparison.OrdinalIgnoreCase))
                    ?? guards[Random.Shared.Next(guards.Length)];
                details.Side = "Savage";
                details.EventRole = null;
            }
            else if (state.EventId == "pmc_sweep")
            {
                details.IsPmc = true;
                bool requestedUsec = original.Role.Equals("pmcUSEC", StringComparison.OrdinalIgnoreCase);
                bool requestedBear = original.Role.Equals("pmcBEAR", StringComparison.OrdinalIgnoreCase);
                // The map schedule already chooses factions. Only convert
                // requests that are not already for a supported PMC faction.
                details.Role = requestedUsec ? "pmcUSEC" : requestedBear ? "pmcBEAR" : Random.Shared.Next(2) == 0 ? "pmcUSEC" : "pmcBEAR";
                details.Side = details.Role == "pmcUSEC" ? "Usec" : "Bear";
                details.EventRole = null;
            }
            else if (state.EventId is "scav_concentrate" or "raider_assault")
            {
                details.IsPmc = false;
                details.Role = state.EventId == "raider_assault" ? "pmcBot" : "assault";
                details.Side = "Savage";
                details.EventRole = null;
            }
            else if (state.EventId == "outbreak")
            {
                details.IsPmc = false;
                details.Role = "infectedAssault";
                details.Side = "Savage";
                details.EventRole = null;
            }

            return details;
        }

        private void SupplyArsenal(BotType bot)
        {
            bot.BotInventory.Ammo = ammo.ToDictionary(x => x.Key, x => new Dictionary<MongoId, double>(x.Value));
            bot.BotInventory.Mods = mods.ToDictionary(x => x.Key, x => x.Value.ToDictionary(s => s.Key, s =>
                 new HashSet<MongoId>(s.Value)));
        }

        private void SetWeapon(BotType bot, Dictionary<MongoId, double> pool, bool handgun, bool noGrenades)
        {
            if (pool.Count == 0)
            {
                throw new InvalidOperationException("No compatible event weapon templates are available.");
            }

            foreach (var slot in new[]
            {
                EquipmentSlots.FirstPrimaryWeapon,
                EquipmentSlots.SecondPrimaryWeapon,
                EquipmentSlots.Holster
            }

            )
            {
                bool chosen = slot == (handgun ? EquipmentSlots.Holster : EquipmentSlots.FirstPrimaryWeapon);
                bot.BotInventory.Equipment[slot] = (handgun || chosen) ? new(pool)
                    : new(); // SPT also samples spare backpack guns from primary candidate pools. Chances still disable equipped backups.
                bot.BotChances.EquipmentChances[slot.ToString()] = chosen ? 100 : 0;
            }

            if (noGrenades)
            {
                if (bot.BotGeneration?.Items?.Grenades != null)
                {
                    bot.BotGeneration.Items.Grenades.Weights = new()
                    {
                        [0] = 1
                    };
                }

                var itemPools = bot.BotInventory.Items;
                if (itemPools != null)
                {
                    foreach (var container in new[]
                    {
                        itemPools.Backpack,
                        itemPools.Pockets,
                        itemPools.TacticalVest,
                        itemPools.SecuredContainer,
                        itemPools.SpecialLoot
                    }

                    )
                    {
                        if (container != null)
                        {
                            foreach (var item in container.Keys.Where(id => templates.Items.TryGetValue(id, out var tpl) && (!string
                                .IsNullOrEmpty(tpl.Properties?.WeapClass) || tpl.Properties?.ThrowType != null)).ToArray())
                            {
                                container.Remove(item);
                            }
                        }
                    }
                }
            }
        }

        public BotType PrepareInventory(BotType original, BotGenerationDetails details, RaidRuleSession state, V2Settings settings)
        {
            var id = state.EventId;
            if (RaidRoulette.Shared.GauntletPolicy.UsesEquipmentProgression(id) && !details.IsPlayerScav)
            {
                return PrepareTrialInventory(original, details, id);
            }

            if (!IsOrdinary(details))
            {
                if (id == "explosive_scav_units")
                {
                    RaidDiagnostics.Decision(id, details.Role, "excluded: boss/follower/playerScav/nonordinary role");
                }

                return original;
            }

            if (id == "explosive_scav_units")
            {
                if (details.IsPmc)
                {
                    RaidDiagnostics.Decision(id, details.Role, "excluded: PMC");
                    return original;
                }

                int roll = Random.Shared.Next(100);
                bool selected = true;
                RaidDiagnostics.Decision(
                    id,
                    details.Role,
                    $"roll={roll} threshold={settings.ExplosiveScavChancePercent} selected={selected} launcherCandidates={WeaponPool("grenadeLauncher").Count}"
                );
                if (!selected)
                {
                    return original;
                }
            }

            if (id is not ("get_handy" or "i_have_a_shot" or "marksman" or "explosive_scav_units" or "geared" or "overloaded"
                or "posers_everywhere"))
            {
                return original;
            }

            var bot = cloner.Clone(original) ?? throw new InvalidOperationException("Could not clone bot template.");
            if (id == "posers_everywhere")
            {
                var killa = Template("bosskilla")!;
                bot.BotInventory = cloner.Clone(killa.BotInventory)!;
                bot.BotChances = cloner.Clone(killa.BotChances)!;
                bot.BotGeneration = cloner.Clone(killa.BotGeneration)!;
                return bot;
            }

            SupplyArsenal(bot);
            switch (id)
            {
                case "get_handy":
                    SetWeapon(bot, WeaponPool("pistol", "revolver"), true, false);
                    break;
                case "i_have_a_shot":
                    SetWeapon(bot, WeaponPool("shotgun"), false, false);
                    break;
                case "marksman":
                    SetWeapon(bot, WeaponPool("sniperRifle"), false, false);
                    break;
                case "explosive_scav_units":
                    SetWeapon(bot, WeaponPool("grenadeLauncher"), false, false);
                    bot.BotInventory.Equipment[EquipmentSlots.Holster] = WeaponPool("pistol", "revolver");
                    bot.BotChances.EquipmentChances["Holster"] = 100;
                    break;
                case "overloaded":
                    var pool = DrumWeapons(settings.MinimumMagazineCapacity);
                    SetWeapon(bot, pool, false, false);
                    bot.BotInventory.Equipment[EquipmentSlots.SecondPrimaryWeapon] = new(pool);
                    bot.BotChances.EquipmentChances["SecondPrimaryWeapon"] = original.BotChances.EquipmentChances
                        .GetValueOrDefault("SecondPrimaryWeapon", 0);
                    foreach (var gun in pool.Keys)
                    {
                        if (!bot.BotInventory.Mods.TryGetValue(gun, out var slots))
                        {
                            bot.BotInventory.Mods[gun] = slots = new();
                        }

                        slots["mod_magazine"] = new(DrumOptions(gun, settings.MinimumMagazineCapacity));
                    }

                    bot.BotChances.WeaponModsChances["mod_magazine"] = 100;
                    break;
                case "geared":
                    SetWeapon(bot, GearedWeapons(settings), false, false);
                    bot.BotInventory.Equipment[EquipmentSlots.ArmorVest] = ArmorPool(EquipmentSlots.ArmorVest, settings
                        .GearedBodyArmorClass);
                    bot.BotChances.EquipmentChances["ArmorVest"] = 100;
                    var rigs = (equipment.GetValueOrDefault(EquipmentSlots.TacticalVest) ?? []).Where(x => BodyArmorClass(x.Key) == 0)
                        .ToDictionary(x => x.Key, _ => 1d);
                    bot.BotInventory.Equipment[EquipmentSlots.TacticalVest] = rigs;
                    bot.BotChances.EquipmentChances["TacticalVest"] = rigs.Count > 0 ? 100 : 0;
                    var headPool = equipment.GetValueOrDefault(EquipmentSlots.Headwear) ?? [];
                    int headClass = headPool.Keys.Select(HeadwearClass).DefaultIfEmpty(0).Max();
                    if (headClass > 0)
                    {
                        bot.BotInventory.Equipment[EquipmentSlots.Headwear] = headPool.Where(item => HeadwearClass(item.Key) == headClass)
                            .ToDictionary(item => item.Key, _ => 1d);
                        bot.BotChances.EquipmentChances["Headwear"] = 100;
                    }

                    foreach (var caliber in bot.BotInventory.Ammo.Keys.ToArray())
                    {
                        var rounds = bot.BotInventory.Ammo[caliber];
                        int best = rounds.Keys.Select(x => templates.Items.GetValueOrDefault(x)?.Properties?.PenetrationPower ?? 0)
                            .DefaultIfEmpty(0).Max();
                        if (best > 0)
                        {
                            bot.BotInventory.Ammo[caliber] = rounds.Where(x => (templates.Items.GetValueOrDefault(x.Key)?.Properties?
                                .PenetrationPower ?? 0) == best).ToDictionary(x => x.Key, _ => 1d);
                        }
                    }

                    foreach (var parent in bot.BotInventory.Mods.Values)
                    {
                        foreach (var slot in parent.Keys.Where(x => x.Contains("plate", StringComparison.OrdinalIgnoreCase)).ToArray())
                        {
                            var best = parent[slot].Where(x => ArmorClass(x) >= settings.GearedBodyArmorClass).ToHashSet();
                            if (best.Count > 0)
                            {
                                parent[slot] = best;
                            }

                            bot.BotChances.EquipmentModsChances[slot] = 100;
                        }
                    }

                    break;
            }

            return bot;
        }

        internal BotType PrepareTrialInventory(BotType original, BotGenerationDetails details, string eventId = "total_trial")
        {
            int wave = RaidRoulette.Shared.GauntletPolicy.FindStage(eventId, details.Role);
            if (wave < 0)
            {
                return original;
            }

            int tier = RaidRoulette.Shared.GauntletPolicy.StageTier(eventId, wave);
            int armorClass = tier + 2;
            var bot = cloner.Clone(original) ?? throw new InvalidOperationException("Could not clone Trials inventory template.");
            SupplyArsenal(bot);
            string[][] weaponNames = [["toz", "mp133", "mp153", "mosin", "sks", "ppsh", "kedr"], ["akm", "ak74", "saiga",
                 "mp5"], ["ak105", "ak74m", "m4a1", "scar"], ["hk416", "sr25", "mdr"], ["spear", "rsass", "hk416", "sr25"]];
            var primary = weapons.Where(id => templates.Items.TryGetValue(id, out var item) && item.Properties?
                .WeapClass is "assaultRifle" or "shotgun" or "smg" or "sniperRifle" or "marksmanRifle" && weaponNames[tier]
                .Any(token => item.Name?.Contains(token, StringComparison.OrdinalIgnoreCase) == true)).ToDictionary(id => id, _ => 1d);
            if (primary.Count > 0)
            {
                bot.BotInventory.Equipment[EquipmentSlots.FirstPrimaryWeapon] = primary;
                bot.BotChances.EquipmentChances["FirstPrimaryWeapon"] = 100;
                bot.BotInventory.Equipment[EquipmentSlots.SecondPrimaryWeapon] = [];
                bot.BotChances.EquipmentChances["SecondPrimaryWeapon"] = 0;
            }

            var armor = ArmorPool(EquipmentSlots.ArmorVest, armorClass);
            if (armor.Count > 0)
            {
                int minimum = armor.Keys.Select(BodyArmorClass).Min();
                bot.BotInventory.Equipment[EquipmentSlots.ArmorVest] = armor.Where(pair => BodyArmorClass(pair.Key) == minimum)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                bot.BotChances.EquipmentChances["ArmorVest"] = 100;
                // Avoid an armored tactical rig displacing the selected vest.
                var rigs = equipment.GetValueOrDefault(EquipmentSlots.TacticalVest) ?? [];
                bot.BotInventory.Equipment[EquipmentSlots.TacticalVest] = rigs.Where(pair => BodyArmorClass(pair.Key) == 0)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            var headwear = equipment.GetValueOrDefault(EquipmentSlots.Headwear) ?? [];
            var helmets = headwear.Where(pair => HeadwearClass(pair.Key) >= armorClass).ToArray();
            if (helmets.Length > 0)
            {
                int minimum = helmets.Min(pair => HeadwearClass(pair.Key));
                bot.BotInventory.Equipment[EquipmentSlots.Headwear] = helmets.Where(pair => HeadwearClass(pair.Key) == minimum)
                    .ToDictionary(pair => pair.Key, _ => 1d);
                bot.BotChances.EquipmentChances["Headwear"] = 100;
            }

            // The native weapon generator still chooses by caliber. Select the
            // best round at this tier's penetration ceiling in each caliber;
            // if none fits the ceiling, use its least-penetrating native round.
            int ceiling = tier == 4 ? int.MaxValue : 20 + tier * 10;
            foreach (string caliber in bot.BotInventory.Ammo.Keys.ToArray())
            {
                var rounds = bot.BotInventory.Ammo[caliber];
                var eligible = rounds.Keys.Where(id => (templates.Items.GetValueOrDefault(id)?.Properties?.PenetrationPower
                    ?? 0) <= ceiling).ToArray();
                var selected = eligible.Length > 0 ? eligible.OrderByDescending(id => templates.Items.GetValueOrDefault(id)?
                    .Properties?.PenetrationPower ?? 0).First() : rounds.Keys.OrderBy(id => templates.Items.GetValueOrDefault(id)?
                    .Properties?.PenetrationPower ?? 0).FirstOrDefault();
                if (rounds.ContainsKey(selected))
                {
                    bot.BotInventory.Ammo[caliber] = new()
                    {
                        [selected] = 1d
                    };
                }
            }

            if (details.Role.Equals("cursedAssault", StringComparison.OrdinalIgnoreCase))
            {
                var killa = Template("bosskilla");
                var helmetId = new MongoId("5c0e874186f7745dc7616606");
                if (templates.Items.ContainsKey(helmetId) && killa?.BotInventory != null)
                {
                    bot.BotInventory.Equipment[EquipmentSlots.Headwear] = new()
                    {
                        [helmetId] = 1d
                    };
                    bot.BotChances.EquipmentChances["Headwear"] = 100;
                    foreach (var pair in killa.BotInventory.Mods)
                    {
                        bot.BotInventory.Mods[pair.Key] = cloner.Clone(pair.Value)!;
                    }

                    foreach (var pair in killa.BotChances!.EquipmentModsChances)
                    {
                        bot.BotChances.EquipmentModsChances[pair.Key] = pair.Value;
                    }
                }
            }

            return bot;
        }

        private static MongoId? Pick(Dictionary<MongoId, double>? pool) => pool?.OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key.ToString(), StringComparer.Ordinal).Select(x => (MongoId? )x.Key).FirstOrDefault();
        private static Customization Clothing(Appearance appearance) => new()
        {
            Body = Pick(appearance.Body),
            Feet = Pick(appearance.Feet),
            Hands = Pick(appearance.Hands)
        };
        public void ApplyAppearance(BotBase bot, BotGenerationDetails details, RaidRuleSession state, V2Settings settings)
        {
            if (!IsOrdinary(details) || bot.Customization is null)
            {
                return;
            }

            var id = state.EventId;
            Customization? look = null;
            if (id == "matrix_glitch")
            {
                var key = string.Equals(settings.MatrixClothingMode, "shared", StringComparison.OrdinalIgnoreCase) ? "shared"
                    : details.IsPmc ? details.Side.ToLowerInvariant() : "scav";
                look = state.Clothing.GetOrAdd(
                    key,
                    _ => new Customization { Body = bot.Customization.Body, Feet = bot.Customization.Feet, Hands = bot.Customization.Hands }
                );
            }
            else if (id == "posers_everywhere")
            {
                look = state.Clothing.GetOrAdd("killa", _ => Clothing(Template("bosskilla")!.BotAppearance));
            }
            else if (id == "fake_cult")
            {
                look = state.Clothing.GetOrAdd(
                    "cult",
                    _ => Clothing((HasClothing(Template("sectantwarrior")) ? Template("sectantwarrior") : Template("sectantpriest"))!
                        .BotAppearance)
                );
            }

            if (look != null)
            {
                bot.Customization.Body = look.Body;
                bot.Customization.Feet = look.Feet;
                bot.Customization.Hands = look.Hands;
            }
        }

        public void VerifyGeneratedBot(BotBase bot, BotGenerationDetails details, RaidRuleSession state, V2Settings settings)
        {
            if (!IsOrdinary(details) || bot.Inventory?.Items is null)
            {
                return;
            }

            var roots = bot.Inventory.Items.Where(x => x.ParentId == bot.Inventory.Equipment?.ToString() && x
                .SlotId is "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster").ToArray();
            var allowed = state.EventId switch
            {
                "get_handy" => new[]
                {
                    "pistol",
                    "revolver"
                },
                "i_have_a_shot" => new[]
                {
                    "shotgun"
                },
                "marksman" => new[]
                {
                    "sniperRifle"
                },
                _ => Array.Empty<string>()};
            if (allowed.Length > 0 && (roots.Length != 1 || !allowed.Contains(
                templates.Items.GetValueOrDefault(roots[0].Template)?.Properties?.WeapClass,
                StringComparer.OrdinalIgnoreCase
            )))
            {
                Warning(
                    state,
                    "generated weapon verification",
                    new InvalidOperationException("SPT or another mod generated a loadout outside the requested weapon class.")
                );
            }

            if (state.EventId == "overloaded")
            {
                foreach (var gun in roots)
                {
                    var magazines = bot.Inventory.Items.Where(x => x.ParentId == gun.Id.ToString() && x.SlotId == "mod_magazine").ToArray();
                    if (magazines.Length == 0 || magazines.Any(m => (templates.Items.GetValueOrDefault(m.Template)?.Properties?
                        .Cartridges ?? []).All(c => (c.MaxCount ?? 0) < settings.MinimumMagazineCapacity)))
                    {
                        Warning(
                            state,
                            "generated magazine verification",
                            new InvalidOperationException("Generated weapon did not receive the requested native-capacity magazine.")
                        );
                    }
                }
            }
        }

        public void Warning(RaidRuleSession state, string stage, Exception error)
        {
            if (state.Warnings.TryAdd(stage, 0))
            {
                warn($"[Raid Roulette] {state.EventId} {stage}: {error.Message}. Check this raid before enabling random selection.");
            }
        }
    }
}
