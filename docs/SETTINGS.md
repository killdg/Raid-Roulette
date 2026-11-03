# Raid Roulette settings guide

Applies to the **2.0.146 beta**. Press **F1** and expand **Raid Roulette**.
F1 opens the installed Configuration Manager; its shortcut may be changed in
that provider. Saved preferences survive updates, so the defaults below describe
a fresh installation. Settings are saved automatically.

## General and event selection

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

## Raid chances

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

## Raid event notices — the bottom-corner message

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

## Stormtide

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

## Infrastructure Decay

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

## Additional controls

| Setting | Fresh default | What it does |
| --- | --- | --- |
| **Overrun: Replacement cooldown seconds** | 3 seconds | Minimum delay between population replacement requests. Overrun checks low population rather than using a fixed wave timer. |
| **Overrun: Bots per wave** | 0 (automatic) | Uses the ordinary Scavs/PMCs observed during the first 60 seconds, with a minimum of one; excludes bosses/followers. A positive value sets a fixed count. |
| **Overrun: Maximum alive AI** | 35 | Reinforcements wait for capacity. Counts all living AI; native spawn limits still apply. |
| **Lab Work Day: XP penalty per staff kill** | 1,000 XP | Deducts experience per staff member personally killed. Final raid XP cannot fall below zero. Captured at raid start. |
| **Respawn after lethal damage** | Off | Legacy solo Gun Game preference for non-Fence Gun Game selection. A Fence Gun Game assignment overrides it for that raid; use Fence's **Gun Game [Respawns]** for the simplest choice. Respawn resets the ladder and knife bonus, restores health and equips a pistol. The raid timer continues. |

Overrun population settings are captured at raid start. These values only affect
the event that uses them, not every ordinary raid.

## Developer tools

**Show advanced options** defaults to off and requires the optional **DG Testing
Tools** plugin. It exposes development controls such as the live AI monitor,
hunter tracking, Ghost Review, quick travel, spawn logging, assisted wave
progression and the F2 scare panel. Ordinary players do not need that plugin.
Developer controls can change a test's outcome; they are not required to activate
Fence events or use random raids.

## Saved configuration

RR settings are stored in
`BepInEx/config/com.killdg.raidroulette.testing.cfg`. If editing this file by
hand, close the game first. Updating RR preserves saved preferences; new default
values do not automatically replace choices already saved in that file.
