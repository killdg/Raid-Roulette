using System;
using EFT;
using EFT.Ballistics;
using HarmonyLib;

namespace RaidRoulette.TestingClient
{
    internal static class GauntletSpawnOwnership
    {
        internal static bool Exclusive => Plugin.RaidContext.EndsWith(": trials", StringComparison.Ordinal) || Plugin
            .RaidContext.EndsWith(": extreme_trials", StringComparison.Ordinal) || Plugin.RaidContext.EndsWith(": snowball",
             StringComparison.Ordinal) || Plugin.RaidContext.EndsWith(": guard_trials", StringComparison.Ordinal) || Plugin
            .RaidContext.EndsWith(": total_trial", StringComparison.Ordinal);

        internal static bool Managed(BotCreationData data)
        {
            return LocalRaidDirector.Creating.TryGetValue(data, out var request) && request.Wave == -6 && !request.Failed;
        }
    }

    // Ordinary map waves bypass BotBossSpawn.TrySpawn. Reject them before their
    // positions enter the native pending-activation count.
    [HarmonyPatch(typeof(BotSpawner), nameof(BotSpawner.SpawnBotsInZoneOnPositions))]
    internal static class GauntletOrdinaryWaveGate
    {
        private static bool Prefix(BotCreationData __2)
        {
            if (RaidRoulette.Shared.GauntletPolicy.AllowActivation(GauntletSpawnOwnership.Exclusive, GauntletSpawnOwnership.Managed(__2)))
            {
                return true;
            }

            __2.StopSpawn();
            Plugin.Trace("[RR-GAUNTLET] blocked ordinary position wave count=" + __2.Count);
            return false;
        }
    }

    // SPT 4.1.6's method_10 is the final queued BotCreationData activation pass.
    // This catches delayed/seasonal paths that did not use the public wave method.
    [HarmonyPatch(typeof(BotSpawner), "method_10")]
    internal static class GauntletQueuedActivationGate
    {
        private static bool Prefix(BotSpawner __instance, BotCreationData __1)
        {
            if (RaidRoulette.Shared.GauntletPolicy.AllowActivation(GauntletSpawnOwnership.Exclusive, GauntletSpawnOwnership.Managed(__1)))
            {
                return true;
            }

            int reserved = __1.CountPositions;
            __1.StopSpawn();
            __instance._inSpawnProcess = Math.Max(0, __instance._inSpawnProcess - reserved);
            Plugin.Trace("[RR-GAUNTLET] blocked unmanaged queued activation count=" + __1.Count);
            return false;
        }
    }

    // Exclusive gauntlets request their own finite roster. Map/seasonal role
    // blocks must not reject a requested boss or cultist on a different map.
    // Unmanaged native boss waves are rejected by WaveAttemptObserver first.
    [HarmonyPatch(typeof(BotSpawner), nameof(BotSpawner.CanSpawnRole))]
    internal static class GauntletRoleGate
    {
        private static bool Prefix(ref bool __result)
        {
            if (!GauntletDirector.Active)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BotZonesLeaveController), "IsZoneBlockFor")]
    internal static class GauntletZoneGate
    {
        private static void Postfix(ref bool __result)
        {
            if (GauntletDirector.Active)
            {
                __result = false;
            }
        }
    }

    // Prevent hunters' stray rounds and grenades from spoiling a required kill.
    // Player damage and environmental damage retain their native behaviour.
    [HarmonyPatch(typeof(Player), nameof(Player.ApplyDamageInfo))]
    internal static class GauntletFriendlyDamage
    {
        private static bool Prefix(Player __instance, DamageInfo damageInfo)
        {
            return !(GauntletDirector.IsActor(__instance) && GauntletDirector.IsActor(damageInfo.Player?.iPlayer));
        }
    }
}
