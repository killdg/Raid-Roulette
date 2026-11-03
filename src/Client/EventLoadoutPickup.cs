using System;
using EFT;

namespace RaidRoulette.TestingClient
{
    internal static class EventLoadoutPickup
    {
        internal static bool IsRestricted(Player? player)
        {
            if (player == null || player.IsYourPlayer)
            {
                return false;
            }

            if (!Plugin.RaidContext.EndsWith(": explosive_scav_units", StringComparison.Ordinal))
            {
                return false;
            }

            // Native Scavs can replace event weapons with guns from bodies.
            // Keep their pistol/launcher kit for this raid only. The shared
            // pickup hooks never change inventory contents or player looting.
            string role = player.Profile.Info.Settings.Role.ToString();
            return role.Equals("assault", StringComparison.OrdinalIgnoreCase) || role.Equals("assaultGroup", StringComparison
                .OrdinalIgnoreCase) || role.Equals("cursedAssault", StringComparison.OrdinalIgnoreCase) || role.Equals("marksman",
                 StringComparison.OrdinalIgnoreCase);
        }
    }
}
