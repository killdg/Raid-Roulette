# Validation and release status

Public beta: client **2.0.144**, backend **2.0.0-preview.68**, SPT **4.1.6**. Tests are confined to a dedicated solo installation. Results below distinguish user observations from automated verification; they are not exhaustive compatibility guarantees.

## Player-confirmed observations

- Gun Game: Factory and Labs runs confirmed weapon progression, rocket damage and respawn in the tested builds. Further population/pressure behavior can vary by map.
- Geared: the latest Factory test confirmed class-six plates on inspected opponents. Initial population and idle behavior still follow native map scheduling.
- Outbreak: user reported a successful Factory test.
- Explosive Scav Units: user reported a successful Ground Zero test.
- Boss Run Down: three user raids showed improved pursuit after routing repairs.
- Fence assignment replacement and supplied artwork: user confirmed these displayed and worked.
- Lab Work Day improved office staff movement, but closed-door handling and later routing changes still need regression tests.
- Schizophrenia, Stormtide, Smokers Lobby, The Thief, Locked Loot and several equipment events have earlier player confirmations; new changes still need targeted regression checks.

## Open checks

- Extreme/Guard/Snowball/Total Trials: complete wave runs, final rewards, timer extension and pursuit across maps.
- Client 2.0.144: verify stuck hunters do not disappear while visible. It now vetoes native visible contact and requires scenery concealment at both relocation ends, excluding actor colliders as cover. This is compiled/checked, not yet a live pass.
- Infrastructure Decay: powered button recovery, light-source restoration and day/night chance behavior.
- Lab Work Day: staff motion, native door interaction and raids with no missing workers.
- Fence: repeat accepts, end-of-raid reset, interruption/startup reset and accepted-only map markers.
- Wrong Door: preserve heading/runout; season changes: no residual snow on the next raid.
- Schizophrenia: audio fallback balance, extract-menu text restoration and late-raid effects.
- Stormtide: night hurricane chance, roof separation and recorded/gameplay sound balances.


## Automated evidence

The 2.0.144 client build completed without warnings/errors, and the 304 retained compatibility checks passed. The updated Fence checks passed 457 assertions. Such checks verify signatures and policies, not live animation, damage, sound or AI behavior.

Detailed local raid logs remain outside the public repository: they can contain session/profile information. Publish only redacted summaries. Fika co-op has not been verified for this solo edition.
