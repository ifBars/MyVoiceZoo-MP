# Repeatable local testing and packaging

Run the scripts from PowerShell 7. The test runner uses two fresh copies of the installed game under a unique directory in `TestRoot`. Use a spacious local drive for this root; it retains each run's installs and evidence until you inspect and remove them yourself. It never changes the live game installation.

## GSE co-op test

```powershell
$game = 'E:\SteamLibrary\steamapps\common\MyVoiceZoo'
$gse = 'D:\SteamLibrary\steamapps\common\Schedule I_alternate\Schedule I_Data\Plugins\x86_64\steam_api64.dll.goldberg'
$root = 'E:\MVZ-MP-Testing'
./scripts/Test-GseCoop.ps1 -GameDirectory $game -GseDll $gse -TestRoot $root -PlanOnly
./scripts/Test-GseCoop.ps1 -GameDirectory $game -GseDll $gse -TestRoot $root
```

The default DLL check pins the known GSE Client API SHA-256 `EF32F9BB1FEF9E9B58F3EA06F88B2EA1E206C861A4D0431D287E537C59A1A391`. The runner rejects an already running `MyVoiceZoo.exe`, a source install with `steam_settings`, overlapping test paths, and identical Steam IDs. The two local test identities are `76561198000040001` and `76561198000040002`; override both parameters if those are unsuitable. The only GSE modifications are in fresh copies: one native DLL, two `steam_appid.txt` files, and each copy's `steam_settings/configs.user.ini`. The original native DLL is retained as `.original` inside each disposable copy.

The runner builds Release unless `-SkipBuild` is set, installs only `MVZ.MP.dll` into each copied `Mods` folder, and launches the host with `--mvzmp-host`, then the client with `+connect_lobby <id>`. The shared-zoo scenario instead seeds an existing client save before joining the lobby. Both receive separate `--mvzmp-data-dir=<absolute>` paths under that run. It still moves the original shared `%USERPROFILE%\AppData\LocalLow\DefaultCompany\MyVoiceZoo` directory to a uniquely named adjacent backup before launch, even if the game is expected to honor data redirection. In `finally`, it stops only the two launched PIDs, moves any test-created shared directory into `evidence/test-created-user-data`, and restores the original directory. If the run is interrupted outside PowerShell's `finally`, find the backup path printed at startup and restore it manually after closing the two test processes.

Phase markers require fresh log evidence from **both** copies: Steam ready with distinct IDs; host creation and entry; client entry into the same lobby with `members>=2` and the expected owner; the host receiving the guest ready request; and the guest applying an initial authoritative snapshot. Each phase has `-TimeoutSeconds` (default 120). A run directory contains copied `host-Latest.log`, `client-Latest.log`, `result.json`, isolated installs, and separate data directories. A passing GSE transport test does not establish real Steam overlay invites, game state replication, or save safety during gameplay.

For an in-mod scenario that emits `PASS|<scenario>|...` on **both** peers, run:

```powershell
./scripts/Test-GseCoop.ps1 -GameDirectory $game -GseDll $gse -TestRoot $root -Scenario shared-zoo
```

The shared-zoo scenario adopts and names an animal, records a synthetic clip through the native editor, moves the animal, buys an area, camp, and costume, edits the adopted animal, checks the guest save hash, leaves, rejoins with an empty recording cache, then verifies solo restoration after host departure. Add `-Rendered` to exercise graphics and write `coop.png` for the client and timestamped `coop-*.png` frames for the host under their data directories. Add `-LobbyChat` only to force the diagnostic fallback transport. The scenario marker is checked after lobby and initial synchronization. Any `FAIL|...` line or process exit should be investigated in the retained per-peer logs. The mod owns scenario actions and assertions; this script owns build, isolated installs, process lifetime, log collection, and user-data preservation.

## Package

```powershell
./scripts/Package-Mod.ps1 -GameDirectory $game -OutputDirectory 'E:\MVZ-MP-Packages'
```

Pass `-GameDirectory $game` if `local.build.props` is absent. This builds Release and writes a uniquely named ZIP containing exactly `MVZ.MP.dll` and `README.md`. `-SkipBuild` packages the current Release DLL. The archive contains no game assemblies, generated interop wrappers, GSE files, recordings, or saves. Inspect the ZIP and perform live Steam validation separately before treating it as a public release.

