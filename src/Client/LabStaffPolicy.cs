using System;

namespace RaidRoulette.TestingClient
{
    public static class LabStaffPolicy
    {
        public const int DefaultPenalty = 1000;
        public static int Award(int earned, int requested) => Math.Max(0, Math.Max(0, earned) - Math.Max(0, requested));
        public static bool IsStaff(string role, string name) => role == "gifter" && name == "Lab Staff";
        public static bool Panic(double now, double heardAt, double distance) => now >= heardAt && now - heardAt <= 25 && distance <= 60;
    }
}
