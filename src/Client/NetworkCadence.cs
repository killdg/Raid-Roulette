using System;

namespace RaidRoulette.TestingClient
{
    public static class NetworkCadence
    {
        public static double Roulette(bool enabled, bool inRaid)
        {
            return enabled ? (inRaid ? 60 : 30) : 120;
        }

        public static double Tasks(bool enabled, bool inRaid, bool active, bool awaitingMission)
        {
            if (!enabled)
            {
                return 120;
            }

            // Active observations already return mission state. Do not duplicate
            // those responses with a second stream of state requests.
            if (active)
            {
                return double.PositiveInfinity;
            }

            return inRaid ? (awaitingMission ? 2 : 60) : 15;
        }

        public static double Retry(int failures, double initial = 3)
        {
            return Math.Min(60, initial * Math.Pow(2, Math.Max(0, Math.Min(6, failures - 1))));
        }
    }
}
