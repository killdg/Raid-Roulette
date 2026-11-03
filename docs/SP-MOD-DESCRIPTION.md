## Your next raid might have other plans.

Choose how often normal raids give way to unusual enemies, weather, loot and challenges—or visit Fence and pick exactly what you want to play next.

> **Press F1**, open **Raid Roulette**, and choose weighted random raids or **Trader Event Activation**. Settings save automatically.

{.is-info}

## About this project

Raid Roulette is an experiment in how far we can push SPT and its engine to create unusual, varied raids. These events also explore ideas and engine interactions for future **quest packs** I intend to build.

Please report anything that does not work as expected through the **Issues** tab. Include the event, map, installed versions, what happened and any other mods you use; share only relevant, redacted log excerpts.

I will investigate reported conflicts with other mods where I can, but **compatibility with every mod cannot be promised**. **SAIN and Fika** are specific compatibility goals I intend to spend time on. Full compatibility is the aim, not a claim that it has already been achieved. The current main edition remains a solo build.

## Explore Raid Roulette {.tabset}

### How to play

**Let events happen naturally**

Press **F1**, expand **Raid Roulette**, and keep **Enable Raid Roulette** on. With **Trader Event Activation off**, choose a raid preset:

| Preset | Normal raids | Event raids |
| --- | --- | --- |
| Mostly Normal (default) | 90% | 10% |
| Half And Half | 50% | 50% |
| Custom | Your chosen normal share | Your enabled event weights |

Custom separates **how often anything unusual happens** from **which events you enjoy**. Set the normal-raid chance, enable the event types you want, and give those events relative weights. Equal weights make enabled events equally likely; a larger weight makes an event more frequent within the event share. Set an event's weight to zero or disable it to leave it out. You do **not** need to make dozens of values add up to 100.

For example, Normal 60% leaves 40% for events. Giving Stormtide a weight of 3 and Clone Wars a weight of 1 makes their overall shares 30% and 10%. If Stormtide is unsupported on your selected map, its share becomes Normal: on Labs that example becomes 90% Normal and 10% Clone Wars. Unsupported shares are not redistributed to force another event.

Changes apply to the next generated raid. Existing saved preferences are preserved when updating.

**Choose the next raid through Fence**

Enable **Trader Event Activation** in F1, then visit **Fence → Tasks**. Accept an **[RR: Event Name]** assignment to choose that event for your next eligible **PMC** raid. Accepting another RR assignment replaces the previous selection; events do not stack.

The deployment map shows a gold **[RR]** badge beside locations that support **your accepted assignment**. Hover over it for the event name. No accepted RR task means no all-events marker clutter. Stormtide excludes Labs and Factory; Lab Work Day is Labs-only. All four season choices exclude Labs and include Factory, although some seasonal differences there are subtle.

An unsupported map runs Normal and leaves the selection waiting. Fence activation owns the selection while enabled; switch it off to return to weighted random raids. Ordinary assignments finish after their event raid regardless of survival. Trials require the relevant wave clear. After an attempt, assignments return as fresh, unaccepted offers; startup also clears stale RR selections left by an interrupted session. These are reusable, reward-free event selectors, separate from the story quest pack.

**Gun Game [Respawns]** is a separate Fence choice. Defeat resets the ladder and knife bonus; respawning restores health and starts again with a pistol. The raid countdown continues.

### Settings

Applies to the **2.0.146 beta**. Press **F1** and expand **Raid Roulette**.
F1 opens the installed Configuration Manager; its shortcut may be changed in
that provider. Saved preferences survive updates, so the defaults below describe
a fresh installation. Settings are saved automatically.

#### General and event selection

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Enable Raid Roulette** | On | Enables RR's random and queued event selection for the next raid. Turning it off leaves ordinary raids Normal. A separately installed task pack can still request its mission events through the shared engine. |
| **Trader Event Activation** | Off | Lets Fence's accepted **[RR: Event Name]** assignment select the next eligible PMC raid instead of the random/manual selection. Accepting a different RR assignment replaces the previous selection. With no eligible accepted assignment, the raid is Normal. Reopen Fence after changing this setting. |
| **Event** | Random | Search or scroll the manual event list. Selecting a name alone does not queue it. |
| **Queue selected raid once / Queue next raid** | No event queued | Queues the selected type for one raid; automatic chances resume afterward. Requires RR enabled and Trader Event Activation off. The queue lasts for the current server session. Unsupported maps become Normal. |
| **Selection status** | Status display | Shows acknowledgement of the current selection. It is information, not another activation switch. |

Fence assignments show **[RR]** badges only at locations that support the
accepted event. These selections do not stack multiple events. For respawning
Gun Game, choose **Gun Game [Respawns]** at Fence. That assignment controls its
own respawn behavior for that raid.

#### Raid chances

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Raid preset: Mostly Normal** | Selected | 90% Normal raids and 10% event raids. Uses its preset event pool; Trials variants are excluded from this preset's random pool. |
| **Raid preset: Half And Half** | Not selected | 50% Normal raids and 50% event raids, using the preset event pool. |
| **Raid preset: Custom** | Not selected | Uses your Normal chance and the enabled events' relative weights. Select this preset to apply the custom mix. |
| **Chance status** | Status display | Shows the current balance and explains how unsupported map shares are handled. |
| **Normal raid chance (%)** | 90% on a fresh installation | In Custom mode, sets the chance of a Normal raid independently of event weights. Existing installations can retain a migrated value. |
| **Custom event mix** | Saved event preferences | Enable or disable individual events and change their relative weights in the scrolling list. Zero weight excludes an event. Values do not need to add up to 100. |
| **Enable all** | Action | Enables every event in the custom mix, giving zero-weight entries a positive weight. |
| **Disable all** | Action | Excludes all events from the custom mix. With no enabled positive-weight event, selection becomes Normal. |
| **Equal weights** | Action | Sets event weights equally; it does not turn disabled events on. |

The custom mix is used only with **Custom** selected. For example, 60% Normal
leaves 40% for events. Stormtide weight 3 and Clone Wars weight 1 split that
40% into 30% Stormtide and 10% Clone Wars. Unsupported event shares become
Normal rather than being redistributed: on Labs, that example is 90% Normal
and 10% Clone Wars. Chance changes apply to the next generated raid.

#### Raid event notices — the bottom-corner message

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Show raid event before raid** | **Off** | Reveals the event once near the start of the raid, in the native notification area: for example, **Raid Event : Stormtide**. Despite the label, it appears when the raid begins, rather than on the deployment selection screen. |
| **Show raid event after raid** | **On** | Reveals the actual event on the post-raid screen. Includes **Raid Event : Normal** for an ordinary raid. |

These switches are independent. Turn both off to hide both event-name notices;
leave only the after-raid switch on to discover the event while playing and
receive confirmation afterward. Neither switch enables or disables the event
itself. Event-specific messages can still appear during play.

The before-raid preference is captured when the raid world loads. Set it before
loading your next raid; changing it halfway through a raid does not create a
new start notice. The optional language pack translates the event-name notice.

#### Stormtide

These controls tune **Stormtide when that event is selected**. They do not turn
normal rain into Stormtide or change the frequency of other RR events.

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Night hurricane effects** | On | Allows cosmetic wind, outdoor eye/goggle rain and night-only outdoor debris when the hurricane roll succeeds. |
| **Night hurricane chance (%)** | 30% | One roll per Stormtide raid at its first nighttime moment, between 20:00 and 06:00. Hurricane effects are not enabled during daylight. Outdoor debris may remain visible through windows from inside. |
| **Night lightning flicker** | On | Lets lightning briefly flicker controllable map lights at night. Some baked lights cannot be changed. |
| **Recorded heavy rain** | On | Crossfades toward the bundled heavy-rain recording through the five-minute storm buildup. Native rain remains the fallback if recordings fail to load. |
| **Outdoor rain volume (%)** | 70% | Maximum recorded-rain level while outside. |
| **Indoor rain volume (%)** | 40% | Recorded-rain level under shelter. |
| **Outdoor gameplay volume (%)** | 25% | Level for the affected gameplay sounds outside during the storm. |
| **Indoor gameplay volume (%)** | 60% | Level for the affected gameplay sounds under shelter. |
| **Test thunder and lightning / Play test strike** | Action | Requests a test strike after five minutes of an active Stormtide raid. It waits for bundled recordings to finish loading. |

Shelter sound transitions are smoothed. These are game audio gain settings,
not measured real-world decibel reductions.

#### Infrastructure Decay

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Night power event chance (%)** | 60% | Chance per scheduled power-event window during 20:00–06:00, including the first window. |
| **Day power event chance (%)** | 20% | Chance per scheduled window outside those night hours, including the first window. |

These settings apply when the **Infrastructure Decay** event runs. The first
window is scheduled 3–5 minutes into the raid; later checks use 3–6 minute
intervals. Outages last 30–120 seconds. Some later successful events are brief
flickers that recover without a full outage. Supported powered buttons and
switches are unavailable during a full outage and become usable when power
returns. Baked illumination and some displays can remain visible.

#### Additional controls

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Overrun: Replacement cooldown seconds** | 3 seconds | Minimum delay between population replacement requests. Overrun checks low population rather than using a fixed wave timer. |
| **Overrun: Bots per wave** | 0 (automatic) | Uses the ordinary Scavs/PMCs observed during the first 60 seconds, with a minimum of one; excludes bosses/followers. A positive value sets a fixed count. |
| **Overrun: Maximum alive AI** | 35 | Reinforcements wait for capacity. Counts all living AI; native spawn limits still apply. |
| **Lab Work Day: XP penalty per staff kill** | 1,000 XP | Deducts experience per staff member personally killed. Final raid XP cannot fall below zero. Captured at raid start. |
| **Respawn after lethal damage** | Off | Legacy solo Gun Game preference for non-Fence Gun Game selection. A Fence Gun Game assignment overrides it for that raid; use Fence's **Gun Game [Respawns]** for the simplest choice. Respawn resets the ladder and knife bonus, restores health and equips a pistol. The raid timer continues. |

Overrun population settings are captured at raid start. These values only affect
the event that uses them, not every ordinary raid.

#### Developer tools

**Show advanced options** defaults to off and requires the optional **DG Testing
Tools** plugin. It exposes development controls such as the live AI monitor,
hunter tracking, Ghost Review, quick travel, spawn logging, assisted wave
progression and the F2 scare panel. Ordinary players do not need that plugin.
Developer controls can change a test's outcome; they are not required to activate
Fence events or use random raids.

#### Saved configuration

RR settings are stored in
`BepInEx/config/com.killdg.raidroulette.testing.cfg`. If editing this file by
hand, close the game first. Updating RR preserves saved preferences; new default
values do not automatically replace choices already saved in that file.

### Events

Choose the theme you enjoy. These descriptions explain the main changes; easter-egg solutions remain a surprise.

| Event | What happens |
| --- | --- |
| **Boss Run Down** | One randomly selected boss is assigned to hunt you. The hunter continually updates its route toward your position, so waiting in one area can bring the fight to you. Native navigation still matters: this is a pursuing enemy, not an opponent guaranteed to reach every player-accessible ledge. |
| **Boss Convention** | Multiple bosses and their company share one location. Expect several dangerous encounters instead of the map’s usual boss possibilities. Plan ammunition, routes and an escape before picking a fight with a gathering. |
| **Raider Assault** | Raiders replace the usual opposition. Their equipment and combat behavior make familiar routes more dangerous. Your own starting loadout stays yours, so bring something suited to a tougher raid. |
| **Cultist Invasion** | Cultists replace ordinary encounters and bring their characteristic equipment and close-range threat. Do not assume a quiet corridor is empty. Keep enough distance to respond when an encounter turns into a knife rush. |
| **Guarded** | The raid draws its opposition from boss-guard roles. This gives a mixture of guard equipment and behavior without requiring every encounter to be a full boss group. Expect stronger resistance than a routine Scav run. |
| **PMC Sweep** | USEC and BEAR groups take the place of the usual opposition. Expect armed contractors throughout the raid rather than the normal mix of Scavs and bosses. Bring enough ammunition to deal with multiple equipped groups. |
| **Scav Concentrate** | Scavs gather into groups rather than spreading evenly across the map. A quiet approach can suddenly become a crowded fight when you find one of those groups. They do not all have to form one perfectly coordinated map-wide squad. |
| **No Safe Space** | Combatants across the map are directed toward you, including bosses where present. The pressure comes from pursuit rather than a special weapon restriction. Keep an exit route and avoid assuming that a cleared corner will remain quiet. |
| **Get Handy** | Scavs and PMCs use handguns instead of their usual long guns. Their carried grenades are flashbangs, keeping the theme focused on pistol fights. Your own brought-in equipment is not automatically replaced by this restriction. |
| **I Have A Shot** | Scavs and PMCs receive shotguns, with flashbangs as their grenade option. Close doorways and short corridors become particularly dangerous. The event changes the opposition’s loadouts; you can prepare your own approach. |
| **Marksman** | Scavs and PMCs receive sniper rifles instead of a mixed weapon pool. Long sightlines are dangerous, while short-range encounters have a very different rhythm from automatic-rifle fights. Carried grenades are limited to flashbangs. |
| **Explosive Scav Units** | Scavs carry pistols and grenade launchers. A seemingly ordinary Scav encounter can quickly turn explosive, especially around windows, cover and clustered enemies. Keep moving and leave room to retreat. |
| **Geared** | Opponents receive stronger equipment, including class-six armor plates and powerful weapons. This makes ammunition choice and positioning more important than in an ordinary raid. The theme improves gear; it does not mean every bot continually chases you. |
| **Overloaded** | Scavs and PMCs use compatible primary weapons with magazines holding at least 100 rounds, plus high-capacity pistol options. Expect longer bursts and fewer reload opportunities to exploit. Weapon selection is constrained to equipment that supports the required magazines. |
| **AK50** | The AK-50 becomes the raid’s signature weapon, including for the player. Your original weapons are handled by the return-mail system rather than deliberately discarded. Expect a loud, hard-hitting change from normal engagements. |
| **Grenadier** | Scavs and PMCs carry plentiful explosive grenades and are encouraged to use them. Cover that feels safe during a gunfight may not stay safe. Pay attention to escape routes and avoid being trapped in small rooms. |
| **Locked Loot** | Keyed rooms replace one to three suitable loose-loot placements with premium finds. The pool includes valuable items and cases, making keys and backpack space especially useful. Bring the appropriate key: the event does not simply unlock the room for you. |
| **The Thief** | A moving thief in a Santa hat carries stolen shop supplies, currencies and a chance of bitcoins. Watch for the runner and follow the encounter rather than expecting a fixed treasure chest. The event itself is separate from the optional story quest chain. |
| **Smokers Lobby** | Opponents receive replenishing smoke supplies and stagger their throws so the theme can last beyond the opening fights. Visibility changes as clouds develop around encounters. Your own brought-in loadout stays intact. |
| **Clone Wars** | AI PMCs copy your starting appearance and equipment. Familiar clothing becomes unreliable as a way to identify a target. This is a lookalike raid, not a promise that every non-PMC role becomes a clone. |
| **Matrix Glitch** | Groups share clothing sets, creating an uncanny repeated appearance across the raid. Their weapons remain independent, so matching outfits do not mean identical threats. The event changes recognition more than the basic rules of combat. |
| **Posers Everywhere** | Ordinary fighters wear Killa-style equipment and clothing. Seeing a familiar silhouette no longer means you have found the real boss. The people underneath retain ordinary fighter roles rather than becoming a map full of genuine Killas. |
| **Fake Cult** | Opponents dress like cultists while retaining their usual combat roles and equipment rules. It changes what you think you are approaching without turning every robe into an actual cultist. Judge the encounter by what the enemy does, not just the outfit. |
| **Gun Game** | Start with a pistol and advance through a weapon ladder by earning qualifying kills. New weapons replace the current stage, ending with rocket and grenade launchers. Finishing the ladder awards a bitcoin, with an additional hidden challenge to discover. Your original weapons return through trader mail after the raid. Available on Factory, Labs and Ground Zero. |
| **Gun Game [Respawns]** | The same ladder with another chance after defeat. Respawn at full health with a pistol; your stage progress and bonus progress reset. AI and the raid countdown continue, so it is not an unlimited-time mode. This is a separate Fence assignment on the Gun Game maps. |
| **Trials** | Fight ten successive waves of five hunters, with stronger opposition and equipment as you progress. The next group waits until every member of the current wave is dead, regardless of who killed them. The last wave includes a bitcoin. The normal map raid duration is doubled. |
| **Guard Trials** | Face successive five-person boss-guard squads. Later groups bring different guard families, and the final group mixes roles from earlier waves. Clear the current squad before another arrives. The normal map raid duration is doubled. |
| **Extreme Trials** | Fight ten waves of five copies of the same boss, progressing through the boss roster to the Labyrinth variants. Every opponent in a wave must die before progression. At least one member of each wave carries ten bitcoins. The normal map raid duration is doubled. |
| **Snowball Trials** | Bosses hunt you one at a time rather than fighting each other in one crowded spawn. Personally defeating the required roster is the challenge. There is also something hidden for curious players to discover. The normal map raid duration is doubled. |
| **Total Trial** | The long-form challenge: twenty waves with five hunters per wave. Clearing a squad brings the next step in the full progression. It is designed for a sustained fight rather than a brief variation on a normal raid, and doubles the normal map raid duration. |
| **Outbreak** | Infected replace the usual opposition, with further arrivals as the raid continues. Clearing the opening group does not end the threat. Watch your surroundings and keep enough supplies to survive repeated contact. |
| **Overrun** | The raid receives reinforcement squads as population falls. Fighting through one group does not mean the rest of the map is now empty. The event keeps replacing pressure while respecting its population controls. |
| **Stormtide** | Daylight gives way to darkness within about five minutes as heavy rain develops. Recorded rain, thunder, lightning and supported light flicker build the storm, with adjustable indoor/outdoor sound balances. A nighttime hurricane variation can add outdoor debris; the default RR chance is 30% when eligible. Labs and Factory are excluded. |
| **Blue Day** | An early siren announces mortar danger. Bombardments are scheduled within varied time windows rather than exact five-minute marks. Seek overhead cover when the shells begin; nearby AI can also respond to incoming strikes. It is available only where the event supports bombardments. |
| **Time Warp** | The world clock moves rapidly through repeated day and night cycles. The raid countdown, movement and weapon handling do not speed up with it. Outdoor lighting can change dramatically during one run, so bring equipment for more than the time you selected at deployment. |
| **Infrastructure Decay** | Supported lights suffer abrupt flickers, recovered surges and temporary blackouts. Electrical buttons and switches can stop responding during an outage, then become usable again when power returns. Day raids can be affected too, with a lower default chance. Some baked lighting or unsupported displays can remain visible. |
| **Wrong Door** | Crossing an eligible doorway has a 50% roll to move you to another safe doorway. Successful transfers have a cooldown, so every doorway is not an immediate chain reaction. The effect aims to preserve your travel direction and place you with room to move. Ordinary navigation can suddenly become unfamiliar. |
| **Lab Work Day** | Unarmed staff travel between the Black/Green and Red/Violet office areas while fighters battle nearby. Independent destinations, mixed walking/running, short rests and occasional proximity mutters help spread workers through the main work areas. Keyed rooms are open, and power interruptions can disrupt the run. Killing staff costs raid experience; three staff deaths call in hostile Raiders. The event is Labs-only. |
| **Schizophrenia** | Voices, unusual sounds, decoys and impossible-looking sights make the raid feel unreliable. Some effects react to what you do, while others are timed or chance-based. Extraction labels and other presentation tricks can mislead you without changing the real extraction requirements. The full surprise list is deliberately left undisclosed. |
| **Spring** | Use the game’s spring season for the selected raid. Outdoor scenery and weather presentation follow what the map supports for that season. All locations except Labs are eligible; changes in enclosed areas such as Factory may be subtle. The override is scoped to this raid. |
| **Summer** | Use summer scenery for the selected raid. Expect the map’s supported summer vegetation and seasonal presentation rather than a separate combat modifier. All locations except Labs are eligible, including Factory even where the difference is difficult to see. The override is scoped to this raid. |
| **Autumn** | Use the map’s autumn presentation, including supported foliage and environmental changes. This selects a season rather than adding special enemies or a separate challenge. All locations except Labs are eligible. The override is scoped to this raid. |
| **Winter** | Use supported winter scenery, with snow where the map provides it. Factory is eligible as well as outdoor locations, while Labs is excluded. This selects the season for the raid rather than permanently changing your save or all future maps. |

### Installation

Close the game and SPT server. Extract the eventual release ZIP into the **SPT installation root**, merging:

```text
README.md
BepInEx/
SPT_Runtime/
```

Use matching client/server files from the same package. Do not install source archives as mods. Existing settings and profiles are not part of the installer and must not be replaced.

**F1** opens SPT's Configuration Manager when that menu provider is installed and its shortcut is unchanged. The menu provider is not created or bundled by Raid Roulette. Configuration is also saved in `BepInEx/config/com.killdg.raidroulette.testing.cfg`.

The optional **DG Testing Tools** package exposes developer diagnostics. Ordinary players do not need it. The separate **Raid Roulette Tasks** package contains story missions and requires the RR engine; it is not included here.

### Compatibility

- Designed and tested against **SPT 4.1.6**, using the game's and SPT's assemblies.
- The main edition targets **solo play**. A separate Fika Edition is being investigated; no co-op support is claimed for this build.
- No additional AI, loot, weather or gameplay mod is required. Other mods that alter the same systems may conflict; combinations have not all been tested.
- AI still depends on native navigation and physics. Recovery relocations in Trials must be concealed at both ends; they are not a new navigation mesh.
- Blackouts affect supported runtime sources. Baked lighting and some displays may remain visible.
- Compilation and automated checks do not prove live movement, audio, extraction or quest lifecycle behavior.


{.endtabset}

## Credits

**Created by killdg, with assistance from ChatGPT.**

Inspired by **[Dynamic Raid Events](https://sp-mod.com/mod/2842/dynamic-raid-events)** by **Cobra**.

[Source code and testing status](https://github.com/killdg/Raid-Roulette) · MIT license

If you enjoy my work, [you can feed my caffeine addiction](https://ko-fi.com/killdg).
