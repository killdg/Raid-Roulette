using RaidRoulette.Core;

namespace RaidRoulette.Quests
{
    internal static class FenceImages
    {
        // Quest artwork is cached by URL in the client. A new route namespace
        // invalidates the old placeholder images without deleting client caches,
        // changing quest IDs or resetting accepted assignments.
        internal const string RoutePrefix = "/files/quest/raidroulette-events/briefings-v2/";
        internal static void Register(string modFolder, Action<string, string> addRoute)
        {
            // Only static, bundled briefing artwork is exposed through SPT's
            // existing authenticated/local image service. No external URL fetch,
            // file upload, telemetry or directory enumeration is performed.
            var files = EventRegistry.GetEvents().Where(value => value.Id != "normal").Select(value => new { Key = RoutePrefix
                + value.Id, Path = Path.Combine(modFolder, "FenceImages", value.Id + ".png") }).ToArray();
            foreach (var file in files)
            {
                if (!File.Exists(file.Path))
                {
                    throw new FileNotFoundException("Missing RR briefing artwork. Install the complete matching package.", file.Path);
                }
            }

            // SPT removes the extension before looking up an image route.
            foreach (var file in files)
            {
                addRoute(file.Key, file.Path);
            }
        }
    }
}
