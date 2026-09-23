---
name: mvz-modding
description: Develop MyVoiceZoo IL2CPP MelonLoader mods from local runtime evidence, with native Steamworks integration and host-owned zoo state.
---

# MyVoiceZoo Modding

Adapted from `ScheduleOne/.agents/skills/schedule-one-modding` for this game's Unity 2022.3 IL2CPP build. Do not apply Schedule One branch names, S1API, SteamNetworkLib, FishNet, or dedicated-server assumptions to MyVoiceZoo.

1. Inspect the installed game and fresh `MelonLoader/Il2CppAssemblies` before choosing a hook. Verify assembly generation against the current `GameAssembly.dll` and metadata.
2. Keep MelonLoader lifecycle code small. Patch only identified native methods, using Harmony prefix/postfix where appropriate. Avoid IL transpilers on IL2CPP and avoid duplicate `PatchAll` registration.
3. Steam integration should use the game-bundled Steamworks.NET wrapper and existing `SteamManager` initialization/callback loop. Preserve callbacks and call results for their lifetime. Use the actual Steam lobby owner as the zoo authority.
4. Keep game-owned binaries, generated wrappers, recordings, saves, prefab exports, and decompiled output local. Reference them through ignored machine-local build configuration.
5. Treat host/client, runtime state, local save state, audio recordings, and network protocol as separate concerns. Guests must not persist a joined zoo into their own single-player save.
6. Confirm compile, game load, Steam ready, lobby creation, two-account join, and shared-state behavior as distinct milestones. Do not infer one from another.

