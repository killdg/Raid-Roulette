using System;

namespace RaidRoulette.TestingClient
{
    public static class TimeWarpPolicy
    {
        public const int Cycles = 6;
        public static double Rate(double raidSeconds) => double.IsNaN(raidSeconds) || double.IsInfinity(raidSeconds)
            || raidSeconds <= 0 ? 0 : Cycles * 86400d / raidSeconds;
        public static double GameSeconds(double elapsed, double raidSeconds) => double.IsNaN(elapsed) || double
            .IsInfinity(elapsed) ? 0 : Math.Clamp(elapsed, 0, Math.Max(0, raidSeconds)) * Rate(raidSeconds);
    }
}
