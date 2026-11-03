using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RaidRoulette.LanguagePack;

namespace RaidRoulette.LanguagePackChecks
{
    internal static class Program
    {
        private static int checks;

        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Main(string[] args)
        {
            string folder = Path.GetFullPath(args[0]);
            var store = new CatalogStore(folder, value => JsonSerializer.Deserialize<LocaleCatalog>(value)!);
            string[] codes =
            {
                "ch", "cz", "es-mx", "es", "fr", "ge", "hu", "it",
                "jp", "kr", "pl", "po", "ro", "ru", "sk", "tu"
            };
            var registered = store.English.Events.Where(pair => pair.Key != "normal")
                .ToDictionary(pair => CatalogStore.QuestId(pair.Key) + " name", pair => pair.Value.Name);
            string[] englishKeys = store.English.Events.Keys.Order().ToArray();
            Require(englishKeys.Length == 44, "43 assignments plus Normal are required.");
            Require(Directory.GetFiles(folder, "*.json").Length == 17, "Expected 17 catalogs.");
            Require(store.Get("unknown") == store.English, "Unknown languages must use English.");
            Require(store.Text("en", "unrecognized") == "unrecognized", "English must pass through.");
            Require(store.FenceEntries("en", registered).Count == 0, "Do not override English quest prose.");
            Require(store.FenceEntries("unknown", registered).Count == 0, "Do not register unknown locales.");
            foreach (string code in codes)
            {
                LocaleCatalog catalog = store.Get(code);
                Require(catalog.Language == code, "Missing catalog: " + code);
                Require(catalog.Events.Keys.Order().SequenceEqual(englishKeys), "Event IDs differ: " + code);
                Require(store.Text(code, "unrecognized") == "unrecognized", "Unknown strings must pass through.");
                Require(store.Text(code, "Raid Event : {0}").Count(character => character == '{') == 1,
                    "Notice placeholder damaged: " + code);
                Require(string.Format(store.Text(code, "Raid Event : {0}"), "example").Contains("example"),
                    "Notice formatting failed: " + code);
                Require(store.FenceEntries(code, new Dictionary<string, string>()).Count == 0,
                    "Must not create absent assignments: " + code);
                Dictionary<string, string> overlay = store.FenceEntries(code, registered);
                Require(overlay.Count == 43 * 8, "Wrong Fence overlay size: " + code);
                foreach (KeyValuePair<string, EventTranslation> pair in catalog.Events)
                {
                    Require(!string.IsNullOrWhiteSpace(pair.Value.Name), "Blank title: " + code + pair.Key);
                    Require(!string.IsNullOrWhiteSpace(pair.Value.Description), "Blank briefing: " + code + pair.Key);
                    Require(pair.Value.Description != store.English.Events[pair.Key].Description,
                        "Untranslated briefing: " + code + pair.Key);
                    Require(!pair.Value.Description.Contains('\uFFFD'), "Invalid Unicode replacement character.");
                    Require(store.EventName(code, pair.Key) == pair.Value.Name, "Event title lookup failed.");
                    if (pair.Key != "normal")
                    {
                        string questId = CatalogStore.QuestId(pair.Key);
                        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("killdg/rr/fence/v1/" + pair.Key + "/quest"));
                        Require(questId == Convert.ToHexString(hash[..12]).ToLowerInvariant(), "Stable quest ID mismatch.");
                        Require(overlay[questId + " name"] == "[RR: " + pair.Value.Name + "]", "Quest title mismatch.");
                        Require(overlay[questId + " description"] == pair.Value.Description, "Quest briefing mismatch.");
                    }
                }

                foreach (string key in store.Get("fr").Text.Keys)
                {
                    Require(catalog.Text.ContainsKey(key), "Missing common string: " + code + key);
                }
            }

            Require(CatalogStore.Normalize("zh-CN") == "ch", "Chinese alias.");
            Require(CatalogStore.Normalize("de") == "ge", "German alias.");
            Require(CatalogStore.Normalize("pt_BR") == "po", "Portuguese alias.");
            Require(store.Text("fr", "[RR: Winter]") == "[RR: Hiver]", "Map title decoration.");
            Require(store.Text("fr", "Winter ▼") == "Hiver ▼", "Picker decoration.");
            // A community-edited catalog may omit an event. That assignment
            // must still receive its English text rather than a blank title.
            LocaleCatalog french = store.Get("fr");
            EventTranslation saved = french.Events["winter"];
            french.Events.Remove("winter");
            Require(store.EventName("fr", "winter") == "Winter", "Missing event title fallback.");
            Require(store.FenceEntries("fr", registered)[CatalogStore.QuestId("winter") + " description"]
                == store.English.Events["winter"].Description, "Missing briefing fallback.");
            french.Events["winter"] = saved;
            Console.WriteLine($"{checks} catalog/overlay checks passed. No gameplay or native font rendering tested.");
        }
    }
}
