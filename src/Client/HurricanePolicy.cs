using System;

namespace RaidRoulette.TestingClient
{
    public static class HurricanePolicy
    {
        public static bool NightDebris(double hour) => LightFlickerPolicy.Night(hour);
        public const int MaxLarge = 6;
        public const int RayBudget = 4;
        public static double Gust(double elapsed)
        {
            double phase = elapsed % 32;
            if (phase < 0)
            {
                phase += 32;
            }

            if (phase < 9 || phase > 23)
            {
                return 0;
            }

            double t = (phase - 9) / 14;
            return Math.Sin(Math.PI * t) * Math.Sin(Math.PI * t);
        }

        public static double Wind(double strength, double gust) => Math.Min(0.98, 0.25 + 0.45 * Clamp(strength)
            + 0.28 * Clamp(gust) * Clamp(strength));
        public static double DropRate(double strength, double gust) => 1 + Clamp(strength) * (1 + 0.75 * Clamp(gust));
        public static int Limit(int requested) => Math.Max(12, Math.Min(96, requested));
        public static double DebrisHeight(double groundHeight, double roll) => groundHeight + 1.4 + 6.6 * Clamp(roll);
        public static double DistanceFade(double distance) => 1 - Clamp((distance - 100) / 50);
        public static double Fade(double age, double life) => Math.Max(0, Math.Min(1, Math.Min(age / 3, (life - age) / 3)));
        private static double Clamp(double x) => Math.Max(0, Math.Min(1, x));
    }
}
