using System;
using System.Collections.Generic;

namespace RaidRoulette.TestingClient
{
    public sealed class TraceLimiter
    {
        private readonly HashSet<string> seen = new();
        private int lines;
        public bool Allow(string key)
        {
            lock (seen)
            {
                if (lines >= 240 || !seen.Add(key))
                {
                    return false;
                }

                lines++;
                return true;
            }
        }

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
                lines = 0;
            }
        }
    }
}
