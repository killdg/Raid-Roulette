using System;
using System.Collections.Generic;
using System.Linq;

namespace RaidRoulette.TestingClient
{
    public static class LockedLootBackpackPolicy
    {
        public static string[] AllowCases(IEnumerable<string> original, IEnumerable<string> cases)
        {
            return original.Concat(cases).Distinct(StringComparer.Ordinal).ToArray();
        }

        public static string[] KeepOtherExclusions(IEnumerable<string> original, ISet<string> cases)
        {
            // SimpleContainer is the native case/pouch class, distinct from bags
            // and secure containers. Return a new array; never edit template data.
            return original.Where(id => id != "5795f317245977243854e041" && !cases.Contains(id)).ToArray();
        }
    }
}
