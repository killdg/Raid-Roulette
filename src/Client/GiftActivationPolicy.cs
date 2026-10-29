namespace RaidRoulette.TestingClient;
public static class GiftActivationPolicy
{
    public static bool Allow(bool confirmedRunner,bool preActive,bool alive,bool handsReady)
        =>confirmedRunner && preActive && alive && handsReady;
}
