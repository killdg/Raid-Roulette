# Event catalog and testing status

Status reflects the author’s raid feedback, not a guarantee for other installations. Follow-up fixes may still need another live test. Default weights and enable switches are in `src/RaidRoulette/Configuration/V2Settings.cs` and the example config. A zero weight prevents random selection; F12 can queue supported events once. Validated F12 percentages take precedence over legacy automatic weights; a positive share is an explicit opt-in, still subject to map/assets.

| Event | Behavior | Status |
|---|---|---|
| AK50 (`ak50`) | AI combatants and the PMC player use the native AK-50; vehicle crew are excluded. Original player weapons are escrowed for trader-mail return. | Needs live verification |
| Boss Convention (`boss_convention`) | Multiple boss groups have entered the raid. | Needs live verification |
| Boss Hunt (`boss_hunt`) | One random mobile boss receives a 100% starting spawn request and relentlessly hunts the local player until either dies; requires the local client. | Player-tested; latest fixes may need retesting |
| Clone Wars (`clone_wars`) | Ordinary AI PMCs copy your raid-start appearance, name, level, skills and carried kit. Lootable, with independent IDs; native faction/AI remain unchanged. | Player-tested; latest fixes may need retesting |
| Cultist Invasion (`cultist_invasion`) | Cultist groups are active in this raid. | Player-tested; latest fixes may need retesting |
| Explosive Scav Units (`explosive_scav_units`) | Configured percentage of ordinary Scavs receive standalone grenade launchers, not rocket launchers. | Needs live verification |
| Extreme Overwatch (`extreme_overwatch`) | After normal starting AI load, add two sniper Scavs per native rooftop sniper point, using a separate finite extra-spawn budget. Normal waves remain unchanged. | Unresolved: extra rooftop sniper spawning needs verification |
| Fake Cult (`fake_cult`) | Ordinary AI use installed Cultist clothing, retaining their own role and weapons. | Needs live verification |
| GEARED (`geared`) | Curated high-tier rifles, class-6-capable body armor, top available helmet class, and highest-penetration compatible ammo. | Needs live verification |
| Get Handy (`get_handy`) | Ordinary AI PMCs and Scavs spawn with handguns only, no secondary guns; carried grenades are flashbangs only. Cached profiles are included. | Needs live verification |
| Grenadier (`grenadier`) | Ordinary AI PMCs and Scavs carry 10-15 F-1 grenades and frequently attempt native checked throws. Player loadout stays unchanged. | Needs live verification |
| Gun Game (`gun_game`) | Pistol, Shotgun, SMG, Sniper, AK50, Assault Rifle, LMG, Rocket Launcher, Grenade Launcher. Compatible randomized attachments. Each scored kill advances that actor. Completing all nine earns one Bitcoin; a knife kill in that successful attempt adds two. Rewards are limited to one set per raid. Original player weapons are escrowed for trader-mail return. | Player-tested; latest fixes may need retesting |
| High-Value Loot (`high_value_loot`) | More high-value loot opportunities are active. | Needs live verification |
| I Have a Shot (`i_have_a_shot`) | Ordinary AI PMCs and Scavs spawn with shotguns only, no secondary guns; carried grenades are flashbangs only. Cached profiles are included. | Needs live verification |
| Lab Work Day (`lab_work_day`) | Labs staff in medical clothing and Security vests; empty hands, flee from shots/hits, open keyed rooms, four Raiders after three staff kills, adjustable 1,000 XP penalty per staff kill with zero final-XP floor. | Initial version tested; latest staff changes awaiting live test |
| Loot Surge (`loot_surge`) | Loose-loot generation has increased. | Needs live verification |
| Marksman (`marksman`) | Ordinary AI PMCs and Scavs spawn with sniper rifles only, no secondary guns; carried grenades are flashbangs only. Cached profiles are included. | Needs live verification |
| Matrix Glitch (`matrix_glitch`) | Shared clothing per faction or raid-wide; weapon inventories remain independent. | Needs live verification |
| No Safe Space (`no_safe_space`) | Multiply scheduled AI counts, bounded by configured alive and total scheduled limits. | Needs live verification |
| Normal Raid (`normal`) | No special event is active. | Baseline |
| Outbreak (`outbreak`) | Replace scheduled waves with infectedAssault; requires installed zombie bot templates and client assets. | Needs live verification |
| Overloaded (`overloaded`) | Generated Scavs and AI PMCs receive only compatible 100+ round primary weapons and the largest real-capacity pistol magazine (84-round PM/PB in the standard database). Cached profiles are included; infected magazines excluded. | Needs live verification |
| Overrun (`overrun`) | Normal starting spawns plus client-directed reinforcement waves; requires the local testing client 2.0.7. | Needs live verification |
| PMC Sweep (`pmc_sweep`) | Replace scheduled AI waves with additional USEC/BEAR groups; exclude Scavs and bosses. | Needs live verification |
| Posers Everywhere (`posers_everywhere`) | Ordinary AI use the installed Killa equipment template and clothing; they do not become real Killa bosses. | Player-tested; latest fixes may need retesting |
| Raider Assault (`raider_assault`) | A Raider invasion is spread across several map zones. | Needs live verification |
| Scav Concentrate (`scav_concentrate`) | Concentrate scheduled Scav spawns in one valid map AI zone; no building/pathing guarantee. | Needs live verification |
| Schizophrenia (`schizophrenia`) | Local hallucinations: random unlocked doors, phantom Scav voices, weapon sounds and surface impacts, tripwire pin/fuze sounds, visual-only grenades, fleeting rendered AI decoys and a once-per-raid twelve-hour clock jump lasting sixty seconds. No live projectiles, explosives or extra AI. Requires the matching local client. | Player-tested; latest fixes may need retesting |
| Smokers Lobby (`smokers_lobby`) | Generated AI, including PMCs, bosses and guards, carry 5–10 M18 smoke grenades and eagerly throw them using the local client. Human loadouts stay unchanged. | Player-tested; latest fixes may need retesting |
| Stormtide (`stormtide`) | Outdoor darkness and heavy rain by five minutes; custom rain crossfade, lower other sounds, timed recorded thunder and simulated lightning. | Player-tested; latest fixes may need retesting |
| The Gift (`the_gift`) | Neutral Blicky prize runner with Santa hat, 200–400k roubles-equivalent in currencies, 0–3 bitcoins and a 30-second paper trail; requires the local testing client. | Player-tested; latest fixes may need retesting |
| Time Warp (`time_warp`) | Six full day/night cycles over the actual raid duration on outdoor maps, using the local client world clock. Raid countdown, movement and combat speed remain unchanged. | Player-tested; latest fixes may need retesting |

Total: 32 definitions including Normal.

## Lab staff role detail

Staff use the native neutral `gifter` spawn role for compatibility with the game’s spawn/cache machinery. Our roster labels them Civilian; external overlays may still call them bosses. The new hooks suppress normal combat kill/damage XP and boss/combat kill credit. The native casualty list uses a Scav role. The configurable staff penalty is applied after bonuses, before the native profile XP increment, and cannot reduce pre-raid XP.

## Optional Task Edition campaign

Stormtide → Smokers Lobby → The Gift → Time Warp → Schizophrenia → Clone Wars → AK50 → Boss Hunt → Cultist Invasion → Gun Game.

Stormtide unlocks at level 10. Later quests require the previous quest completed and turned in. Stable quest IDs preserve existing accepted quests and progress; later accepted tasks wait their turn. Do not queue a manual raid override when testing automatic quest-driven selection.

The task definitions, trader dialogue and rewards are maintained in the separate [Task Edition repository](https://github.com/killdg/Raid-Roulette-Task-Edition).
