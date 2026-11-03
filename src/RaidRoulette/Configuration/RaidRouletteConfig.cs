using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidRoulette.Configuration
{
    public sealed class RaidRouletteConfig
    {
        public bool Enabled { get; set; } = true;
        public V2Settings V2 { get; set; } = new();
        public int EventChance { get; set; } = 35;
        public LoggingConfig Logging { get; set; } = new();
        public DebugConfig Debug { get; set; } = new();
        public EventWeightConfig Weights { get; set; } = new();
        public double LootSurgeMultiplier { get; set; } = 2.0;
        public double HighValueLootMultiplier { get; set; } = 3.0;
        public int CultistGroups { get; set; } = 2;
        public int RaiderGroups { get; set; } = 4;
        public BossConventionConfig BossConvention { get; set; } = new();
    }

    public sealed class LoggingConfig
    {
        public string Level { get; set; } = "Minimal";
    }

    public sealed class DebugConfig
    {
        public string ForceEvent { get; set; } = string.Empty;
    }

    public sealed class BossConventionConfig
    {
        public bool Enabled { get; set; } = true;
        public BossConventionFactoryConfig Factory { get; set; } = new();
        public BossConventionOtherMapsConfig OtherMaps { get; set; } = new();
        public BossConventionGlobalConfig GlobalSettings { get; set; } = new();
        public BossConventionBossConfig Bosses { get; set; } = new();
    }

    public sealed class BossConventionFactoryConfig
    {
        public int BossCount { get; set; } = 3;
        public bool RandomSelection { get; set; } = true;
    }

    public sealed class BossConventionOtherMapsConfig
    {
        public bool SpawnAllEnabledBosses { get; set; } = true;
        public int BossCount { get; set; } = 6;
        public bool RandomSelection { get; set; } = true;
    }

    public sealed class BossConventionGlobalConfig
    {
        public int SpawnChance { get; set; } = 100;
        public bool IncludeGuards { get; set; } = true;
        public string BossDifficulty { get; set; } = "hard";
        public string GuardDifficulty { get; set; } = "hard";
        public bool SpreadAcrossZones { get; set; } = true;
    }

    public sealed class BossConventionBossConfig
    {
        public BossConventionBossSetting Reshala { get; set; } = new();
        public BossConventionBossSetting Shturman { get; set; } = new();
        public BossConventionBossSetting Glukhar { get; set; } = new();
        public BossConventionBossSetting Killa { get; set; } = new();
        public BossConventionBossSetting Sanitar { get; set; } = new();
        public BossConventionBossSetting Tagilla { get; set; } = new();
        public BossConventionBossSetting Zryachiy { get; set; } = new();
        public BossConventionBossSetting Kaban { get; set; } = new();
        public BossConventionBossSetting Kollontay { get; set; } = new();
        public BossConventionBossSetting Partisan { get; set; } = new();
        public BossConventionBossSetting Goons { get; set; } = new();

        public BossConventionBossSetting Get(string key)
        {
            return key switch
            {
                "Reshala" => Reshala,
                "Shturman" => Shturman,
                "Glukhar" => Glukhar,
                "Killa" => Killa,
                "Sanitar" => Sanitar,
                "Tagilla" => Tagilla,
                "Zryachiy" => Zryachiy,
                "Kaban" => Kaban,
                "Kollontay" => Kollontay,
                "Partisan" => Partisan,
                "Goons" => Goons,
                _ => new BossConventionBossSetting
                {
                    Enabled = false
                }
            };
        }

        public bool IsEnabled(string key) => Get(key).Enabled;
    }

    [JsonConverter(typeof(BossConventionBossSettingConverter))]
    public sealed class BossConventionBossSetting
    {
        public bool Enabled { get; set; } = true;
        public bool UseGlobalSpawnChance { get; set; } = true;
        public int SpawnChance { get; set; } = 100;
        public bool UseGlobalGuardSetting { get; set; } = true;
        public bool IncludeGuards { get; set; } = true;
    }

    /// <summary>
    /// Keeps old v0.2.2 boolean boss entries compatible while writing the clearer
    /// object-based format used by v0.2.3 and later.
    /// </summary>
    public sealed class BossConventionBossSettingConverter : JsonConverter<BossConventionBossSetting>
    {
        public override BossConventionBossSetting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
            {
                return new BossConventionBossSetting
                {
                    Enabled = reader.GetBoolean()
                };
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("A boss setting must be true, false, or a settings object.");
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            var setting = new BossConventionBossSetting();
            if (root.TryGetProperty(nameof(setting.Enabled), out var enabled))
            {
                setting.Enabled = enabled.GetBoolean();
            }

            if (root.TryGetProperty(nameof(setting.UseGlobalSpawnChance), out var useGlobalChance))
            {
                setting.UseGlobalSpawnChance = useGlobalChance.GetBoolean();
            }

            if (root.TryGetProperty(nameof(setting.SpawnChance), out var chance))
            {
                setting.SpawnChance = chance.GetInt32();
            }

            if (root.TryGetProperty(nameof(setting.UseGlobalGuardSetting), out var useGlobalGuards))
            {
                setting.UseGlobalGuardSetting = useGlobalGuards.GetBoolean();
            }

            if (root.TryGetProperty(nameof(setting.IncludeGuards), out var guards))
            {
                setting.IncludeGuards = guards.GetBoolean();
            }

            return setting;
        }

        public override void Write(Utf8JsonWriter writer, BossConventionBossSetting value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteBoolean(nameof(value.Enabled), value.Enabled);
            writer.WriteBoolean(nameof(value.UseGlobalSpawnChance), value.UseGlobalSpawnChance);
            writer.WriteNumber(nameof(value.SpawnChance), value.SpawnChance);
            writer.WriteBoolean(nameof(value.UseGlobalGuardSetting), value.UseGlobalGuardSetting);
            writer.WriteBoolean(nameof(value.IncludeGuards), value.IncludeGuards);
            writer.WriteEndObject();
        }
    }

    public sealed class EventWeightConfig
    {
        public int NormalRaid { get; set; } = 50;
        public int LootSurge { get; set; } = 25;
        public int BossHunt { get; set; } = 25;
        public int BossConvention { get; set; } = 25;
        public int HighValueLoot { get; set; } = 20;
        public int RaiderAssault { get; set; } = 15;
        public int CultistInvasion { get; set; } = 15;

        public int GetWeight(string eventId)
        {
            return eventId switch
            {
                "normal" => NormalRaid,
                "loot_surge" => LootSurge,
                "boss_hunt" => BossHunt,
                "boss_convention" => BossConvention,
                "high_value_loot" => HighValueLoot,
                "raider_assault" => RaiderAssault,
                "cultist_invasion" => CultistInvasion,
                _ => 0
            };
        }
    }
}
