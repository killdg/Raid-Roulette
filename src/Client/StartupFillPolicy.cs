namespace RaidRoulette.TestingClient;
public static class StartupFillPolicy
{
    public static bool CanSubmit(int alive,int pending,int submitted,int target,int cap,double elapsed)
        =>elapsed>=5&&elapsed<=90&&alive>=0&&pending>=0&&submitted>=0&&target>0&&cap>0&&submitted<target&&(long)alive+pending<System.Math.Min(target,cap);
}
