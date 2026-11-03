using EFT;
using EFT.Vehicle;
using HarmonyLib;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal static class PsychBtrTracking
    {
        private static BTRTurretServer? turret;
        private static Transform? previousTarget;
        private static Vector3 previousPosition;
        internal static bool Active { get; private set; }

        internal static void Tick()
        {
            Active = true;
            var controller = BtrController.Instance;
            var current = controller?.BtrVehicle?.BTRTurret;
            if (current == null || Plugin.Local == null)
            {
                return;
            }

            if (turret != current)
            {
                Restore();
                turret = current;
                previousTarget = current.targetTransform;
                previousPosition = current.targetPosition;
                Plugin.Trace("[RR-PSYCH] stage=btr-tracking started; native route unchanged, firing blocked");
            }

            Aim(current);
            // Release an already-held trigger as well as blocking future native
            // Shoot calls. Do not add enemies, buy BTR services or change its route.
            controller?.BotShooterBtr?.ShootData?.EndShoot();
        }

        internal static void Aim(BTRTurretServer current)
        {
            if (Active && current == turret && Plugin.Local != null)
            {
                current.EnableAimingPosition(Plugin.Local.Position + Vector3.up * 1.2f);
            }
        }

        internal static bool BlockShot(BotOwner? owner)
        {
            return Active && owner?.Profile.Info.Settings.Role == WildSpawnType.shooterBTR;
        }

        internal static void Stop()
        {
            Active = false;
            Restore();
        }

        private static void Restore()
        {
            if (turret != null)
            {
                turret.targetTransform = previousTarget;
                turret.targetPosition = previousPosition;
                turret.CheckCanShoot();
            }

            turret = null;
            previousTarget = null;
        }
    }

    [HarmonyPatch(typeof(BTRTurretServer), nameof(BTRTurretServer.FixedUpdate))]
    internal static class PsychBtrAim
    {
        private static void Prefix(BTRTurretServer __instance)
        {
            PsychBtrTracking.Aim(__instance);
        }
    }

    [HarmonyPatch(typeof(BTRTurretServer), nameof(BTRTurretServer.CheckCanShoot))]
    internal static class PsychBtrTurretSafety
    {
        private static void Postfix(BTRTurretServer __instance)
        {
            if (PsychBtrTracking.Active)
            {
                __instance.IsCanShoot = false;
            }
        }
    }

    [HarmonyPatch(typeof(ShootData), nameof(ShootData.Shoot))]
    internal static class PsychBtrShotSafety
    {
        private static bool Prefix(ShootData __instance, ref bool __result)
        {
            if (!PsychBtrTracking.BlockShot(__instance._owner))
            {
                return true;
            }

            __instance.EndShoot();
            __result = false;
            return false;
        }
    }
}
