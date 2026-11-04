# Changes

## 1.0.1

- Fix the blank F1 settings window caused by processing BepInEx core settings,
  which have no plugin owner. Skip those rows before accessing plugin metadata.
- Skip all settings translation immediately when English is selected.
- Add regression checks that invoke the actual compiled client hook with a null
  owner in English and all 16 additional locales. No game is launched by these checks.

Restart the game after replacing the add-on. The base RR files, saved settings,
profiles and quest IDs are unchanged.
