namespace RaidRoulette.TestingClient
{
    public static class OverwatchPolicy
    {
        public static bool BaselineReady(double elapsed, double quietSeconds) => elapsed >= 60 && (quietSeconds >= 10 || elapsed >= 90);
        public static int ExtraCount(int nativePoints, int budget) => (int)System.Math.Min(System.Math.Max(0, budget),
             2L * System.Math.Max(0, nativePoints));
    }
}
