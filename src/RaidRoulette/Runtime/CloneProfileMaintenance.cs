using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers;
using HarmonyLib;

namespace RaidRoulette.Runtime
{
    internal static class CloneProfileMaintenance
    {
        private static SaveServer? save;
        internal static void Initialize(SaveServer server) => save = server;
        internal static void Repair(MongoId session)
        {
            // The optional pack additionally migrates its provenance ledger.
            if (EventIntegration.Active)
            {
                return;
            }

            var profile = save?.GetProfile(session)?.CharacterData?.PmcData;
            if (profile != null)
            {
                Services.CloneDogtagRepair.Apply(profile);
            }
        }
    }

    [HarmonyPatch(typeof(SPTarkov.Server.Core.Controllers.ProfileController), "GetCompleteProfile")]
    internal static class CloneProfileMaintenancePatch
    {
        private static void Prefix(MongoId sessionId) => CloneProfileMaintenance.Repair(sessionId);
    }
}
