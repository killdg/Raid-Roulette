using System.Reflection;
using RaidRoulette.Configuration;
using RaidRoulette.Runtime;
using RaidRoulette.Services;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace RaidRoulette
{
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.killdg.raidroulette";
        public string Name { get; init; } = "Raid Roulette";
        public string Author { get; init; } = "killdg";
        public List<string>? Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new("2.0.0-preview.68");
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.6");
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public string? Url { get; init; }
        public bool HasPrepatcher { get; init; }
        public string License { get; init; } = "MIT";
    }

    [Injectable(TypePriority = OnLoadOrder.PostLoad + 10)]
    public sealed class RaidRouletteMod(ISptLogger<RaidRouletteMod> logger, LocationTable locationTable,
         TemplateTable templateTable, GlobalTable globalTable, BotTable botTable, ICloner cloner, SPTarkov.Server.Core
        .Servers.SaveServer saveServer, SPTarkov.Server.Core.Services.Commerce.MailSendService mailSendService, SPTarkov
        .Server.Core.Utils.JsonUtil jsonUtil, SPTarkov.Server.Core.Controllers.QuestController questController, SPTarkov
        .Server.Core.Services.Modding.Custom.CustomQuestService customQuestService, SPTarkov.Server.Core.Models.Spt.Config
        .SeasonalEventConfig seasonalEventConfig, SPTarkov.Server.Core.Helpers.Quest.QuestHelper questHelper, SPTarkov
        .Server.Core.Routers.ImageRouter imageRouter, SPTarkov.Server.Core.Generators.Weather.WeatherGenerator weatherGenerator) : IOnLoad
    {
        private const string HarmonyId = "com.killdg.raidroulette";
        private const string DisplayName = "[Raid Roulette]";
        private const string DisplayVersion = "2.0.0-preview.68";
        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var configManager = new ConfigManager();
                configManager.Load();
                if (!string.IsNullOrWhiteSpace(configManager.LastWarning))
                {
                    logger.Warning($"{DisplayName} {configManager.LastWarning}");
                }

                if (!configManager.Config.Enabled)
                {
                    logger.Warning($"{DisplayName} Disabled in config.json.");
                    return Task.CompletedTask;
                }

                CloneProfileMaintenance.Initialize(saveServer);
                GunGameRuntime.Initialize(new EventArsenal(templateTable, globalTable, cloner), saveServer, mailSendService,
                     jsonUtil, cloner);
                HighValueRoomRuntime.Initialize(templateTable, globalTable, cloner);
                GrenadierRuntime.Initialize(new GrenadierService(templateTable, cloner));
                GiftRuntime.Initialize(new GiftService(templateTable, botTable, cloner));
                OverloadedRuntime.Initialize(new OverloadedService(templateTable, globalTable, cloner));
                WeaponCategoryRuntime.Initialize(new WeaponCategoryService(templateTable, globalTable, cloner));
                SmokersRuntime.Initialize(new SmokersService(templateTable, cloner));
                CloneWarsRuntime.Initialize(
                    new CloneWarsService(cloner),
                    session => saveServer.GetProfile(session)?.CharacterData?.PmcData,
                    message => logger.Info(message)
                );
                RaidDiagnostics.Initialize(message => logger.Info(message), templateTable);
                var lootService = new LootService(locationTable);
                var bossSpawnService = new BossSpawnService(locationTable);
                RaidRulesRuntime.Initialize(
                    configManager.Config,
                    new RaidRulesService(templateTable, botTable, locationTable, cloner, message => logger.Warning(message))
                );
                RaidRouletteRuntime.Initialize(
                    configManager.Config,
                    lootService,
                    bossSpawnService,
                    message => logger.Info(message),
                    message => logger.Warning(message)
                );
                SeasonalRaidIsolation.Initialize(locationTable, seasonalEventConfig);
                SeasonRaidWeather.Initialize(weatherGenerator);
                var harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                EventIntegration.Ready = true;
                Quests.FenceImages.Register(Path.GetDirectoryName(typeof(RaidRouletteMod).Assembly.Location)!, imageRouter.AddRoute);
                Quests.FenceEvents.InitializeRetries(questHelper);
                Quests.FenceEvents.Initialize(saveServer, questController, customQuestService, jsonUtil, message => logger.Info(message));
                logger.Success($"{DisplayName} v{DisplayVersion} loaded.");
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                logger.Error($"{DisplayName} Failed to load: {exception.Message}");
                return Task.CompletedTask;
            }
        }
    }
}
