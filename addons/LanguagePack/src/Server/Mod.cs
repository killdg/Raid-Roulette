using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RaidRoulette.LanguagePack;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;

namespace RaidRoulette.LanguagePackServer
{
    public record Metadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.killdg.raidroulette.languages";
        public string Name { get; init; } = "Raid Roulette Language Pack";
        public string Author { get; init; } = "killdg";
        public List<string>? Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.6");
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
        {
            ["com.killdg.raidroulette"] = new(">=2.0.0-preview.68 <3.0.0")
        };
        public string? Url { get; init; }
        public bool HasPrepatcher { get; init; }
        public string License { get; init; } = "MIT";
    }

    [Injectable(TypePriority = OnLoadOrder.PostLoad + 20)]
    public sealed class Mod(ISptLogger<Mod> logger, LocaleTable locales) : IOnLoad
    {
        internal static CatalogStore Catalogs = null!;
        internal static HashSet<string> Available = new(StringComparer.OrdinalIgnoreCase);

        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            Catalogs = new CatalogStore(Path.Combine(folder, "Locales"), text =>
                JsonSerializer.Deserialize<LocaleCatalog>(text)
                ?? throw new InvalidDataException("Invalid language catalog."));
            Available = locales.Global.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            new Harmony("com.killdg.raidroulette.languages.server").PatchAll();
            logger.Info("[RR Languages] Offline Fence translations loaded; English remains the fallback.");
            return Task.CompletedTask;
        }
    }

    [HarmonyPatch(typeof(LocaleService), nameof(LocaleService.GetLocaleDb))]
    internal static class FenceTranslations
    {
        private static void Postfix(LocaleService __instance, string? language, Dictionary<string, string> __result)
        {
            string code = CatalogStore.Normalize(language ?? __instance.GetDesiredGameLocale());
            if (code == "en" || !Mod.Available.Contains(code))
            {
                return;
            }

            LocaleCatalog catalog = Mod.Catalogs.Get(code);
            if (ReferenceEquals(catalog, Mod.Catalogs.English))
            {
                return;
            }

            // RR registers its original assignments in English only. Consult
            // that bank to confirm the stable assignment exists before adding
            // localized keys to another bank. The English call exits above,
            // so this cannot recursively translate the English fallback.
            Dictionary<string, string> registered = __instance.GetLocaleDb("en");

            // Native LocaleService owns this cached dictionary. Modify only RR's
            // existing quest keys while serving that locale; do not edit locale
            // files, profiles or other mods' strings. The loop is catalog-bounded.
            lock (__result)
            {
                foreach (KeyValuePair<string, string> entry in Mod.Catalogs.FenceEntries(code, registered))
                {
                    __result[entry.Key] = entry.Value;
                }
            }
        }
    }
}
