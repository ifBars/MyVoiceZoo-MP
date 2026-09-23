# MVZ-MP

Steamworks-based co-op mod for the Unity IL2CPP game MyVoiceZoo. Current milestone: friends-only lobby, Steam invite overlay, invite join, and peer handshake through the game's bundled Steamworks.NET. Shared zoo synchronization is the next milestone.

## Local setup

1. Install MelonLoader x64 into your MyVoiceZoo installation and launch the game once to generate `MelonLoader/Il2CppAssemblies`.
2. Copy `local.build.props.example` to `local.build.props` and set `GameDir` to your local game installation.
3. Run `dotnet build MVZ.MP.csproj -c Release`.
4. Copy `bin/Release/net6.0/MVZ.MP.dll` into the game's `Mods` folder.

In game, press **F6** to create a friends-only Steam lobby, **F7** to open the Steam invite dialog, or **F8** to leave. Accepting a Steam lobby invite joins the session. The loader log reports the lobby ID, member count, and protocol handshake. All players need the same game build and mod version.

For a menu-only host smoke test, add `--mvzmp-host` to Steam launch options. This creates the lobby once Steam initializes.

The lobby and handshake have been tested with two isolated GSE identities. The Steam overlay invite flow still needs a two-account live Steam test.

This milestone does **not** synchronize zoo progress, animal voices, player avatars, or saves yet. Keep ordinary single-player saves separate until host-owned state sync is implemented and tested. Even a menu-only run can create a save in the shared Windows `LocalLow` path, so back it up before running multiple local game processes.

Game-owned assemblies and generated wrappers are referenced locally and are never bundled in this repository.
