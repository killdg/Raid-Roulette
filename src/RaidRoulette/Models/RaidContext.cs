using RaidRoulette.Configuration;
using RaidRoulette.Services;

namespace RaidRoulette.Models
{
    public sealed class RaidContext
    {
        public required Action<string> LogWarning { get; init; }
        public required Action<string> LogDebug { get; init; }
        public required LootService LootService { get; init; }
        public required BossSpawnService BossSpawnService { get; init; }
        public required RaidRouletteConfig Config { get; init; }
        public required string LocationId { get; init; }
        public required EventReport Report { get; init; }
        public required Action<Action> RegisterRollback { get; init; }
    }
}
