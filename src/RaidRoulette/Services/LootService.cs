using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace RaidRoulette.Services
{
    public sealed class LootService(LocationTable locationTable)
    {
        public string ApplyLooseLootMultiplierForRaid(string locationId, double multiplier, Action<Action> registerRollback)
        {
            if (multiplier <= 0)
            {
                return "skipped because the multiplier must be greater than zero.";
            }

            var location = FindLocation(locationId);
            var original = location?.LooseLoot;
            if (location is null || original?.Value?.SpawnpointCount is null)
            {
                return $"skipped because loose-loot data was unavailable for '{locationId}'.";
            }

            // In 4.1.x LazyLoad can deserialize on each Value read. Mutating a single
            // read is lost. Wrap the loader during generation and clone only the
            // count record, leaving any cached original untouched.
            location.LooseLoot = new LazyLoad<LooseLoot>(
                () =>
            {
                var value = original.Value;
                if (value?.SpawnpointCount is not { } count)
                {
                    return value!;
                }

                return value with
                {
                    SpawnpointCount = count with
                    {
                        Mean = count.Mean * multiplier,
                        Std = count.Std * multiplier
                    }
                };
            },
                cacheValue: false
            );
            registerRollback(() => location.LooseLoot = original);
            return $"applied {multiplier:0.##}x loose-loot generation to '{locationId}' for this raid.";
        }

        private Location? FindLocation(string locationId)
        {
            var locations = locationTable;
            var normalized = NormalizeLocationId(locationId);
            return normalized switch
            {
                "bigmap" or "customs" => locations.Bigmap,
                "factory4day" or "factoryday" => locations.Factory4Day,
                "factory4night" or "factorynight" => locations.Factory4Night,
                "interchange" => locations.Interchange,
                "laboratory" or "lab" or "labs" => locations.Laboratory,
                "labyrinth" => locations.Labyrinth,
                "lighthouse" => locations.Lighthouse,
                "rezervbase" or "reserve" => locations.RezervBase,
                "sandbox" or "groundzero" => locations.Sandbox,
                "sandboxhigh" or "groundzerohigh" => locations.SandboxHigh,
                "shoreline" => locations.Shoreline,
                "tarkovstreets" or "streets" or "streetsoftarkov" => locations.TarkovStreets,
                "woods" => locations.Woods,
                _ => null
            };
        }

        private static string NormalizeLocationId(string locationId)
        {
            return new string (locationId.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
