using RaidRoulette.Models;

namespace RaidRoulette.Events
{
    public abstract class ServerRuleEvent(string id, string name, string description) : RaidEvent(id, name, description, 15)
    {
        public override void Apply(RaidContext context)
        {
            context.Report.AddDetail(Description);
            context.Report.MarkApplied("Server rules will be applied to the generated location/bots.");
        }
    }

    public sealed class ScavConcentrateEvent() : ServerRuleEvent(
        "scav_concentrate",
        "Scav Concentrate",
        "Replace native combat population with one Scav group that gathers around a roaming leader."
    );
    public sealed class PmcSweepEvent() : ServerRuleEvent(
        "pmc_sweep",
        "PMC Sweep",
        "Replace scheduled AI waves with additional USEC/BEAR groups; exclude Scavs and bosses."
    );
    public sealed class ExplosiveScavUnitsEvent() : ServerRuleEvent(
        "explosive_scav_units",
        "Explosive Scav Units",
        "Scavs carry a handgun and a native grenade launcher; player kit stays unchanged."
    );
    public sealed class OutbreakEvent() : ServerRuleEvent("outbreak", "Outbreak",
         "Zombie starting population with replenishment while the raid continues.");
    public sealed class GearedEvent() : ServerRuleEvent(
        "geared",
        "GEARED",
        "Curated high-tier rifles, class-6-capable body armor, top available helmet class, and highest-penetration compatible ammo."
    );
    public sealed class GetHandyEvent() : ServerRuleEvent(
        "get_handy",
        "Get Handy",
        "Ordinary AI PMCs and Scavs spawn with handguns only, no secondary guns; carried grenades "
            + "are flashbangs only. Cached profiles are included."
    );
    public sealed class IHaveAShotEvent() : ServerRuleEvent(
        "i_have_a_shot",
        "I Have a Shot",
        "Ordinary AI PMCs and Scavs spawn with shotguns only, no secondary guns; carried grenades "
            + "are flashbangs only. Cached profiles are included."
    );
    public sealed class MarksmanEvent() : ServerRuleEvent(
        "marksman",
        "Marksman",
        "Ordinary AI PMCs and Scavs spawn with sniper rifles only, no secondary guns; carried "
            + "grenades are flashbangs only. Cached profiles are included."
    );
    public sealed class OverloadedEvent() : ServerRuleEvent(
        "overloaded",
        "Overloaded",
        "Generated Scavs and AI PMCs receive only compatible 100\u002B round primary weapons and the "
            + "largest real-capacity pistol magazine (84-round PM/PB in the standard database). Cached "
            + "profiles are included; infected magazines excluded."
    );
    public sealed class MatrixGlitchEvent() : ServerRuleEvent(
        "matrix_glitch",
        "Matrix Glitch",
        "Shared clothing per faction or raid-wide; weapon inventories remain independent."
    );
    public sealed class NoSafeSpaceEvent() : ServerRuleEvent("no_safe_space", "No Safe Space",
         "All mobile combat AI track and pursue the player, including bosses.");
    public sealed class PosersEverywhereEvent() : ServerRuleEvent(
        "posers_everywhere",
        "Posers Everywhere",
        "Ordinary AI use the installed Killa equipment template and clothing; they do not become real Killa bosses."
    );
    public sealed class FakeCultEvent() : ServerRuleEvent(
        "fake_cult",
        "Fake Cult",
        "Ordinary AI use installed Cultist clothing, retaining their own role and weapons."
    );
    public sealed class OverrunEvent() : ServerRuleEvent(
        "overrun",
        "Overrun",
        "Replace casualties with native Scav/PMC reinforcement squads when population falls below half its map target."
    );
    public sealed class CloneWarsEvent() : ServerRuleEvent(
        "clone_wars",
        "Clone Wars",
        "Ordinary AI PMCs copy your raid-start appearance, name, level, skills and carried kit. "
            + "Lootable, with independent IDs; native faction/AI remain unchanged."
    );
    public sealed class SmokersLobbyEvent() : ServerRuleEvent(
        "smokers_lobby",
        "Smokers Lobby",
        "Generated AI, including PMCs, bosses and guards, carry 5\u201310 M18 smoke grenades and eagerly "
            + "throw them using the local client. Human loadouts stay unchanged."
    );
    public sealed class TimeWarpEvent() : ServerRuleEvent(
        "time_warp",
        "Time Warp",
        "Six full day/night cycles over the actual raid duration on outdoor maps, using the local "
            + "client world clock. Raid countdown, movement and combat speed remain unchanged."
    );
    public sealed class TheGiftEvent() : ServerRuleEvent(
        "the_gift",
        "The Thief",
        "Neutral Blicky prize runner with Santa hat, 200\u2013400k roubles-equivalent in currencies, 0\u20133 "
            + "bitcoins and a 30-second paper trail; requires the local testing client."
    );
    public sealed class SchizophreniaEvent() : ServerRuleEvent(
        "schizophrenia",
        "Schizophrenia",
        "Local hallucinations: random unlocked doors, phantom Scav voices, weapon sounds and "
            + "surface impacts, tripwire pin/fuze sounds, visual-only grenades, fleeting rendered AI "
            + "decoys and a once-per-raid twelve-hour clock jump lasting sixty seconds. No live "
            + "projectiles, explosives or extra AI. Requires the matching local client."
    );
    public sealed class GunGameEvent() : ServerRuleEvent(
        "gun_game",
        "Gun Game",
        "Factory, Labs and Ground Zero only. "
            + "Pistol, Shotgun, SMG, Sniper, AK50, Assault Rifle, LMG, Rocket Launcher, Grenade Launcher. "
            + "Compatible randomized attachments. Each scored kill advances that actor. Completing all "
            + "nine earns one Bitcoin; a knife kill in that successful attempt adds two. Rewards are "
            + "limited to one set per raid. Original player weapons are escrowed for trader-mail return."
    );
    public sealed class StormtideEvent() : ServerRuleEvent(
        "stormtide",
        "Stormtide",
        "Outdoor maps reach darkness and heavy rain five minutes after raid start; night raids "
            + "remain dark from the start. Native rain volume ramps to double while other sounds fall to "
            + "25%. Original weather, clock and sound settings restore afterwards."
    );
    public sealed class Ak50Event() : ServerRuleEvent(
        "ak50",
        "AK50",
        "AI combatants and the PMC player use the native AK-50; vehicle crew are excluded. Original "
            + "player weapons are escrowed for trader-mail return."
    );
    public sealed class GrenadierEvent() : ServerRuleEvent(
        "grenadier",
        "Grenadier",
        "Ordinary AI PMCs and Scavs carry 10-15 F-1 grenades and frequently attempt native checked "
            + "throws. Player loadout stays unchanged."
    );
    public sealed class LabWorkDayEvent() : ServerRuleEvent(
        "lab_work_day",
        "Lab Work Day",
        "Labs with roaming unarmed human staff and open keyed rooms. Killing three staff calls four "
            + "hostile Raiders. Uses native Scav appearances because Terminal civilian assets are absent " + "in this client."
    );
    public sealed class BlueDayEvent() : ServerRuleEvent(
        "blue_day",
        "Blue Day",
        "Native mortar zones bombard in eight randomized raid-time windows, day or " + "night. Supported outdoor maps only."
    );
}
