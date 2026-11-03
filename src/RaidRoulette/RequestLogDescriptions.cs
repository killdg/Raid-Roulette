using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.AspNetCore.Http;

namespace RaidRoulette.Logging
{
    /// <summary>
    /// Adds an explanation to this mod's existing SPT console request lines.
    /// This hook changes only the path string read by the logging method; it
    /// never changes HttpRequest.Path, routing, request bodies or responses.
    /// </summary>
    [HarmonyPatch]
    internal static class RequestLogDescriptions
    {
        private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
        {
            ["/client/raidroulette/testing"] = "Syncing raid selection and testing settings",
            ["/client/raidroulette/gungame"] = "Processing Gun Game progression and equipment",
            ["/client/raidroulette/keyed-room-kits"] = "Preparing premium loot for locked rooms",
            ["/client/raidroulette/fence-trial"] = "Recording assigned Trials completion for Fence"
        };
        private static MethodBase TargetMethod()
        {
            // The middleware lives in the SPT executable, not Server.Core.
            // Resolve the public logging method after the server has loaded it.
            return AccessTools.Method("SPTarkov.Server.Middleware.SptLoggerMiddleware:LogRequest")
                ?? throw new MissingMethodException("SPT request logging middleware was not found.");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo pathValue = AccessTools.PropertyGetter(typeof(PathString), nameof(PathString.Value));
            MethodInfo describe = AccessTools.Method(typeof(RequestLogDescriptions), nameof(Describe));
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(pathValue))
                {
                    // Includes local and remote HTTP log branches. WebSocket
                    // routes and routes owned by other mods pass through unchanged.
                    yield return new CodeInstruction(OpCodes.Call, describe);
                }
            }
        }

        internal static string? Describe(string? path)
        {
            return path != null && Descriptions.TryGetValue(path, out string? description) ? path + " | " + description : path;
        }
    }
}
