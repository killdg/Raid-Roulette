using System.Reflection;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    [HarmonyPatch(typeof(LookSensor), "UpdateLook")]
    internal static class LabStaffVision
    {
        private static readonly FieldInfo OwnerField = AccessTools.Field(typeof(LookSensor), "_botOwner");
        private static readonly FieldInfo CompletedField = AccessTools.Field(typeof(LookSensor), "_isTaskCompleted");
        private static bool Prefix(LookSensor __instance)
        {
            var owner = OwnerField.GetValue(__instance) as EFT.BotOwner;
            if (!LabWorkDirector.IsStaff(owner?.GetPlayer))
            {
                return true;
            }

            // Native periodic combat vision reads the firearm's BifacialTransform.
            // Unarmed civilians have no weapon root; the resulting exception aborts
            // the shared AI task batch and can freeze unrelated fighters as well.
            // Staff use nearby shot/damage events for panic, not combat targeting.
            // Complete their vision task normally rather than disabling the scheduler.
            CompletedField.SetValue(__instance, true);
            return false;
        }
    }
}
