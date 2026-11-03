> Historical engine notes: some paths and old F12/chance controls below describe earlier builds. Use README.md and BUILDING.md for the current player controls and build commands; inspect the current source for exact behavior.

# How Raid Roulette works

## Server and client

`src/RaidRoulette` is the SPT backend. It registers events, selects an eligible event for a generated raid, changes native location/bot data, and exposes an optional campaign interface. `src/Client` is the BepInEx plugin: it implements effects and behavior that need the running Unity world. The client uses SPT's existing request handler and session rather than a hard-coded HTTP address.

Start with `RaidRouletteMod.cs`, `Core`, `Runtime/RaidRulesRuntime.cs`, `Services/RaidRulesService.cs`, and `Client/Plugin.cs`. Event descriptions are under `Events`. Shared policies and identifiers live in `src/Shared`.

## Stormtide

`StormtideDirector` snapshots the weather debug values, clock and sky state. Over five real minutes, it changes rain, clouds, wind, fog and time of day. A daytime start reaches 23:00; a night start remains night. Raid countdown and combat speed are not accelerated.

`StormAudioAssets` loads optional PCM WAV recordings in the background, then creates Unity clips on the main thread. `StormtideRecordedRain` crossfades recorded rain against native rain. `StormtideSound` tracks source volumes, applies the event gains, and restores them afterward. Native volume-update hooks keep newly played sounds consistent with the mix.

`StormtideThunder` uses a temporary directional light for visual flashes and an environment-mixed audio source for supplied recordings. `ThunderSchedule` makes the first 33% chance roll at eight minutes, then rolls every three minutes. There are no damaging lightning strikes. Missing recordings fall back to native rain and omit recorded thunder.

## Lab Work Day

The backend makes independent unarmed staff profiles with native medical clothing and Security vests. Native neutral `gifter` spawning is reused; the client runs movement and door handling while bypassing ordinary combat/weapon behavior for staff.

`LabStaffBehaviour` explicitly sets empty hands and clears the weapon animation layer. Shots and hits feed a bounded danger queue. Nearby danger triggers a panic window; route selection uses complete NavMesh paths away from the threat, with cover preference and throttled replanning. There is no teleporting.

`LabStaffExperience` suppresses native combat XP and boss/combat kill credit for staff. A configurable penalty, defaulting to 1,000 XP per personal staff kill, is subtracted from the final post-bonus award before native XP is committed. The award is clamped at zero. `LabWorkDirector` requests a hostile Raider group after three personal staff kills. External overlays can still display the internal spawn role as a boss.

## Quests and persistence

Task Edition’s `src/QuestPackServer/Runtime/TraderQuestRuntime.cs` registers its embedded `Quests/quests.json` with SPT's native quest service. Stable IDs and predecessor requirements implement the chain. Event/map checks gate credit; item hand-in checks cover found-in-raid status and ownership/provenance where applicable. Reward rolls are stable per character.

Clone dogtags keep unique item IDs instead of inheriting one shared customization ID. `CloneDogtagRepair` repairs the known surviving legacy collision on profile fetch without creating missing tags or resetting progress.

## Scope

The local client drives several effects and behaviors. Multiplayer replication, authority and Fika synchronization still require separate work. Native signature checks help detect game-version mismatches; they do not replace live AI, animation, audio and persistence tests.

## Optional Quest Pack boundary

Task Edition’s `src/QuestPackServer` and Task Edition’s `src/QuestPackClient` build independent add-on assemblies. The backend depends on the event backend; the client declares a hard BepInEx dependency on the event client. `EventIntegration.IEventCampaign` exposes selection, raid-start and Gun Game-win callbacks. No campaign registered means ordinary random event selection.

Only the pack contains the catalog, quest routes, task lifecycle patches, kill/time counters, item filtering and hand-in proof synchronization. The core retains event directors, clone dogtag identity/legacy repair, event prizes and all Lab staff behavior. Quest-only mode falls back to Normal; the optional mixed setting returns control to core random selection when no quest applies. Explicit testing overrides take priority.

## Chance profiles and diagnostics

`Shared/RaidChances.cs` validates and allocates the F12 profile. OverallChance scales nonzero event shares by the overall event rate. IndividualPercentages uses exact out-of-100 values. Availability is evaluated after allocation, so excluded shares remain Normal instead of being redistributed. Invalid profiles leave the last accepted settings intact.

The momentary next-raid action sends a queue ID. `TestingControl` consumes the choice once, and recognizes resends of that queue ID so settings synchronization cannot re-arm it. Percentage edits do not clear a pending choice. The queue is in memory for the server session; F12 chance preferences persist in the client config.

`AdvancedMenu` controls Configuration Manager visibility and refreshes the setting list on changes. Monitor rendering, F8 input, roster scanning, diagnostic logging and Ghost Review are gated separately from display visibility. Event directors continue to work when diagnostics are hidden.

Task Edition source and builds are now published separately at https://github.com/killdg/Raid-Roulette-Task-Edition. Raid Roulette itself builds without that checkout.
