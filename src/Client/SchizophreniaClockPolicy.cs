using System;

namespace RaidRoulette.TestingClient
{
    public static class SchizophreniaClockPolicy
    {
        public static double ScheduledAt(double duration, double roll) => duration < 140 || double.IsNaN(duration)
            || double.IsInfinity(duration) ? double.PositiveInfinity : 60 + Math.Max(0, Math.Min(1, roll)) * (duration - 130);
        public static DateTime Shift(DateTime original) => original.AddHours(12);
        public const double Duration = 60;
    }
}
