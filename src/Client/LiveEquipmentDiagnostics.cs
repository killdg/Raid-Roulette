using System;
using System.Linq;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;

namespace RaidRoulette.TestingClient
{
    /// <summary>
    /// Records actual generated equipment for the Geared validation run.
    /// </summary>
    internal static class LiveEquipmentDiagnostics
    {
        internal static void Write(ManualLogSource log, Player player, string raidContext)
        {
            if (!Plugin.TraceEnabled || !raidContext.EndsWith(": geared", StringComparison.Ordinal))
            {
                return;
            }

            // Template IDs include nested plates, magazine rounds and carried ammunition.
            // Resolve these IDs against the local SPT database during review rather than
            // reflecting native item templates every frame. This writes only to the
            // existing local diagnostic log and never changes inventory or sends data.
            // The caller invokes this once per observed AI; its seen set resets per world.
            var equipment = player.Profile.Inventory.Equipment.GetAllItems().Take(256).GroupBy(item => item.TemplateId
                .ToString(), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
                 $"{group.Key}:{group.Count()}");
            log.LogInfo($"[RR-GEAR] stage=live-equipment bot={player.ProfileId} "
                + $"templates=[{string.Join(",", equipment)}] " + "counts=item-instances-not-round-quantities limit=256");
        }
    }
}
