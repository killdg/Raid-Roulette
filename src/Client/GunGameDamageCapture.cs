using EFT.HealthSystem;
using EFT;
using EFT.Ballistics;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    [HarmonyPatch(typeof(ActiveHealthController), nameof(ActiveHealthController.ApplyDamage),
         new[] { typeof(EBodyPart), typeof(float), typeof(DamageInfo) })]
    internal static class GunGameDamageCapture
    {
        private static void Prefix(ActiveHealthController __instance, float __1, DamageInfo __2)
        {
            if (__1 > 0 && __instance.Player != null)
            {
                GunGameDirector.Current?.ObserveDamage(__instance.Player, __2);
            }
        }
    }
}
