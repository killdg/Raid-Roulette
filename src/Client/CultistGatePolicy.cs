using System;

namespace RaidRoulette.TestingClient
{
    public static class CultistGatePolicy
    {
        public static bool IsEventWave(bool confirmedEvent, bool forced, bool ignoreMax, string boss, string escort,
             double time) => confirmedEvent && forced && ignoreMax && boss == "sectantPriest" && escort == "sectantWarrior"
            && time > 0 && time % 15 == 0;
        public static bool IsCultistRole(string role) => role is "sectantPriest" or "sectantWarrior";
    }
}
