# Build, install and inspect

Use Windows, your own SPT 4.1.6 installation with game 0.16.9.5.40743, .NET SDK 10, and the .NET 8 runtime for the menu/metadata checks. These are development prerequisites; Python and other mods are not runtime dependencies.

```powershell
.\Build.ps1 -SptRoot 'C:\SPT'
.\Test.ps1 -SptRoot 'C:\SPT' -TaskEditionSource '..\Raid-Roulette-Task-Edition'
```

The scripts accept `-Dotnet` if the SDK is not on PATH. The backend uses `-p:SPTBinaries=<SPT folder>\SPT_Runtime`; the client and compatibility checks use `-p:SPTPath=<SPT folder>`. No proprietary reference DLLs are copied into this repository.

`Build.ps1` creates `artifacts/INSTALL`, containing only the two event mod DLLs, a configuration example and the three bundled recordings under `assets/Audio`. It does not install or modify the live game. Copying locally generated output should never include game reference DLLs.

To install a build, close SPT.Server and Tarkov first and back up the existing mod, its configuration and your profile. Merge `artifacts/INSTALL` into your SPT root. On a fresh installation copy the backend's `config.example.json` to `config.json`; preserve an existing configuration when updating. Keep `Audio` beside the client DLL. Both event DLLs are required. Task Edition is built from its separate repository. Install both products to retain the combined build’s tasks. Raid Roulette builds without a Task Edition checkout; full integration checks require its source path and both products built.

F12 exposes normal-player selection controls. Select a type and press Queue next raid for a one-shot override. Otherwise use OverallChance or IndividualPercentages. Unsupported shares become Normal. Advanced diagnostics, the roster and F8 monitor require Show advanced options; it defaults off. Manual choices still obey map/asset eligibility.

Task Edition’s ten quests use stable IDs. Existing accepted tasks/progress are retained, but later quests wait for their predecessors. Follow the [quest guide](QUEST-GUIDE.txt). Avoid removing quest definitions while relying on them in a character's save.

`Test.ps1` defaults to skipping the backend Harmony runtime smoke test because the local .NET test host encountered a MonoMod binding limitation; `-IncludeHarmonySmoke` opts into that check. The unchanged native HTTP adapter check is skipped in the documented runner. Audio-specific checks run when all three bundled recordings are present. The retired friend-package fixture is not required by this runner, so its counts differ from the author's full validation snapshot.

A build succeeding does not establish compatibility with another game/SPT version. Use a separate test installation for live verification.

Task Edition’s `RandomEventsBetweenQuests` setting defaults to false. A missing pack config uses that default; copy its example to `config.json` to customize it. Existing core configuration is preserved. Neither build script modifies profiles or installs into the game.
