using System.Threading;
using HarmonyLib;
using RaidRoulette.Runtime;
using RaidRoulette.Services;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace RaidRoulette.Patches
{
    // Native generation can rebuild the mod pool after our template rules, then
    // choose lower plates using level weights. Carry the event minimum only
    // through this synchronous inventory-generation call. AsyncLocal isolates
    // concurrent sessions; the finalizer restores nested scopes on success/error.
    internal static class GearedEquipmentScope
    {
        internal static readonly AsyncLocal<int> MinimumArmorClass = new();
    }

    [HarmonyPatch(typeof(BotInventoryGenerator), nameof(BotInventoryGenerator.GenerateInventory))]
    internal static class GearedInventoryScopePatch
    {
        private static void Prefix(MongoId sessionId, BotGenerationDetails botGenerationDetails, out int __state)
        {
            __state = GearedEquipmentScope.MinimumArmorClass.Value;
            var session = RaidRulesRuntime.Get(sessionId);
            if (session != null && RaidRoulette.Shared.GauntletPolicy.UsesEquipmentProgression(session.EventId))
            {
                int wave = RaidRoulette.Shared.GauntletPolicy.FindStage(session.EventId, botGenerationDetails.Role);
                GearedEquipmentScope.MinimumArmorClass.Value = wave < 0 ? 0 : RaidRoulette.Shared.GauntletPolicy.StageTier(session
                    .EventId, wave) + 2;
                return;
            }

            GearedEquipmentScope.MinimumArmorClass.Value = session?.EventId == "geared" && RaidRulesService
                .IsOrdinary(botGenerationDetails) ? RaidRulesRuntime.Config.V2.GearedBodyArmorClass : 0;
        }

        private static void Finalizer(int __state)
        {
            GearedEquipmentScope.MinimumArmorClass.Value = __state;
        }
    }

    [HarmonyPatch(typeof(BotEquipmentModGenerator), nameof(BotEquipmentModGenerator.FilterPlateModsForSlotByLevel))]
    internal static class GearedPlateSelectionPatch
    {
        private static void Postfix(string modSlot, TemplateItem armorItem, ref FilterPlateModsForSlotByLevelResult __result)
        {
            int minimum = GearedEquipmentScope.MinimumArmorClass.Value;
            var service = RaidRulesRuntime.Service;
            if (minimum <= 0 || service == null)
            {
                return;
            }

            var candidates = service.GearedPlateCandidates(armorItem, modSlot, minimum);
            if (candidates.Count == 0)
            {
                // Keep native handling for slots with no compatible high-class
                // plate. Never substitute a physically incompatible template.
                return;
            }

            if (minimum < 6)
            {
                int closest = candidates.Min(id => service.ArmorClass(id));
                candidates = candidates.Where(id => service.ArmorClass(id) == closest).ToHashSet();
            }

            __result.Result = Result.SUCCESS;
            __result.PlateModTemplates = candidates;
        }
    }
}
