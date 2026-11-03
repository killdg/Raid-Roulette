# Raid Roulette Language Pack — 1.0.0 beta

An optional, offline language add-on for **Raid Roulette 2.0.146** on **SPT 4.1.6**.
English remains the base mod's default. Install this single pack to use the
language selected in the game's language settings. Unsupported or missing text
falls back to English. Restart the game and server after installation, removal,
or changing the selected language so cached trader and settings text refreshes.

## Languages

Simplified Chinese, Czech, Spanish, Latin American Spanish, French, German,
Hungarian, Italian, Japanese, Korean, Polish, Brazilian Portuguese, Romanian,
Russian, Slovak and Turkish. English is included as the fallback catalog.
These are the 16 additional locale banks in the targeted SPT build; this is not
an assertion that every newer game release has the same language list.

## Included translations

- All 43 current Fence RR assignment titles, briefings, objectives and dialogue.
- Event names in the manual picker, custom event mix, map badge tooltips and
  the start/end raid-event notice, including Normal.
- Main RR settings labels, preset names and core custom-menu controls.

Translations are a first pass by killdg with ChatGPT assistance. Native-speaker
review is welcome. This is **not yet a fully localized release**: longer settings
help, dynamic progression/error messages, some scare notifications and developer
interfaces can remain English. Logs and internal IDs intentionally remain English.
Some search filtering still uses the underlying English event names. Chinese,
Japanese and Korean glyph rendering in the IMGUI settings panel needs a live
check with the selected native language fonts. Native Fence text uses the game's
own UI font handling; no proprietary fonts are distributed.

## Installation

Install Raid Roulette first. With the B: test game and server closed, extract
`BepInEx` and `SPT_Runtime` from the add-on ZIP into your **SPT root**, merging
folders. The add-on has its own folders and does not replace the main RR DLLs.
Both the server and client component are needed: the server supplies Fence text;
the client supplies the event labels and menu presentation. A Configuration
Manager is optional; if absent, its settings-label hook is skipped.

There is no separate language selector. Select a supported language in the game
and restart. No extra gameplay mod or online translation account is required.

## Removal

With the game and server closed, remove only:

- `BepInEx/plugins/RaidRouletteLanguages`
- `SPT_Runtime/user/mods/RaidRouletteLanguages`

Restart both processes. The original RR English text returns. Quest IDs,
accepted tasks, inventory, profiles and saved configuration keys are not changed.
The parent RR mod retains its own ordinary quest lifecycle.

## What this add-on accesses

Translation JSON is read once from its own `Locales` folders. There are no file
watchers, telemetry, downloads, external connections or new API routes.

The server hooks the native locale service and inserts only the existing RR
Fence assignment keys into the requested non-English language bank. It verifies
registration against the English bank first. The client reads the native culture
and listens for its change notification; it does not scan the scene or poll the
server. Targeted RR hooks translate notices and tooltips. Scoped Unity GUI hooks
translate known labels only while RR draws its own menus. Configuration Manager
hooks change presentation labels and reversible enum display conversions, not
saved setting values. Client hooks and the language listener are removed on
plugin destruction. Unknown text remains unchanged.

## Validation status

The author reports that the installed add-on worked in a live test. This does
not verify all 16 languages. Catalog/overlay and native API checks are automated.
CJK font rendering, every language and language switching still need broader
review. No co-op validation is claimed.
A suggested first test is to select a language, restart, inspect Fence, hover an
accepted assignment's [RR] map badge and inspect RR's menu and raid notice.

## Source and building

Source is included separately with the release. Code is not obfuscated.
Translation catalogs are plain UTF-8 JSON. `build_catalogs.py` regenerates them
from the readable translation rows and `EnglishEvents.json`, using Python's
standard library only. Both modules compile from the same shared catalog code.
See `build.ps1` for local assembly references, checks and packaging. Do not
redistribute native game/SPT DLLs with the add-on.

Original add-on work: **killdg**, with **ChatGPT assistance**. MIT license.

