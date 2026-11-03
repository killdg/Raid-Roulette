using System;

namespace RaidRoulette.Shared
{
    public static class DoorTeleportPolicy
    {
        public const float CooldownSeconds = 30f;
        public static bool Roll(int zeroBasedPercent)
        {
            return zeroBasedPercent >= 0 && zeroBasedPercent < 50;
        }

        public static bool Crossed(float before, float after, float lateral, float height, float movementSquared)
        {
            return movementSquared > 0.0001f && movementSquared < 16f && before * after < 0f && Math.Abs(before) < 1.6f && Math
                .Abs(after) < 1.6f && Math.Abs(lateral) < 0.85f && Math.Abs(height) < 1.8f;
        }
    }
}
