# Build and verify

Use a compatible SPT 4.1.6 installation, .NET SDK 10 and the .NET 8 runtime for the menu/client check projects. Referenced game/SPT assemblies stay local and must not be redistributed.

```powershell
./Build.ps1 -SptRoot 'B:/SPT MOD TESTING'
./Test.ps1 -SptRoot 'B:/SPT MOD TESTING'
```

The build writes `artifacts/INSTALL/README.md`, `BepInEx/` and `SPT_Runtime/`. It copies the client/server pair, example backend config, every bundled audio recording and every current Fence illustration. It does not install into SPT or modify profiles. Preserve existing active config files when merging an eventual release package.

For the cross-package compatibility harness, first build the matching separate Tasks checkout and supply its root:

```powershell
./Test.ps1 -SptRoot 'B:/SPT MOD TESTING' -TaskEditionSource '../Raid-Roulette-Tasks'
```

The script runs MenuTests and FenceChecks; the optional Tasks path also runs ClientChecks against native signatures and the built assembly pair. The older `src/Tests` integration harness references the legacy pre-merge Task Edition source layout and is not the current release gate. Do not mistake a skipped historical harness for a live gameplay pass.

Run the whitespace check before submission:

```powershell
dotnet format whitespace . --folder --include src --verify-no-changes
```

## Release provenance

Finish live checks, review the final source, commit and tag it, then build the download from that exact tag. Record its commit, SDK/SPT versions and DLL/ZIP SHA-256 hashes. Re-download uploaded assets and verify them before linking a release on the mod page. No obfuscation/minification step is used.

Do not include the optional DG Testing Tools DLL, game assemblies, profiles, private logs or source archives in the player installer. Source archives belong beside the matching binary release, not inside SPT.
