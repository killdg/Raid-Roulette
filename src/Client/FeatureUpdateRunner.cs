using System;
using System.Collections.Generic;

namespace RaidRoulette.TestingClient
{
    public sealed class FeatureUpdateRunner
    {
        private sealed class Feature
        {
            internal string Name = "";
            internal Action Update = null!;
            internal double NextReport;
        }

        private readonly List<Feature> features = new();
        private readonly Action<string, Exception> report;
        public FeatureUpdateRunner(Action<string, Exception> reportError)
        {
            report = reportError;
        }

        public void Add(string name, Action update)
        {
            features.Add(new Feature { Name = name, Update = update });
        }

        public void Tick(double now)
        {
            foreach (Feature feature in features)
            {
                try
                {
                    feature.Update();
                }
                catch (Exception error)
                {
                    // A broken effect must not prevent travel controls, other
                    // directors or the final Ghost Review stamina refill.
                    if (now >= feature.NextReport)
                    {
                        feature.NextReport = now + 30;
                        report(feature.Name, error);
                    }
                }
            }
        }
    }
}
