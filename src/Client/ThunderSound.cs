using System;

namespace RaidRoulette.TestingClient
{
    public static class ThunderPolicy
    {
        public const string SourceName = "RR_StormtideThunder";
        public static double Delay(double random) => 0.8 + 2.2 * Clamp(random);
        public static float Flash(double seconds)
        {
            if (seconds >= 0 && seconds < 0.07)
            {
                return (float)(1 - seconds / 0.07);
            }

            if (seconds >= 0.13 && seconds < 0.19)
            {
                return (float)(0.55 * (1 - (seconds - 0.13) / 0.06));
            }

            return 0;
        }

        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
    }

    public sealed class ThunderSchedule
    {
        public double NextRollAt { get; private set; } = 480;
        public int Rolls { get; private set; }

        public void Reset()
        {
            NextRollAt = 480;
            Rolls = 0;
        }

        public bool Tick(double elapsed, bool available, double roll)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < NextRollAt)
            {
                return false;
            }

            NextRollAt = elapsed + 180;
            Rolls++;
            return available && roll < 0.33;
        }
    }
}
