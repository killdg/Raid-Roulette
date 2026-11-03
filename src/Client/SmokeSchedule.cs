using System;

namespace RaidRoulette.TestingClient
{
    public static class SmokeSchedule
    {
        public static float Delay(float emission, float fuse)
        {
            float halfLife = Math.Max(1, emission) * 0.5f + Math.Max(0, fuse);
            return halfLife;
        }
    }
}
