using System;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace RaidRoulette.TestingClient
{
    // These are roaming goals, not spawn/teleport positions. Existing population
    // and safe-spawn rules remain responsible for actual reinforcements.
    internal static class GunGamePressure
    {
        private const float OuterDistanceMetres = 22.86f;
        private const float TargetDistanceMetres = 19f;
        internal static bool Applies(string context)
        {
            return context.EndsWith(": gun_game", StringComparison.Ordinal) && (context.StartsWith("laboratory:",
                 StringComparison.OrdinalIgnoreCase) || context.StartsWith("sandbox:", StringComparison.OrdinalIgnoreCase)
                || context.StartsWith("sandbox_high:", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool TryTarget(BotOwner bot, Vector3 playerPosition, out Vector3 target)
        {
            target = default;
            if (Vector3.Distance(bot.Position, playerPosition) <= OuterDistanceMetres)
            {
                return false;
            }

            Vector3 approach = bot.Position - playerPosition;
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f)
            {
                approach = Vector3.forward;
            }

            approach.Normalize();
            // Start on the bot's side of the player, then try nearby directions.
            // Sample only the player's floor; the shared director validates the
            // complete route and locked-door links before issuing movement.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = attempt == 0 ? 0f : (attempt % 2 == 0 ? -1f : 1f) * attempt * 35f;
                Vector3 candidate = playerPosition + Quaternion.Euler(0f, angle, 0f) * approach * TargetDistanceMetres;
                if (NavMesh.SamplePosition(candidate, out var floor, 2f, NavMesh.AllAreas) && Mathf.Abs(floor.position
                    .y - playerPosition.y) < 1.5f && Vector3.Distance(floor.position, playerPosition) >= 15.24f && Vector3
                    .Distance(floor.position, playerPosition) <= OuterDistanceMetres)
                {
                    target = floor.position;
                    return true;
                }
            }

            return false;
        }
    }
}
