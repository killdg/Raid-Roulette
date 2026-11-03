using System.Runtime.CompilerServices;
using Path = System.IO.Path;
using NativeCloner = SPTarkov.Server.Core.Utils.Cloners.FastCloner;
using RaidRoulette.Configuration;
using RaidRoulette.Core;
using RaidRoulette.Services;
using RaidRoulette.Runtime;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Models.Spt.Bots;

internal static class NativeEventMatrix
{
    internal static void Run(string databaseRoot, JsonUtil json, TemplateTable templates)
    {
        // Read licensed test-install data in memory. Never write its database.
        var bots = (BotTable)RuntimeHelpers.GetUninitializedObject(typeof(BotTable));
        var types = Directory.GetFiles(Path.Combine(databaseRoot, "bots", "types"), "*.json").ToDictionary(Path
            .GetFileNameWithoutExtension, path => json.Deserialize<BotType>(File.ReadAllText(path)));
        typeof(BotTable).GetProperty("Types")!.SetValue(bots, types);
        var locations = (LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
        var rules = new RaidRulesService(templates, bots, locations, new NativeCloner(), _ =>
        {
        });
        var settings = new V2Settings();
        CheckGearedEquipment(rules, templates, types["assault"]!, settings);
        CheckGauntletEquipment(rules, templates, types);
        foreach (string role in RaidRoulette.Shared.GuardedPolicy.Roles.Concat(new[] { "pmcUSEC", "pmcBEAR" }))
        {
            string eventId = role.StartsWith("pmc", StringComparison.OrdinalIgnoreCase) ? "pmc_sweep" : "guarded";
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var request = new BotGenerationDetails
                {
                    Role = role,
                    Side = "Savage"
                };
                var prepared = rules.PrepareRole(request, new RaidRuleSession(eventId));
                if (!prepared.Role.Equals(role, StringComparison.OrdinalIgnoreCase) || request.Role != role)
                {
                    throw new InvalidOperationException($"Requested role changed: {eventId}/{role}");
                }
            }

            Console.WriteLine($"PASS requested-role preservation {eventId}/{role}: 64 preparations");
        }

        string[] maps =
        {
            "bigmap",
            "factory4_day",
            "factory4_night",
            "interchange",
            "laboratory",
            "lighthouse",
            "rezervbase",
            "sandbox",
            "sandbox_high",
            "shoreline",
            "tarkovstreets",
            "woods"
        };
        foreach (string mapId in maps)
        {
            string path = Path.Combine(databaseRoot, "locations", mapId, "base.json");
            string original = File.ReadAllText(path);
            foreach (var raidEvent in EventRegistry.GetEvents())
            {
                // A fresh native map per event catches missing lists and avoids
                // one event's schedule masking a failure in the next event.
                LocationBase map = json.Deserialize<LocationBase>(original)!;
                rules.ApplyMap(map, raidEvent.Id, settings);
                if (map.BotMax < 0 || map.BotMaxPvE < 0 || (map.Waves?.Any(wave => wave.SlotsMin < 0 || wave.SlotsMax < wave
                    .SlotsMin) ?? false))
                {
                    throw new InvalidOperationException($"Invalid population: {mapId}/{raidEvent.Id}");
                }

                if (RaidRoulette.Shared.GauntletPolicy.Handles(raidEvent.Id) && (map.Waves?.Count != 0 || map.BossLocationSpawn?
                    .Count != 0))
                {
                    throw new InvalidOperationException("Gauntlet retained native schedules: " + mapId);
                }

                if (raidEvent.Id is "guarded" or "raider_assault" or "pmc_sweep" or "outbreak")
                {
                    if (map.BossLocationSpawn?.Count > 0 || map.Waves?.Count == 0)
                    {
                        throw new InvalidOperationException($"Conflicting native schedules: {mapId}/{raidEvent.Id}");
                    }

                    foreach (Wave wave in map.Waves!)
                    {
                        string role = wave.WildSpawnType.ToString();
                        bool expected = raidEvent.Id switch
                        {
                            "guarded" => RaidRoulette.Shared.GuardedPolicy.Roles.Contains(role, StringComparer.OrdinalIgnoreCase),
                            "raider_assault" => role == "pmcBot",
                            "outbreak" => role == "infectedAssault",
                            "pmc_sweep" => role is "pmcUSEC" or "pmcBEAR",
                            _ => false
                        };
                        if (!expected || !types.ContainsKey(role.ToLowerInvariant()) || wave.BotSide != "Savage")
                        {
                            throw new InvalidOperationException($"Invalid native role {role}: {mapId}/{raidEvent.Id}");
                        }
                    }

                    Console.WriteLine($"PASS native-role-matrix {mapId}/{raidEvent.Id}: exclusive intended roles with native templates");
                }

                Console.WriteLine($"PASS native-map-matrix {mapId}/{raidEvent.Id}: valid population schedule");
            }

            if (File.ReadAllText(path) != original)
            {
                throw new InvalidOperationException("Native database changed during read-only matrix: " + mapId);
            }
        }
    }

    private static void CheckGauntletEquipment(RaidRulesService rules, TemplateTable templates, Dictionary<string, BotType?> types)
    {
        foreach (string role in RaidRoulette.Shared.GauntletPolicy.SnowballRoles.Concat(RaidRoulette.Shared.GauntletPolicy
            .TrialRoles).Concat(RaidRoulette.Shared.GauntletPolicy.ExtremeRoles).Distinct())
        {
            if (!types.ContainsKey(role.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Missing gauntlet role template: " + role);
            }
        }

        Console.WriteLine("PASS all Snowball, Trials and Extreme Trials roles have native templates");
        var helmetScav = rules.PrepareTrialInventory(types["cursedassault"]!, new BotGenerationDetails { Role =
             "cursedAssault", Side = "Savage" });
        var helmets = helmetScav.BotInventory.Equipment[SPTarkov.Server.Core.Models.Enums.EquipmentSlots.Headwear];
        if (helmets.Count != 1 || !helmets.ContainsKey(new SPTarkov.Server.Core.Models.Common.MongoId("5c0e874186f7745dc7616606")))
        {
            throw new InvalidOperationException("Stronger Scavs must wear the Killa helmet");
        }

        Console.WriteLine("PASS stronger Scav wave wears Killa helmets without changing role");
        foreach (string role in RaidRoulette.Shared.GauntletPolicy.TrialRoles)
        {
            var inventory = rules.PrepareTrialInventory(types[role.ToLowerInvariant()]!, new BotGenerationDetails { Role =
                 role, Side = "Savage" });
            var primary = inventory.BotInventory.Equipment[SPTarkov.Server.Core.Models.Enums.EquipmentSlots.FirstPrimaryWeapon];
            if (primary.Count == 0 || inventory.BotInventory.Ammo.Values.Any(pool => pool.Count != 1))
            {
                throw new InvalidOperationException("Invalid staged Trials arsenal: " + role);
            }

            Console.WriteLine("PASS Trials role has staged primary weapons and per-caliber tier ammo: " + role);
        }
    }

    private static void CheckGearedEquipment(RaidRulesService rules, TemplateTable templates, BotType scav, V2Settings settings)
    {
        var prepared = rules.PrepareInventory(
            scav,
            new BotGenerationDetails { Role = "assault", Side = "Savage" },
            new RaidRuleSession("geared"),
            settings
        );
        var helmets = prepared.BotInventory.Equipment[SPTarkov.Server.Core.Models.Enums.EquipmentSlots.Headwear];
        var vulkan = new SPTarkov.Server.Core.Models.Common.MongoId("5ca20ee186f774799474abc2");
        var pumpkin = new SPTarkov.Server.Core.Models.Common.MongoId("59ef13ca86f77445fd0e2483");
        if (!helmets.ContainsKey(vulkan) || helmets.ContainsKey(pumpkin))
        {
            throw new InvalidOperationException("Geared helmet selection must recognize integrated Vulkan armor over pumpkin root armor.");
        }

        Console.WriteLine("PASS Geared integrated helmet armor outranks seasonal pumpkin headwear");
        var thor = templates.Items[new SPTarkov.Server.Core.Models.Common.MongoId("60a283193cb70855c43a381d")];
        var frontPlates = rules.GearedPlateCandidates(thor, "front_plate", 6);
        if (frontPlates.Count == 0 || frontPlates.Any(plate => templates.Items[plate].Properties?.ArmorClass < 6))
        {
            throw new InvalidOperationException("Geared THOR front pool must contain compatible class-six plates.");
        }

        Console.WriteLine("PASS Geared native carrier filter produces compatible class-six plates");
        if (rules.GearedPlateCandidates(thor, "soft_armor_front", 6).Count != 0)
        {
            throw new InvalidOperationException("Geared plate repair must not replace integrated soft inserts.");
        }

        Console.WriteLine("PASS Geared plate repair excludes integrated soft inserts");
    }
}
