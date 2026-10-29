using System;
namespace RaidRoulette.TestingClient;
public static class GiftRoamPolicy
{
    // Recent destinations include both arrivals and routes abandoned for no progress.
    public static float Score(float distance,float nearestRecentDistance)
        =>distance<15?float.NegativeInfinity:distance+2*Math.Min(nearestRecentDistance,200);
    public static bool NeedsNewDestination(bool hasDestination,float distance,float secondsWithoutProgress)
        =>!hasDestination || distance<=3 || secondsWithoutProgress>=20;
}
