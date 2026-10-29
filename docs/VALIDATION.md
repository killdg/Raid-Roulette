# Validation snapshot

The full modular preview.31 / client 2.0.39 plus Task Edition backend 1.0.0-preview.4 / client 1.0.2 snapshot passed **830 automated checks** in the author's environment: 508 backend, 156 pure/menu/audio, and 166 client metadata/compatibility checks. All four production assemblies completed with zero warnings and errors.

Those counts included local native database fixtures, the supplied three audio recordings, and an earlier friend-package configuration fixture. Optional inputs omitted from a fresh checkout reduce the count. Validation logs are not published because they can include machine paths; tests and runner scripts are included.

The .NET test-host Harmony runtime smoke check was skipped due to a MonoMod binding limitation. The unchanged native HTTP adapter check was also skipped. Client metadata checks verify expected signatures and compiled wiring against the installed game assemblies; they do not execute Unity gameplay.

The latest Labs changes still need a live raid test: medical outfits and Security vests, empty-hands animations, fleeing from shots/hits, suppressing native boss/combat credit, and the configurable civilian XP penalty. Also test the three-kill Raider response, result display, level boundaries, death/extraction and repeat raids.

The quest chain and clone dogtag fixes need live regression checks for hand-ins, reward delivery, accepted-quest migration and persistence. Extreme Overwatch's extra sniper spawning remains unresolved. Other events without explicit player confirmation are marked as needing verification in the [catalog](EVENTS.md).

New checks verify the absence of quest code/resources from core assemblies, one-way dependencies, campaign registration and dispatch, quest-only versus mixed selection, and retention of core clone fixes. Quest JSON and shared quest IDs are byte-for-byte unchanged from the installed combined snapshot.

Chance/Advanced checks cover exact 10/10/80 allocations, Labs fallback to 90% Normal, zero exclusions, overall pool shares, invalid totals/non-finite values, retaining prior settings, one-shot consumption/replay protection, and compiled Advanced gates. The revised F12 visibility and queue UI still need a live test.
