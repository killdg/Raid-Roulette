using System.Reflection;
using System.Text.Json;

namespace RaidRoulette.Configuration;

public sealed class ConfigManager
{
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string configPath;

    public ConfigManager()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var modDirectory = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            throw new InvalidOperationException(
                "The Raid Roulette mod directory could not be determined.");
        }

        configPath = Path.Combine(modDirectory, ConfigFileName);
    }

    public RaidRouletteConfig Config { get; private set; } = new();

    public string? LastWarning { get; private set; }

    public void Load()
    {
        LastWarning = null;

        if (!File.Exists(configPath))
        {
            Config = new RaidRouletteConfig();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                HandleInvalidConfig("The configuration file was empty.");
                return;
            }

            Config =
                JsonSerializer.Deserialize<RaidRouletteConfig>(
                    json,
                    JsonOptions)
                ?? new RaidRouletteConfig();
        }
        catch (JsonException exception)
        {
            HandleInvalidConfig(
                $"The configuration file contained invalid JSON: {exception.Message}");
        }
        catch (IOException exception)
        {
            Config = new RaidRouletteConfig();

            LastWarning =
                $"The configuration file could not be read. Default settings will be used. " +
                $"Reason: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            Config = new RaidRouletteConfig();

            LastWarning =
                $"Access to the configuration file was denied. Default settings will be used. " +
                $"Reason: {exception.Message}";
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Config, JsonOptions);
        File.WriteAllText(configPath, json);
    }

    private void HandleInvalidConfig(string reason)
    {
        var backupPath = CreateBackupPath();

        try
        {
            File.Copy(configPath, backupPath, overwrite: false);

            LastWarning =
                $"{reason} The original file was backed up as " +
                $"\"{Path.GetFileName(backupPath)}\" and defaults were restored.";
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            LastWarning =
                $"{reason} A backup could not be created. Default settings will be used.";
        }

        Config = new RaidRouletteConfig();

        try
        {
            Save();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            LastWarning +=
                $" The replacement configuration could not be saved: {exception.Message}";
        }
    }

    private string CreateBackupPath()
    {
        var modDirectory =
            Path.GetDirectoryName(configPath)
            ?? throw new InvalidOperationException(
                "The configuration directory could not be determined.");

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        return Path.Combine(
            modDirectory,
            $"config.invalid-{timestamp}.json");
    }
}