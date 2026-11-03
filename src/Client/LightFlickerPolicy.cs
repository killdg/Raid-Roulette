using System;

namespace RaidRoulette.TestingClient
{
    public static class LightFlickerPolicy
    {
        public const double Duration = 1.15;
        public static bool Night(double hour) => hour >= 20 && hour < 24 || hour >= 0 && hour < 6;
        public static double Gain(double elapsed, double stagger)
        {
            double t = elapsed - Math.Max(0, Math.Min(0.12, stagger));
            if (t < 0 || t >= 0.98)
            {
                return 1;
            }

            if (t < 0.12)
            {
                return 0.06;
            }

            if (t < 0.25)
            {
                return 0.8;
            }

            if (t < 0.35)
            {
                return 0.02;
            }

            if (t < 0.55)
            {
                return 0.55;
            }

            if (t < 0.64)
            {
                return 0.05;
            }

            return Math.Min(1, 0.55 + (t - 0.64) / 0.34 * 0.45);
        }
    }
}
