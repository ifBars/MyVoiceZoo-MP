# Co-op architecture

## Confirmed local seams

MyVoiceZoo is a Unity 2022.3.62f2 IL2CPP game. MelonLoader 0.7.3 generates interop assemblies from the installed build. The game bundles Steamworks.NET and initializes it through `SteamManager`. `SteamManager.Update` exists and the mod observed `SteamManager.Initialized` in a live process.

The generated game types expose `AnimalManager` adoption, edit, name, voice, and collection operations; `Wallet` gold operations; `AnimalPrefabController` spawning and position lookup; `GameManager` save/load; `GameSaveData` for animal, gold, position, area, camp, and costume data; and `WavSaveLoadManager` for recordings. These are method and data seams, not proof that network application of each operation is safe.

## Host-owned zoo

The Steam lobby owner is the session host and owns the persistent zoo. Guests send commands with an action ID. The host validates each action against current zoo state, executes it once, and publishes the resulting state with a monotonically increasing revision. Guests apply only newer revisions. Rejoining clients receive a full snapshot before incremental events.

The host must decide animal adoption cost and gold changes together; blindly replaying a guest-side `AdoptAnimal` call risks bypassing or double charging the game's UI transaction. Identify the vanilla purchase call chain before implementing guest adoption. Similar care is needed for area and costume purchases.

Voice clips should be encoded and sent in bounded chunks over Steam networking messages, with a content hash and maximum size. The host stores the accepted recording and announces its version to guests. No microphone audio should be sent merely because someone joins.

Guest game saves must be suppressed while joined and restored to normal on leave. Before the first state sync, make a disposable copy of a save and verify join, leave, reload, and crash recovery. The host saves through the game's existing path.

Player presence is a later stage: inspect the live `Player` prefab and movement/camera behavior before adding a guest avatar. The host zoo can be shared before avatar replication, but the UI must clearly show that interim scope.

## Validation gates

1. Mod loads in the current IL2CPP build and sees Steam initialized. **Passed locally.**
2. Friends-only lobby creation and enter callbacks work. **Passed with one Steam account.**
3. Two distinct local GSE identities join the same lobby and exchange the protocol handshake. **Passed in isolated installs.** Real Steam invite acceptance remains pending.
4. Host sends a full snapshot; guest receives it without writing over its local save. **Pending.**
5. Guest requests an animal action; host validates and applies it once; both see the same result and gold. **Pending.**
6. Recordings, positions, player presence, reconnect, and host-leave behavior are validated separately. **Pending.**

Game assemblies, generated wrappers, recordings, and saves remain outside this repository.

The GSE menu-only run created `save.json` under the shared Windows `LocalLow/DefaultCompany/MyVoiceZoo` directory. It was absent before testing; its test-created contents were backed up outside the repository and the pre-test state was restored. Future multi-process runners must back up and restore this directory, even for menu-only runs.
