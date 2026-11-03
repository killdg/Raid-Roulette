using System.Runtime.CompilerServices;
using RaidRoulette.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace RaidRoulette.Services
{
    public static class TrialsRaidDuration
    {
        private sealed class Original(LocationBase map)
        {
            internal readonly double? Standard = map.EscapeTimeLimit;
            internal readonly int? Pve = map.EscapeTimeLimitPVE;
            internal readonly int? Coop = map.EscapeTimeLimitCoop;
        }

        private static readonly ConditionalWeakTable<LocationBase, Original> originals = new();
        public static void Apply(LocationBase map, string eventId)
        {
            if (!GauntletPolicy.Handles(eventId))
            {
                return;
            }

            // Called only on the generated raid copy. SPT exposes durations in
            // minutes for standard/PvE/co-op consumers; do not edit the database
            // or change the speed of the clock, spawning or animations.
            // Remember originals weakly so a repeated hook cannot quadruple time,
            // and the entry disappears naturally when that raid copy is released.
            Original original = originals.GetValue(map, value => new Original(value));
            map.EscapeTimeLimit = original.Standard * 2;
            map.EscapeTimeLimitPVE = original.Pve * 2;
            map.EscapeTimeLimitCoop = original.Coop * 2;
        }
    }
}
