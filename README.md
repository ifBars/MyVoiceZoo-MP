# MVZ-MP

A Steam co-op mod for MyVoiceZoo. Up to four friends share the host's zoo using the game's normal adoption, recording, building, and costume interfaces.

## Install and play

1. Install **MelonLoader 0.7.3 x64** into MyVoiceZoo and run the game once.
2. Put `MVZ.MP.dll` in the game's `Mods` folder. Every player needs the same game and mod build.
3. Load your zoo and open the native settings menu and choose **Host** in its co-op section, then **Invite** to open Steam's invite dialog.
4. Friends accept the Steam invite. Their zoo is temporarily replaced by the host's shared zoo for the session.
5. Choose **Leave** to return to your own zoo. The host retains shared progress.

Keyboard shortcuts: **F6** host, **F7** invite, **F8** leave. Steam must be running with the game available to each player. The lobby is friends-only.

## Shared play

- Shared animals, names, recordings, placement, gold, areas, camps, and purchased costumes.
- Native adoption and edit screens. The host validates costs and reserves an animal while a player edits it.
- Each player keeps independent movement and equipped appearance; remote players have nameplates.
- Recordings transfer when shared animal state requires them. There is no live microphone chat.
- Guests do not save the shared zoo into their solo save. Leaving restores their pre-join zoo in memory.
- If the host leaves, the session ends. Host migration is not supported.

SteamNetworkingMessages carries state and recording data. A bounded lobby-chat transport is retained for diagnostic compatibility. Mod protocol and game fingerprints must match before joining.

## Build

Copy `local.build.props.example` to `local.build.props` and set `GameDir` to your installed game. After MelonLoader generates its IL2CPP wrappers:

```powershell
dotnet build MVZ.MP.csproj -c Release
```

Only `bin/Release/net6.0/MVZ.MP.dll` is installed. Game assemblies, generated wrappers, recordings, saves, and GSE are never included in the mod package.

## Development validation

See [the test runner](docs/testing-runner.md) for isolated two-process GSE tests and packaging, and [architecture](docs/architecture.md) for authority and persistence rules. Test-only `--mvzmp-data-dir=<absolute>` redirects game saves and recordings; `--mvzmp-smoke=shared-zoo` runs the disposable-save gameplay scenario. Never use the smoke option on a personal save.

A local GSE test does not verify Steam overlay invite acceptance between two real Steam accounts. Current validation results are recorded in [runtime validation](docs/runtime-validation.md).
