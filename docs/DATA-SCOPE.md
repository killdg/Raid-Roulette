# Network, file and engine scope

## Network

Raid Roulette uses SPT's existing authenticated local request handler and session. Local `/client/raidroulette/*` routes synchronize next-raid preferences, Gun Game equipment/progression, event receipts, room-loot requests and optional campaign/debug state. Cadence/state guards suppress irrelevant work; this is not a claim of zero requests outside an event. See `NetworkCadence`, the client directors and backend `Testing`/`Quests` routes.

There is no analytics endpoint, telemetry upload, account service or unrelated external connection. Debug messages remain in the local SPT/BepInEx logs. The separate story task package owns its own mission synchronization.

## Files

The plugin reads its bundled audio from the plugin's Audio folder and the backend serves bundled Fence artwork from its own FenceImages folder. BepInEx stores preferences in its normal config directory. Compatibility migration reads only explicitly recognized prior preference files. Native SPT services save inventory/quest state to the selected local profile; the mod does not implement an external profile editor.

Build/release scripts read assemblies from the explicitly supplied SPT installation and write only repository artifacts. They do not install into another game directory. Do not distribute those referenced proprietary assemblies, profiles, credentials, logs or decompiled source.

## Native engine work

Harmony patches integrate event selection, profile generation, native UI, weather/audio, combat progression and powered interactions. Event directors scope behavior to the running world/event and restore owned clock, volume, light or UI state on cleanup. Rendered hallucinations are cosmetic; Trials and other combat events spawn real opponents. Native navigation remains authoritative; bounded recovery is not a rebuilt map mesh.

Please report the event, map, installed versions and redacted relevant log lines. Do not post complete profiles or session credentials.
