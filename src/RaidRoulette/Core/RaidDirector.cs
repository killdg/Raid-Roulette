using RaidRoulette.Configuration;
using RaidRoulette.Models;

namespace RaidRoulette.Core;

[Obsolete("Use EventManager instead.")]
public sealed class RaidDirector
{
    public RaidEvent SelectEvent(int eventChance)
    {
        var config = new RaidRouletteConfig
        {
            EventChance = eventChance
        };

        return new EventManager(config).SelectEvent().Event;
    }
}
