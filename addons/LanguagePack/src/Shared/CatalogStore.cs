using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RaidRoulette.LanguagePack
{
    public sealed class EventTranslation
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class LocaleCatalog
    {
        public string Language { get; set; } = "en";
        public Dictionary<string, EventTranslation> Events { get; set; } = new();
        public Dictionary<string, string> Text { get; set; } = new();
    }

    public sealed class CatalogStore
    {
        private readonly Dictionary<string, LocaleCatalog> catalogs;
        public LocaleCatalog English => catalogs["en"];

        public CatalogStore(string directory, Func<string, LocaleCatalog> deserialize)
        {
            // Read only bundled translation JSON from this add-on's own folder.
            // No web translation service, file watcher or executable data is used.
            catalogs = Directory.EnumerateFiles(directory, "*.json")
                .Select(path => deserialize(File.ReadAllText(path)))
                .ToDictionary(catalog => catalog.Language, StringComparer.OrdinalIgnoreCase);
            if (!catalogs.ContainsKey("en"))
            {
                throw new InvalidDataException("The bundled English fallback catalog is missing.");
            }
        }

        public static string Normalize(string? language)
        {
            string value = (language ?? "en").Replace('_', '-').ToLowerInvariant();
            return value switch
            {
                "de" => "ge",
                "zh" or "zh-cn" or "zh-hans" => "ch",
                "ja" => "jp",
                "ko" => "kr",
                "pt" or "pt-br" => "po",
                "cs" => "cz",
                "tr" => "tu",
                _ => value
            };
        }

        public LocaleCatalog Get(string? language)
        {
            return catalogs.TryGetValue(Normalize(language), out var catalog) ? catalog : English;
        }

        public string Text(string? language, string english)
        {
            var catalog = Get(language);
            if (ReferenceEquals(catalog, English) || string.IsNullOrEmpty(english))
            {
                return english;
            }

            if (catalog.Text.TryGetValue(english, out string? translated) && !string.IsNullOrWhiteSpace(translated))
            {
                return translated;
            }

            foreach (var pair in English.Events)
            {
                if (string.Equals(pair.Value.Name, english, StringComparison.OrdinalIgnoreCase))
                {
                    return EventName(language, pair.Key);
                }
            }

            // These are display decorations, never config values or saved IDs.
            foreach (string suffix in new[] { " ▲", " ▼", " (%)" })
            {
                if (english.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return Text(language, english.Substring(0, english.Length - suffix.Length)) + suffix;
                }
            }

            if (english.StartsWith("[RR: ", StringComparison.Ordinal)
                && english.EndsWith("]", StringComparison.Ordinal))
            {
                return "[RR: " + Text(language, english.Substring(5, english.Length - 6)) + "]";
            }

            if (english.StartsWith("RR: ", StringComparison.Ordinal))
            {
                return "RR: " + Text(language, english.Substring(4));
            }

            return english;
        }

        public string EventName(string? language, string id)
        {
            var catalog = Get(language);
            if (catalog.Events.TryGetValue(id, out var translated) && !string.IsNullOrWhiteSpace(translated.Name))
            {
                return translated.Name;
            }

            return English.Events.TryGetValue(id, out var original) ? original.Name : id;
        }

        public Dictionary<string, string> FenceEntries(
            string language,
            IDictionary<string, string> registeredEnglish)
        {
            var result = new Dictionary<string, string>();
            LocaleCatalog catalog = Get(language);
            if (ReferenceEquals(catalog, English))
            {
                return result;
            }

            foreach (KeyValuePair<string, EventTranslation> pair in English.Events)
            {
                string id = QuestId(pair.Key);
                if (pair.Key == "normal" || !registeredEnglish.ContainsKey(id + " name"))
                {
                    continue;
                }

                result[id + " name"] = "[RR: " + EventName(language, pair.Key) + "]";
                string description = catalog.Events.TryGetValue(pair.Key, out EventTranslation? translated)
                    && !string.IsNullOrWhiteSpace(translated.Description)
                    ? translated.Description
                    : pair.Value.Description;
                result[id + " description"] = description;
                foreach (KeyValuePair<string, string> field in FenceFields)
                {
                    result[id + " " + field.Key] = Text(language, field.Value);
                }

                bool trials = pair.Key is "trials" or "extreme_trials" or "guard_trials" or "total_trial" or "snowball";
                result[QuestId(pair.Key, "condition")] = Text(language,
                    trials ? "Clear every wave." : "Play one raid.");
            }

            return result;
        }

        private static readonly Dictionary<string, string> FenceFields = new()
        {
            ["startedMessageText"] = "It's arranged.",
            ["successMessageText"] = "We're done here.",
            ["failMessageText"] = "Another time, then.",
            ["acceptPlayerMessage"] = "I will take that assignment.",
            ["completePlayerMessage"] = "The raid is over."
        };

        public static string QuestId(string eventId, string suffix = "quest")
        {
            // Matches RR's existing stable Fence namespace. Localized text must
            // never create a new assignment, change event IDs or reset progress.
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("killdg/rr/fence/v1/" + eventId + "/" + suffix));
            return string.Concat(hash.Take(12).Select(value => value.ToString("x2")));
        }
    }
}
