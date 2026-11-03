using System;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class TimeWarpDirector
    {
        private GameWorld? world;
        private GameDateTime? clock;
        private TOD_Time? skyTime;
        private DateTime originalDate;
        private DateTime raidStartDate;
        private float originalFactor;
        private float originalMod;
        private float originalRealtime;
        private float nextTick;
        private float nextLog;
        private bool originalLock;
        private double duration;
        private float nextError;
        internal static double QuestGameSeconds { get; private set; }
        public string Status { get; private set; } = "";

        public void Tick()
        {
            try
            {
                TickCore();
            }
            catch (Exception e)
            {
                try
                {
                    Stop();
                }
                catch
                { /* Original rates are restored before rebasing; keep plugin updates alive. */
                }

                if (Time.unscaledTime >= nextError)
                {
                    nextError = Time.unscaledTime + 30;
                    Plugin.Trace("[RR-TIME] stage=error " + e.GetBaseException().Message);
                }
            }
        }

        private void TickCore()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                Stop();
                world = current;
                nextTick = nextLog = 0;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (current == null || game?.GameTimer?.StartDateTime == null || !Plugin.RaidContext.EndsWith(": time_warp",
                 StringComparison.Ordinal) || game.Status is GameStatus.Stopped or GameStatus.Stopping or GameStatus.SoftStopping)
            {
                Stop();
                return;
            }

            if (Time.unscaledTime < nextTick)
            {
                return;
            }

            nextTick = Time.unscaledTime + 0.5f;
            if (!TODSkyProvider.IsAvailable || TODSkyProvider.Instance?.CurrentTime?.GameDateTime == null)
            {
                return;
            }

            var tod = TODSkyProvider.Instance.CurrentTime;
            double elapsed = game.GameTimer.PastTime.TotalSeconds;
            if (!ReferenceEquals(clock, tod.GameDateTime))
            {
                Stop();
                duration = game.GameTimer.SessionTime?.TotalSeconds ?? 0;
                double rate = TimeWarpPolicy.Rate(duration);
                if (rate <= 0 || rate > float.MaxValue)
                {
                    return;
                }

                clock = tod.GameDateTime;
                skyTime = tod;
                originalFactor = clock.TimeFactor;
                originalMod = clock.TimeFactorMod;
                originalLock = tod.LockCurrentTime;
                originalDate = clock.Calculate();
                originalRealtime = Time.realtimeSinceStartup;
                raidStartDate = originalDate.AddSeconds(-elapsed * originalFactor * originalMod);
                Plugin.Trace($"[RR-TIME] stage=started duration={duration:0.0}s cycles={TimeWarpPolicy.Cycles} rate=" +
                    $"{rate:0.000} baselineRate={originalFactor * originalMod:0.000}; raid countdown unchanged");
            }

            clock.TimeFactor = (float)TimeWarpPolicy.Rate(duration);
            clock.TimeFactorMod = 1;
            skyTime!.LockCurrentTime = false;
            clock.ResetForce(raidStartDate.AddSeconds(TimeWarpPolicy.GameSeconds(elapsed, duration)));
            QuestGameSeconds = TimeWarpPolicy.GameSeconds(elapsed, duration);
            Status = $"\nTime Warp: {Math.Min(TimeWarpPolicy.Cycles, Math.Max(0, elapsed / duration * TimeWarpPolicy.Cycles)):0.00}" +
                $"/{TimeWarpPolicy.Cycles} cycles, world clock {clock.Calculate():HH:mm:ss}, rate={clock.TimeFactor:0.0}" +
                $"x real time";
            if (Time.unscaledTime >= nextLog)
            {
                nextLog = Time.unscaledTime + 30;
                Plugin.Trace("[RR-TIME] stage=progress elapsed=" + elapsed.ToString("0.0") + Status);
            }
        }

        public void Stop()
        {
            QuestGameSeconds = 0;
            if (clock != null)
            {
                var old = clock;
                clock = null;
                try
                {
                    old.TimeFactor = originalFactor;
                    old.TimeFactorMod = originalMod;
                    old.ResetForce(originalDate.AddSeconds((Time.realtimeSinceStartup - originalRealtime) * originalFactor * originalMod));
                    Plugin.Trace("[RR-TIME] stage=restored native clock rate and original timeline");
                }
                finally
                {
                    if (skyTime != null)
                    {
                        skyTime.LockCurrentTime = originalLock;
                    }

                    skyTime = null;
                    Status = "";
                }
            }
        }
    }
}
