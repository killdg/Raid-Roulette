using System;

namespace RaidRoulette.TestingClient
{
    public static class LabStaffPolicy
    {
        // A cross-hub patrol must leave the local office cluster. This threshold
        // does not claim that a valid path exists; native NavMesh checks follow.
        public static bool PatrolDistance(float distance)
        {
            return !float.IsNaN(distance) && !float.IsInfinity(distance) && distance >= 24f;
        }

        public const int DefaultPenalty = 1000;
        public static int Award(int earned, int requested) => Math.Max(0, Math.Max(0, earned) - Math.Max(0, requested));
        public static bool IsStaff(string role, string name) => role == "gifter" && name == "Lab Staff";
        public static bool Panic(double now, double heardAt, double distance) => now >= heardAt && now - heardAt <= 25 && distance <= 60;
    }
}
