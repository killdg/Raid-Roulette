using System;

namespace RaidRoulette.TestingClient
{
    public static class GunGameSpawnPolicy
    {
        public static int MinimumPopulation(int cap) => Math.Max(1, (Math.Max(1, cap) + 1) / 2);
        public static bool CanSubmit(bool ready, bool won, bool alive, double elapsed, int ordinary, int pending,
             int total, int cap, int target = 4) => ready && !won && alive && elapsed >= 5 && ordinary + pending < target
            && pending < Math.Min(4, target) && total + pending < cap;
    }
}
