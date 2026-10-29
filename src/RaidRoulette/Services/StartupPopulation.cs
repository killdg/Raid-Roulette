namespace RaidRoulette.Services;
public static class StartupPopulation
{
    public static int Target(int cap,int percent)
        =>cap<=0?0:(int)Math.Clamp(((long)cap*Math.Clamp(percent,50,100)+99)/100,1,cap);
}
