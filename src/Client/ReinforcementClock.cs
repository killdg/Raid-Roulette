using System;
namespace RaidRoulette.TestingClient;
// Simulation time, not wall-clock time. At most one wave becomes due after a stall.
public sealed class ReinforcementClock
{
    private readonly double interval;
    public double NextDeadline {get;private set;}
    public int Waves {get;private set;}
    public ReinforcementClock(double intervalSeconds)
    {
        if(double.IsNaN(intervalSeconds)||double.IsInfinity(intervalSeconds)||intervalSeconds<1)throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        interval=intervalSeconds;NextDeadline=interval;
    }
    public bool Advance(double elapsedSeconds)
    {
        if(double.IsNaN(elapsedSeconds)||double.IsInfinity(elapsedSeconds)||elapsedSeconds<NextDeadline)return false;
        Waves++;NextDeadline=(Math.Floor(elapsedSeconds/interval)+1)*interval;return true;
    }
    public static bool HasCapacity(int alive,int pending,int maximum)=>alive>=0&&pending>=0&&maximum>0&&(long)alive+pending<maximum;
}
