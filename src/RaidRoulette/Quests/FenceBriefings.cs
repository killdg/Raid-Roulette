using System.Reflection;
using System.Text.Json;

namespace RaidRoulette.Quests
{
    internal static class FenceBriefings
    {
        private static readonly IReadOnlyDictionary<string, string> summaries = Load();
        private static IReadOnlyDictionary<string, string> Load()
        {
            // Bundle player-facing copy with this exact build. Reading this named
            // assembly resource performs no external file or network access.
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("RaidRoulette.FenceBriefings.json")
                ?? throw new InvalidOperationException("Bundled Fence briefing text is missing.");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(resource)
                ?? throw new InvalidOperationException("Bundled Fence briefing text is invalid.");
        }

        internal static string Get(string assignmentId)
        {
            return summaries.TryGetValue(assignmentId, out string? summary) ? summary
                : throw new InvalidOperationException("Missing Fence briefing: " + assignmentId);
        }
    }
}
