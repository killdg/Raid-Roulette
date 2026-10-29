# Raid Roulette

**Created by killdg. Developed with assistance from ChatGPT.**

A raid modifier mod for SPT. Choose a one-shot event for the next raid or customize automatic event percentages. This repository contains the event engine; trader quests are published separately as [Raid Roulette Task Edition](https://github.com/killdg/Raid-Roulette-Task-Edition).

Current snapshot: **backend 2.0.0-preview.31**, **client 2.0.39**. Built against **SPT 4.1.6 / Escape from Tarkov 0.16.9.5.40743**. 

## Choose your install

| Mod | Purpose | Requires |
|---|---|---|
| **Raid Roulette** | Random raid events, event effects, testing controls and raid notices. No trader quests. | Compatible SPT |
| **[Raid Roulette Task Edition](https://github.com/killdg/Raid-Roulette-Task-Edition)** | Trader stories, objectives, rewards and quest-driven event selection. | Raid Roulette backend preview.31 and client 2.0.39 |

Install Raid Roulette alone for variety in ordinary raids. Add Task Edition for the campaign. By default, Task Edition requests a current quest's event only on a suitable map; raids with no applicable quest stay Normal. Set `RandomEventsBetweenQuests` to `true` in Task Edition's `config.json` to keep random events between quest raids. A queued one-shot raid choice takes precedence.

Existing users of the combined build should install **both** parts to retain their tasks. Quest IDs, definitions and progress formats are unchanged; migration still needs live verification. The event-only build does not register quests.

## F12 controls

Choose a raid type and press **Queue next raid** to use it once, then return to automatic selection. Queues last for the current server session. An unsupported manual choice produces Normal for that raid.

- **OverallChance:** set the overall event rate, for example 40% events and 60% Normal. Nonzero event entries share the event pool; zero excludes a type.
- **IndividualPercentages:** use exact allocations, for example Normal 10%, Clone Wars 10%, Stormtide 80%, everything else 0%. Totals above 100% are rejected; unused percentage becomes Normal.
- Unsupported map or asset shares become **Normal** rather than increasing other events. On Labs, the example above becomes Clone Wars 10% and Normal 90%.
- **Advanced → Show advanced options** defaults off. Enable it for the AI roster, live monitor/Boss Hunt tracking, F8 shortcut, spawn diagnostics and Ghost Review. Those diagnostics stay inactive while Advanced is off.

Changes apply to future generated raids. F12 settings are saved by BepInEx and synchronized to the backend; the game's normal raid countdown is unaffected. The backend's JSON selection remains a fallback before client settings are synchronized.

## What is here

- 31 special event definitions plus Normal, with event selection and map eligibility checks.
- Server-side loot, loadout and spawn changes, paired with local client behavior where needed.
- Stormtide weather/audio, Time Warp, hallucinations, Gun Game, Boss Hunt, Clone Wars and more.
- Optional integration with the separate Task Edition campaign; no quests are registered by Raid Roulette itself.
- Native after-raid notices, F12 next-raid choice and percentage controls; optional diagnostics under Advanced.
- Tests and compatibility checks, including the latest civilian XP, movement and clothing changes.

See [event status](docs/EVENTS.md), [how it works](docs/ARCHITECTURE.md), [build and install](docs/BUILDING.md), [Task Edition quest guide](https://github.com/killdg/Raid-Roulette-Task-Edition/blob/main/docs/QUEST-GUIDE.txt), and [validation limits](docs/VALIDATION.md).

## Current testing state

This is a development preview. Player feedback has confirmed several events, but not every event or combination of mods. The latest Labs changes and modular split are compiled and checked; they still need a live raid test. Automated checks do not establish that Unity animations or AI behavior work in a raid.

The project targets **local solo play**. Fika support and co-op synchronization are not implemented. It does not require another quest, weather or AI mod. It uses the game and SPT-provided BepInEx/Harmony assemblies.

## Download

Get the installable event-only ZIP from [Releases](https://github.com/killdg/Raid-Roulette/releases). Task Edition is a separate optional download. Close SPT.Server and Tarkov and back up an existing installation before updating.

## Build

Install .NET SDK 10 and the .NET 8 runtime for the check projects, and use your own compatible SPT installation:

```powershell
.\Build.ps1 -SptRoot 'C:\SPT'
```

This creates the event package in `artifacts/INSTALL/` and does not install anything into the game. Game/SPT DLLs and database files are referenced locally and are not distributed here. The three bundled custom rain/thunder recordings are described in [assets/Audio](assets/Audio/README.txt).

## Contributing

Bug reports should include the event, map, game/SPT version, steps to reproduce, and relevant redacted log excerpts. Identify other installed mods and whether the test was solo. Please keep profiles, session credentials and complete unredacted logs out of issues.

## License and attribution

Project creation and authorship: **killdg**. ChatGPT assisted with development and troubleshooting.

Project source is licensed under [MIT](LICENSE). Escape from Tarkov, SPT, Unity, BepInEx, Harmony and third-party assets retain their own ownership and licenses. No game binaries, decompiled game source, profiles or bundled third-party runtime libraries are included.

SPT upstream: https://github.com/SP-Tushonka/build  
Fika project (future compatibility target): https://github.com/project-fika

Publisher identifiers now use killdg. On first launch, an empty new F12 configuration imports the single existing Raid Roulette preference file, without deleting the original. Quest IDs and profile progress formats are unchanged.
