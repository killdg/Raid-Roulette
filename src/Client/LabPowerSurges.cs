using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class LabPowerSurges
    {
        private readonly LightningLightFlicker lights = new();
        private bool scheduled;
        private bool outage;
        private double nextRoll;
        private double outageStarted;
        private double restoreAt;
        private float outputGain = 1;
        private int pattern;
        internal void Tick(double raidSeconds)
        {
            // Discovery walks scene transforms in bounded batches using the
            // existing light cache. It excludes players and directional lights.
            lights.Discover();
            if (!scheduled)
            {
                scheduled = true;
                nextRoll = raidSeconds + Random.Range(180f, 300f);
            }

            if (outage && raidSeconds >= restoreAt)
            {
                lights.RestoreOutputs();
                outage = false;
                nextRoll = raidSeconds + Random.Range(180f, 360f);
                Plugin.Trace("[RR-LAB-POWER] restored map light outputs");
            }

            if (!outage && raidSeconds >= nextRoll)
            {
                // The first outage is guaranteed; subsequent checks roll 33%.
                // Schedule the next check independently of the frame rate.
                bool firstOutage = restoreAt == 0;
                nextRoll = raidSeconds + Random.Range(180f, 360f);
                if (firstOutage || Random.value < 0.33f)
                {
                    outage = true;
                    pattern = Random.Range(0, DecayEngine.PowerFlickerPatterns.OutageCount);
                    outageStarted = raidSeconds;
                    restoreAt = raidSeconds + Random.Range(30f, 120f);
                    Plugin.Trace($"[RR-LAB-POWER] outage seconds={restoreAt - raidSeconds:0} outputs={lights.OutputCount}");
                }
            }

            if (!outage)
            {
                return;
            }

            double elapsed = raidSeconds - outageStarted;
            outputGain = DecayEngine.PowerFlickerPatterns.Gain(pattern, false, elapsed / 1.2);
        }

        internal void LateTick()
        {
            if (outage)
            {
                // Apply cached outputs after native light culling updates, just
                // like the tested lightning effect. No scene scan occurs here.
                lights.Drive(outputGain);
            }
        }

        internal void Reset()
        {
            // Restore only values still owned by this effect. Native switches,
            // broken lamps and other systems retain any intervening changes.
            lights.RestoreOutputs();
            lights.Dispose();
            scheduled = false;
            outage = false;
            nextRoll = 0;
            outageStarted = 0;
            restoreAt = 0;
            outputGain = 1;
        }
    }
}
