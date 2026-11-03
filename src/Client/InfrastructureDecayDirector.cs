using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using RaidRoulette.TestingClient.DecayEngine;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed class InfrastructureDecayDirector
    {
        private ConfigEntry<int>? nightChance;
        private ConfigEntry<int>? dayChance;
        private readonly MapLightCache lights = new();
        internal void Bind(ConfigFile config)
        {
            nightChance = config.Bind(
                "11. Infrastructure Decay",
                "Night power event chance (%)",
                60,
                new ConfigDescription(
                    "Chance per scheduled window during 20:00-06:00, including the first window.",
                    new AcceptableValueRange<int>(0, 100)
                )
            );
            dayChance = config.Bind(
                "11. Infrastructure Decay",
                "Day power event chance (%)",
                20,
                new ConfigDescription(
                    "Chance per scheduled window outside night hours, including the first window.",
                    new AcceptableValueRange<int>(0, 100)
                )
            );
        }

        private readonly OutageSchedule schedule = new();
        private GameWorld? world;
        private bool controlling;
        private bool first = true;
        private bool recovered;
        private int pattern;
        private float burstSeconds;
        private float nextDecision;
        private float nextNotice;
        private static InfrastructureDecayDirector? current;
        internal static bool Active => Plugin.RaidContext.EndsWith(": infrastructure_decay", StringComparison.Ordinal);
        internal static bool PowerOut => current?.controlling == true && current.schedule.Active && !current.recovered;

        internal void Tick()
        {
            current = this;
            var liveWorld = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (liveWorld is HideoutGameWorld)
            {
                liveWorld = null;
            }

            if (!ReferenceEquals(world, liveWorld))
            {
                Reset();
                world = liveWorld;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            bool night = TODSkyProvider.IsAvailable && (TODSkyProvider.Instance.Cycle.Hour >= 20f || TODSkyProvider.Instance
                .Cycle.Hour < 6f);
            if (!Active || world == null || game?.Status != GameStatus.Started || game.GameTimer?.StartDateTime == null)
            {
                if (controlling)
                {
                    Reset();
                }

                return;
            }

            controlling = true;
            lights.Discover();
            if (Time.unscaledTime < nextDecision)
            {
                return;
            }

            nextDecision = Time.unscaledTime + 0.1f;
            double now = game.GameTimer.PastTime.TotalSeconds;
            if (!schedule.Tick(
                now,
                Random.Range(180f, 300f),
                Random.Range(180f, 360f),
                Random.Range(30f, 120f),
                (night ? nightChance?.Value ?? 60 : dayChance?.Value ?? 20) / 100d,
                Random.value
            ))
            {
                return;
            }

            if (!schedule.Active)
            {
                lights.Restore();
                Plugin.Trace("[RR-DECAY] power restored; native controls available again");
                return;
            }

            recovered = !first && Random.value < 0.35f;
            first = false;
            pattern = Random.Range(0, recovered ? PowerFlickerPatterns.RecoveredCount : PowerFlickerPatterns.OutageCount);
            burstSeconds = recovered ? Random.Range(0.8f, 1.5f) : Random.Range(0.65f, 1.3f);
            if (recovered)
            {
                schedule.Start(now, burstSeconds);
            }

            Plugin.Trace($"[RR-DECAY] recovered={recovered} pattern={pattern + 1} "
                + $"seconds={schedule.Ends - now:0.0} outputs={lights.OutputCount}");
        }

        internal void LateTick()
        {
            if (!controlling || !schedule.Active || !Singleton<AbstractGame>.Instantiated)
            {
                return;
            }

            double elapsed = Singleton<AbstractGame>.Instance.GameTimer.PastTime.TotalSeconds - schedule.Started;
            lights.Drive(PowerFlickerPatterns.Gain(pattern, recovered, elapsed / burstSeconds));
        }

        internal static void ExplainBlockedControl()
        {
            if (current == null || Time.unscaledTime < current.nextNotice)
            {
                return;
            }

            current.nextNotice = Time.unscaledTime + 3f;
            EFT.UI.ConsoleScreen.Log("Power is out. Try the control after power returns.");
        }

        internal void Reset()
        {
            // Dispose restores owned map outputs and releases the per-raid cache.
            // No native switch state is changed; rejecting interaction eligibility
            // during the outage leaves every control usable when power returns.
            controlling = false;
            lights.Dispose();
            schedule.Reset();
            first = true;
            recovered = false;
            nextDecision = 0f;
        }
    }
}
