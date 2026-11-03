using System;

namespace RaidRoulette.TestingClient
{
    public sealed class StillnessScare
    {
        private bool initialized;
        private bool decided;
        private double x;
        private double y;
        private double z;
        private double since;
        private double cooldown;
        public void Reset()
        {
            initialized = decided = false;
            cooldown = 0;
        }

        public bool Tick(double now, double px, double py, double pz, bool eligible, double roll)
        {
            double dx = px - x;
            double dy = py - y;
            double dz = pz - z;
            if (!initialized || dx * dx + dy * dy + dz * dz >= 0.0625)
            {
                initialized = true;
                decided = false;
                since = now;
                x = px;
                y = py;
                z = pz;
                return false;
            }

            if (decided || now - since < 20 || now < cooldown || !eligible)
            {
                return false;
            }

            decided = true;
            if (roll >= 0.15)
            {
                return false;
            }

            cooldown = now + 240;
            return true;
        }
    }
}
