# Five Trials variants — test build

By killdg, with ChatGPT assistance. Client 2.0.90 / server 2.0.0-preview.47.

These are live-test candidates. Automated compilation, native database rules and
client signature checks do not demonstrate successful gameplay.

All five modes clear the generated raid copy's normal and boss schedules after
location generation. Client-managed requests are the only boss-scenario waves
allowed during these modes. Map/seasonal role restrictions are bypassed only
while an exclusive gauntlet is active. Event actors do not target or damage one
another; the player's attacks and environmental damage remain native.

No main installation files, profiles, quest IDs or saved settings were edited.
The update is packaged, not deployed. Native bot-generation requests use SPT's
existing local API. These features add no analytics or external connections.

## Snowball

The roster follows the installed SPT 4.1.6 Snowball achievement
`6529097eccf6aa5f8737b3d0`: Reshala, Shturman, Glukhar, Killa, Sanitar, Tagilla,
Kaban, Kollontay, Partisan, Knight, Big Pipe, Birdeye and Zryachiy.
The achievement accepts followerTagilla as an alternative to Tagilla; the event
spawns one standard Tagilla, not an additional alternative copy. Guards are not
requested. Each actor independently pursues the local player.

After the player kills all thirteen, place the Killa helmet and Tagilla mask
looted from those specific bosses edge-to-edge in the same inventory grid.
Diagonals, separate containers and helmets brought into the raid do not count.
The items are retained. This once-only easter egg requests Labyrinth Killa and
Labyrinth Tagilla at native reachable spawn points 70–130 metres away, preferring
100 metres. If no suitable point exists, it waits instead of spawning through a
wall or outside the map. Killing both completes the bonus encounter.

## Normal Trials

Five actors per wave. Every confirmed member must be dead and no creation request
may remain pending before progression. Missing actors do not count as dead.

Normal Trials now has ten waves:

1. Scavs
2. Stronger Scavs wearing Killa helmets
3. Sniper Scavs
4. Mixed USEC and BEAR PMCs
5. Raiders
6. Rogues
7. Mixed Reshala, Shturman and Sanitar guards
8. Mixed Glukhar, Kaban and Kollontay guards
9. Zryachiy guards
10. Cultist warriors

Guard Trial has eight waves of five: Reshala, Shturman, Sanitar, mixed Glukhar
scout/assault/security, mixed Kaban standard/close 1/close 2, mixed Kollontay
assault/security, Zryachiy, then five independent random picks from those guard
roles. Repeated roles are allowed in the finale. Only the eighth wave receives
the guaranteed single bitcoin.

Total Trial retains the original twenty waves: Scavs, stronger Scavs, snipers,
USEC, BEAR, Raiders, Rogues, those twelve guard roles, and Cultist warriors.
Each stage has five actors. Mixed stages distribute their member roles across
the listed pool. Levels progress from 10 to 48 over each mode's complete roster.

Equipment increases in five tiers over each complete roster: armor targets class
2/3/4/5/6, progressively stronger compatible primary-weapon families, and
per-caliber ammunition selected up to penetration ceilings 20/30/40/50, followed
by the best compatible rounds. Native compatibility constraints remain active;
levels alone are not presented as an AI-difficulty guarantee. AI presets advance
from normal to hard to impossible. Stronger Scavs are the intentional early
helmet exception. Named bosses, peaceful NPCs, vehicle crew and duplicate event
skins are not part of normal Trials.

Only the final wave has a guaranteed event bitcoin: at least one on one actor.
Earlier waves receive no forced bitcoin reward. Ordinary native random loot is
not removed. The loot belongs to the corpse and must be recovered and extracted.

## Extreme Trials

Five identical bosses per wave, no escorts:

1. Reshala
2. Sanitar
3. Kaban
4. Kollontay
5. Glukhar
6. Tagilla
7. Shturman
8. Killa
9. Labyrinth Tagilla
10. Labyrinth Killa

One actor in each wave is selected to carry at least ten bitcoins. The native
inventory placement path preserves existing gear and loot. If the carrier lacks
a backpack and has insufficient space, an Attack 2 is attached as a fallback;
it is not a player requirement. Failed placement is logged and retried, and
requires live verification alongside actual corpse looting.

## F1 menu

- Enable Raid Roulette: master switch for standalone events.
- Mostly Normal: default for fresh configurations, 90% normal / 10% events.
- Half And Half: 50% normal / 50% events.
- Custom: individual relative weights, zero to exclude. Unsupported map shares
  return to normal raids. Existing allocations migrate rather than resetting.
- Start/end raid notices remain normal user options.
- Advanced contains manual one-shot selection, Gun Game respawn, roster, monitor,
  Ghost Review, developer movement, diagnostics and event-specific test controls.

The three new modes default to zero automatic weight until live testing.
To test: open F1, enable Advanced, choose an event and queue it for one raid.

## Verification and outstanding live checks

Automated checks cover schedule suppression across twelve maps, native role
availability, gear-pool compatibility constraints, five-member wave completion,
helmet adjacency, reward thresholds, native method signatures, cleanup and menu
policy. Native Harmony smoke execution is explicitly skipped by the server
harness. No claim of live patch activation or complete raids is made.

Test spawn completion, repeated same-boss creation, pursuit by every archetype,
corpse loot rewards, friendly-fire suppression, missing-callback diagnostics,
wave progression, extraction, the helmet bonus and menu visibility in-game.
Accepted native creation requests remain pending if their callback never arrives;
the director logs that state instead of creating uncontrolled duplicates.
Placement uses at most three path queries per submission interval. Pursuit plans
at most one actor's route per quarter-second tick. No whole-scene scans are used.
