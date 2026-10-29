using System;
namespace RaidRoulette.TestingClient;
public readonly struct MonitorBounds
{
    public readonly float X,Y,Width,Height;
    public MonitorBounds(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
}
public static class MonitorLayout
{
    public static MonitorBounds Clamp(float x,float y,float width,float height,float screenWidth,float screenHeight)
    {
        screenWidth=Math.Max(1,screenWidth);screenHeight=Math.Max(1,screenHeight);
        width=Math.Min(screenWidth,Math.Max(Math.Min(320,screenWidth),width));
        height=Math.Min(screenHeight,Math.Max(Math.Min(180,screenHeight),height));
        return new MonitorBounds(Math.Max(0,Math.Min(screenWidth-width,x)),Math.Max(0,Math.Min(screenHeight-height,y)),width,height);
    }
}
