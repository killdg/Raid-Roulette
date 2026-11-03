using System.Reflection;
using RaidRoulette.Configuration;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace RaidRoulette.Services
{
    /// <summary>
    /// Creates temporary per-raid boss-style spawn entries using SPT's own
    /// LocationBase/BossLocationSpawn data. Entries are removed immediately after
    /// StartLocalRaid has generated its raid-specific location copy.
    /// </summary>
    public sealed class BossSpawnService(LocationTable locationTable)
    {
        public BossHuntResult EnableBossHuntForRaid(string locationId, Action<Action> registerRollback)
        {
            var location = FindLocation(locationId);
            var spawns = location?.BossLocationSpawn;
            if (location is null)
            {
                return BossHuntResult.Failed("no SPT boss-spawn data was available on this map");
            }

            if (spawns is null)
            {
                spawns = [];
                location.BossLocationSpawn = spawns;
                registerRollback(() => location.BossLocationSpawn = null!);
            }

            var catalog = BuildBossTemplateCatalog(includeAdditionalHunters: true);
            // SPT lists Big Pipe and Birdeye as Knight's escorts rather than
            // standalone location bosses. Reuse the native Goons spawn template
            // for each role; the hunter configuration below removes all escorts.
            if (catalog.TryGetValue("bossKnight", out var goonsTemplate))
            {
                catalog["followerBigPipe"] = goonsTemplate;
                catalog["followerBirdEye"] = goonsTemplate;
            }

            var candidates = catalog.Where(entry => !entry.Key.Equals("bossZryachiy", StringComparison.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next()).ToArray();
            var zones = RaidRulesService.Zones(location).Where(zone => !zone.Contains("snip", StringComparison
                .OrdinalIgnoreCase)).ToArray();
            if (candidates.Length == 0 || zones.Length == 0)
            {
                return BossHuntResult.Failed("no mobile boss template or ground spawn zone was available");
            }

            var selected = candidates[0];
            var role = selected.Key;
            var zone = zones[Random.Shared.Next(zones.Length)];
            var originalChance = ReadDouble(selected.Value, "BossChance");
            var snapshot = spawns.ToArray();
            registerRollback(() =>
            {
                spawns.Clear();
                spawns.AddRange(snapshot);
            });
            spawns.RemoveAll(spawn => string.Equals(spawn.BossName, role, StringComparison.OrdinalIgnoreCase));
            var hunter = Clone(selected.Value);
            hunter.BossName = role;
            hunter.BossChance = 100;
            hunter.BossZone = zone;
            hunter.BossEscortAmount = "0";
            hunter.BossEscortType = "assault";
            hunter.Supports = [];
            hunter.Time = -1;
            hunter.IsRandomTimeSpawn = false;
            hunter.ForceSpawn = true;
            hunter.IgnoreMaxBots = true;
            hunter.TriggerId = "";
            hunter.TriggerName = "";
            hunter.SptId = "rr-boss-hunt";
            spawns.Add(hunter);
            Testing.TestingControl.SetHuntBoss(role);
            return BossHuntResult.Succeeded(role, zone, originalChance, 100);
        }

        public BossConventionResult EnableBossConventionForRaid(string locationId, BossConventionConfig settings,
             Action<Action> registerRollback)
        {
            var targetLocation = FindLocation(locationId);
            var targetSpawns = targetLocation?.BossLocationSpawn;
            if (targetSpawns is null || targetSpawns.Count == 0)
            {
                return BossConventionResult.Failed("no SPT boss-spawn data was available on this map");
            }

            var targetTemplates = targetSpawns.Where(spawn => !string.IsNullOrWhiteSpace(ReadString(spawn, "BossZone"))).ToList();
            var zonePool = targetTemplates.SelectMany(template => SplitZones(ReadString(template, "BossZone")))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(_ => Random.Shared.Next()).ToList();
            if (zonePool.Count == 0)
            {
                return BossConventionResult.Failed("the map contained no usable boss zones");
            }

            var definitions = GetBossConventionDefinitions().Where(definition => settings.Bosses.IsEnabled(definition.Key)).ToList();
            if (definitions.Count == 0)
            {
                return BossConventionResult.Failed("every Boss Convention boss was disabled in config.json");
            }

            var catalog = BuildBossTemplateCatalog();
            var available = definitions.Where(definition => catalog.ContainsKey(definition.BossName)).ToList();
            if (available.Count == 0)
            {
                return BossConventionResult.Failed("no enabled boss templates were found in the SPT location database");
            }

            var isFactory = IsFactory(locationId);
            var requestedCount = isFactory ? Math.Clamp(settings.Factory.BossCount, 0, available.Count) : settings.OtherMaps
                .SpawnAllEnabledBosses ? available.Count : Math.Clamp(settings.OtherMaps.BossCount, 0, available.Count);
            if (requestedCount <= 0)
            {
                return BossConventionResult.Failed("the configured boss count was zero");
            }

            var randomSelection = isFactory ? settings.Factory.RandomSelection : settings.OtherMaps.RandomSelection;
            var selected = randomSelection ? available.OrderBy(_ => Random.Shared.Next()).Take(requestedCount).ToList()
                : available.Take(requestedCount).ToList();
            var added = new List<BossLocationSpawn>();
            var restored = new List<Action>();
            var entries = new List<BossConventionEntry>();
            var globalSettings = settings.GlobalSettings;
            var bossDifficulty = string.IsNullOrWhiteSpace(globalSettings.BossDifficulty) ? "hard" : globalSettings.BossDifficulty.Trim();
            var guardDifficulty = string.IsNullOrWhiteSpace(globalSettings.GuardDifficulty) ? "hard" : globalSettings
                .GuardDifficulty.Trim();
            registerRollback(() =>
            {
                for (var index = restored.Count - 1; index >= 0; index--)
                {
                    restored[index]();
                }

                foreach (var spawn in added)
                {
                    targetSpawns.Remove(spawn);
                }
            });
            for (var index = 0; index < selected.Count; index++)
            {
                var definition = selected[index];
                var bossSettings = settings.Bosses.Get(definition.Key);
                var source = catalog[definition.BossName];
                var zone = globalSettings.SpreadAcrossZones ? zonePool[index % zonePool.Count] : zonePool[0];
                var spawnChance = Math.Clamp(bossSettings.UseGlobalSpawnChance ? globalSettings.SpawnChance : bossSettings
                    .SpawnChance, 0, 100);
                var includeGuards = bossSettings.UseGlobalGuardSetting ? globalSettings.IncludeGuards : bossSettings.IncludeGuards;
                var existing = targetSpawns.FirstOrDefault(spawn => string.Equals(ReadString(spawn, "BossName"), definition
                    .BossName, StringComparison.OrdinalIgnoreCase));
                BossLocationSpawn configuredSpawn;
                if (existing is not null)
                {
                    configuredSpawn = existing;
                    var snapshot = Clone(existing);
                    restored.Add(() =>
                    {
                        foreach (var property in typeof(BossLocationSpawn).GetProperties())
                        {
                            if (property.CanRead && property.CanWrite)
                            {
                                property.SetValue(existing, property.GetValue(snapshot));
                            }
                        }
                    });
                }
                else
                {
                    configuredSpawn = Clone(source);
                    targetSpawns.Add(configuredSpawn);
                    added.Add(configuredSpawn);
                }

                Set(configuredSpawn, "BossName", definition.BossName);
                Set(configuredSpawn, "BossChance", (double)spawnChance);
                Set(configuredSpawn, "BossZone", zone);
                Set(configuredSpawn, "IsBossPlayer", false);
                Set(configuredSpawn, "BossDifficulty", bossDifficulty);
                Set(configuredSpawn, "BossEscortDifficulty", guardDifficulty);
                Set(configuredSpawn, "Time", -1);
                Set(configuredSpawn, "IsRandomTimeSpawn", false);
                Set(configuredSpawn, "TriggerId", string.Empty);
                Set(configuredSpawn, "TriggerName", string.Empty);
                if (!includeGuards)
                {
                    configuredSpawn.Supports = [];
                    Set(configuredSpawn, "BossEscortAmount", "0");
                    Set(configuredSpawn, "BossEscortType", string.Empty);
                }

                var escortType = ReadString(configuredSpawn, "BossEscortType") ?? string.Empty;
                var escortAmount = ReadString(configuredSpawn, "BossEscortAmount") ?? "0";
                entries.Add(new BossConventionEntry(
                        definition.Key,
                        definition.DisplayName,
                        definition.BossName,
                        zone,
                        ReadDouble(configuredSpawn, "BossChance"),
                        includeGuards && HasEscorts(escortType, escortAmount),
                        escortType,
                        escortAmount
                    ));
            }

            return BossConventionResult.Succeeded(available.Count, requestedCount, entries);
        }

        private Dictionary<string, BossLocationSpawn> BuildBossTemplateCatalog(bool includeAdditionalHunters = false)
        {
            var wantedBosses = GetBossConventionDefinitions().Select(definition => definition.BossName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (includeAdditionalHunters)
            {
                // These native roles have their own boss templates. Keep this
                // expansion specific to the solo hunt, leaving Boss Convention intact.
                wantedBosses.UnionWith(["bossKillaAgro", "bossTagillaAgro", "sectantPriest"]);
            }

            var catalog = new Dictionary<string, BossLocationSpawn>(StringComparer.OrdinalIgnoreCase);
            foreach (var location in GetAllLocations())
            {
                var spawns = location?.BossLocationSpawn;
                if (spawns is null)
                {
                    continue;
                }

                foreach (var spawn in spawns)
                {
                    var bossName = ReadString(spawn, "BossName");
                    if (string.IsNullOrWhiteSpace(bossName) || !wantedBosses.Contains(bossName) || catalog.ContainsKey(bossName))
                    {
                        continue;
                    }

                    catalog[bossName] = spawn;
                }
            }

            return catalog;
        }

        private IEnumerable<LocationBase?> GetAllLocations()
        {
            var locations = locationTable;
            yield return locations.Bigmap?.Base;
            yield return locations.Factory4Day?.Base;
            yield return locations.Factory4Night?.Base;
            yield return locations.Interchange?.Base;
            yield return locations.Laboratory?.Base;
            yield return locations.Labyrinth?.Base;
            yield return locations.Lighthouse?.Base;
            yield return locations.RezervBase?.Base;
            yield return locations.Sandbox?.Base;
            yield return locations.SandboxHigh?.Base;
            yield return locations.Shoreline?.Base;
            yield return locations.TarkovStreets?.Base;
            yield return locations.Woods?.Base;
        }

        private static IReadOnlyList<BossConventionDefinition> GetBossConventionDefinitions()
        {
            return[new("Reshala", "Reshala", "bossBully"), new("Shturman", "Shturman", "bossKojaniy"), new("Glukhar",
                 "Glukhar", "bossGluhar"), new("Killa", "Killa", "bossKilla"), new("Sanitar", "Sanitar", "bossSanitar"),
                 new("Tagilla", "Tagilla", "bossTagilla"), new("Zryachiy", "Zryachiy", "bossZryachiy"), new("Kaban", "Kaban",
                 "bossBoar"), new("Kollontay", "Kollontay", "bossKolontay"), new("Partisan", "Partisan", "bossPartisan"),
                 new("Goons", "The Goons", "bossKnight")];
        }

        private static bool IsFactory(string locationId)
        {
            var normalized = NormalizeLocationId(locationId);
            return normalized is "factory4day" or "factoryday" or "factory4night" or "factorynight";
        }

        private static bool HasEscorts(string escortType, string escortAmount)
        {
            if (string.IsNullOrWhiteSpace(escortType))
            {
                return false;
            }

            return escortAmount.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(value =>
                 int.TryParse(value, out var count) && count > 0);
        }

        public SpawnGroupResult AddCultistGroupsForRaid(string locationId, int groupCount, Action<Action> registerRollback)
        {
            return AddGroupsForRaid(
                locationId,
                groupCount,
                bossName: "sectantPriest",
                escortType: "sectantWarrior",
                escortAmount: "2,3,4",
                registerRollback
            );
        }

        public SpawnGroupResult AddRaiderGroupsForRaid(string locationId, int groupCount, Action<Action> registerRollback)
        {
            return AddGroupsForRaid(locationId, groupCount, bossName: "pmcBot", escortType: "pmcBot", escortAmount: "3,4,5",
                 registerRollback);
        }

        private SpawnGroupResult AddGroupsForRaid(string locationId, int groupCount, string bossName, string escortType,
             string escortAmount, Action<Action> registerRollback)
        {
            if (groupCount <= 0)
            {
                return SpawnGroupResult.Disabled(groupCount);
            }

            var location = FindLocation(locationId);
            var spawns = location?.BossLocationSpawn;
            if (spawns is null || spawns.Count == 0)
            {
                return SpawnGroupResult.Failed(groupCount, "no SPT boss-spawn template was available on this map");
            }

            var templates = spawns.Where(spawn => !string.IsNullOrWhiteSpace(ReadString(spawn, "BossZone"))).ToList();
            if (templates.Count == 0)
            {
                return SpawnGroupResult.Failed(groupCount, "no valid SPT boss zones were available on this map");
            }

            // A BossZone can contain several comma-separated zones. Split those into
            // individual candidates so an assault is distributed around the map
            // instead of creating multiple entries that all point at one combined zone.
            var zonePool = templates.SelectMany(template => SplitZones(ReadString(template, "BossZone")))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(_ => Random.Shared.Next()).ToList();
            if (zonePool.Count == 0)
            {
                return SpawnGroupResult.Failed(groupCount, "the map's boss templates contained no usable individual zones");
            }

            var added = new List<BossLocationSpawn>();
            var entries = new List<SpawnGroupEntry>();
            var(minimumEscorts, maximumEscorts) = ParseEscortRange(escortAmount);
            registerRollback(() =>
            {
                foreach (var spawn in added)
                {
                    spawns.Remove(spawn);
                }
            });
            for (var index = 0; index < groupCount; index++)
            {
                var template = templates[index % templates.Count];
                var zone = zonePool[index % zonePool.Count];
                var spawn = Clone(template);
                spawn.Supports = [];
                Set(spawn, "BossName", bossName);
                Set(spawn, "BossChance", 100d);
                Set(spawn, "BossZone", zone);
                Set(spawn, "IsBossPlayer", false);
                Set(spawn, "BossDifficulty", "hard");
                Set(spawn, "BossEscortType", escortType);
                Set(spawn, "BossEscortDifficulty", "hard");
                Set(spawn, "BossEscortAmount", escortAmount);
                // Native BotBossSpawn rejects cultists sharing a zone with a different boss group.
                // ForceSpawn bypasses that group gate; a later wave avoids the initial same-zone boss gate.
                bool cultist = string.Equals(bossName, "sectantPriest", StringComparison.OrdinalIgnoreCase);
                Set(spawn, "Time", cultist ? 15 * (index + 1) : -1);
                if (cultist)
                {
                    Set(spawn, "ForceSpawn", true);
                    Set(spawn, "IgnoreMaxBots", true);
                }

                Set(spawn, "IsRandomTimeSpawn", false);
                Set(spawn, "TriggerId", string.Empty);
                Set(spawn, "TriggerName", string.Empty);
                spawns.Add(spawn);
                added.Add(spawn);
                entries.Add(new SpawnGroupEntry(zone, minimumEscorts + 1, maximumEscorts + 1, 100));
            }

            return SpawnGroupResult.Succeeded(groupCount, entries);
        }

        private static IEnumerable<string> SplitZones(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }

            foreach (var zone in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(zone))
                {
                    yield return zone;
                }
            }
        }

        private LocationBase? FindLocation(string locationId)
        {
            var locations = locationTable;
            var normalized = NormalizeLocationId(locationId);
            return normalized switch
            {
                "bigmap" or "customs" => locations.Bigmap?.Base,
                "factory4day" or "factoryday" => locations.Factory4Day?.Base,
                "factory4night" or "factorynight" => locations.Factory4Night?.Base,
                "interchange" => locations.Interchange?.Base,
                "laboratory" or "lab" or "labs" => locations.Laboratory?.Base,
                "labyrinth" => locations.Labyrinth?.Base,
                "lighthouse" => locations.Lighthouse?.Base,
                "rezervbase" or "reserve" => locations.RezervBase?.Base,
                "sandbox" or "groundzero" => locations.Sandbox?.Base,
                "sandboxhigh" or "groundzerohigh" => locations.SandboxHigh?.Base,
                "shoreline" => locations.Shoreline?.Base,
                "tarkovstreets" or "streets" or "streetsoftarkov" => locations.TarkovStreets?.Base,
                "woods" => locations.Woods?.Base,
                _ => null
            };
        }

        private static BossLocationSpawn Clone(BossLocationSpawn source)
        {
            var memberwiseClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (BossLocationSpawn)memberwiseClone.Invoke(source, null)!;
        }

        private static string? ReadString(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName)?.GetValue(target)?.ToString();
        }

        private static double ReadDouble(object target, string propertyName)
        {
            var value = target.GetType().GetProperty(propertyName)?.GetValue(target);
            if (value is null)
            {
                return 0d;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0d;
            }
        }

        private static void Set(object target, string propertyName, object? value)
        {
            var property = target.GetType().GetProperty(propertyName);
            if (property is null || !property.CanWrite)
            {
                return;
            }

            try
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object? converted = value;
                if (value is not null && !targetType.IsInstanceOfType(value))
                {
                    converted = targetType.IsEnum ? Enum.Parse(targetType, value.ToString()!, true) : Convert.ChangeType(value, targetType);
                }

                property.SetValue(target, converted);
            }
            catch
            {
            // SPT model properties can vary slightly between versions. An
            // unsupported optional property should not prevent raid startup.
            }
        }

        private static (int Minimum, int Maximum) ParseEscortRange(string escortAmount)
        {
            var values = escortAmount.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var parsed) ? parsed : 0).ToArray();
            return values.Length == 0 ? (0, 0) : (values.Min(), values.Max());
        }

        private static string NormalizeLocationId(string locationId)
        {
            return new string (locationId.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }

    internal sealed record BossConventionDefinition(string Key, string DisplayName, string BossName);
    public sealed record BossConventionEntry(string Key, string DisplayName, string BossName, string Zone,
         double SpawnChance, bool GuardsIncluded, string EscortType, string EscortAmount);
    public sealed record BossConventionResult(bool Success, int AvailableGroups, int RequestedGroups,
         IReadOnlyList<BossConventionEntry> Entries, string? FailureReason)
    {
        public static BossConventionResult Succeeded(int availableGroups, int requestedGroups,
             IReadOnlyList<BossConventionEntry> entries) => new(true, availableGroups, requestedGroups, entries, null);
        public static BossConventionResult Failed(string reason) => new(false, 0, 0, [], reason);
    }

    public sealed record BossHuntResult(bool Success, string? BossName, string? Zone, double OriginalChance,
         double AppliedChance, string? FailureReason)
    {
        public static BossHuntResult Succeeded(string bossName, string zone, double originalChance,
             double appliedChance) => new(true, bossName, zone, originalChance, appliedChance, null);
        public static BossHuntResult Failed(string reason) => new(false, null, null, 0d, 0d, reason);
    }

    public sealed record SpawnGroupEntry(string Zone, int MinimumBots, int MaximumBots, int SpawnChance);
    public sealed record SpawnGroupResult(bool Success, bool IsDisabled, int RequestedGroups,
         IReadOnlyList<SpawnGroupEntry> Entries, string? FailureReason)
    {
        public int AddedGroups => Entries.Count;
        public int MinimumTotalBots => Entries.Sum(entry => entry.MinimumBots);
        public int MaximumTotalBots => Entries.Sum(entry => entry.MaximumBots);

        public static SpawnGroupResult Succeeded(int requestedGroups, IReadOnlyList<SpawnGroupEntry> entries) => new(true,
             false, requestedGroups, entries, null);
        public static SpawnGroupResult Disabled(int requestedGroups) => new(false, true, requestedGroups, [], null);
        public static SpawnGroupResult Failed(int requestedGroups, string reason) => new(false, false, requestedGroups, [], reason);
    }
}
