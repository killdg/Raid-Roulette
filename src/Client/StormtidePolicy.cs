using System;

namespace RaidRoulette.TestingClient
{
    public static class StormtidePolicy
    {
        public static float Progress(double elapsed, double duration) => duration <= 0 || double.IsNaN(duration) || double
            .IsInfinity(duration) || double.IsNaN(elapsed) || double.IsInfinity(elapsed) ? 0 : (float)Math.Max(0, Math.Min(1,
                 elapsed / 300));
        public static bool IsNight(DateTime start) => start.Hour >= 21 || start.Hour < 6;
        public static DateTime ClockAt(DateTime start, float progress) => IsNight(start) ? start : start
            .AddTicks((long)((start.Date.AddHours(23) - start).Ticks * (double)Math.Max(0, Math.Min(1, progress))));
        public static float OtherSoundGain(float progress) => 1f - 0.75f * Math.Max(0, Math.Min(1, progress));
        public static float RainSoundGain(float progress) => 1f + Math.Max(0, Math.Min(1, progress));
    }
}
