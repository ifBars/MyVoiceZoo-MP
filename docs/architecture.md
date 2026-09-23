# Co-op architecture

## Native integration

The installed game is Unity 2022.3.62f2 IL2CPP. MelonLoader 0.7.3 generates the local wrappers. The game owns Steam initialization and callback pumping. The mod uses the bundled Steamworks.NET wrapper, a friends-only lobby, and SteamNetworkingMessages.

Harmony prefixes intercept the native collection, area, camp, and costume UI actions for guests. Native compiled call paths were inspected because several UI callbacks inline manager operations. The host executes validated native manager operations; for remote adoption it applies the verified cost/collection sequence without opening the host's editor. Guest adoption opens the normal native editor after a host grant. The editor's Hide boundary submits the resulting name and voice. Guest animal income is suppressed; only the host produces shared gold.

## Authority and synchronization

The Steam lobby owner owns the zoo and its persistence. Commands carry unique request IDs and results are deduplicated. Edits require an animal lease bound to the requesting peer; heartbeat renewal keeps an active editor reserved. A peer leaving releases its leases. A change of lobby owner ends the session.

The host samples zoo state once per second and sends a revisioned full snapshot when it changes. Guests validate animal IDs, counts, positions, names, and gold before applying snapshots. Guests retain their last authoritative state for rollback after rejected speculative edits. Active local editing and dragging are excluded from ordinary snapshot overwrite.

Snapshots reference recordings by SHA-256 of their format and PCM16 samples. Missing clips are requested one at a time. Audio dimensions, digest, message sizes, queue budgets, and reassembly lifetime are bounded. Player poses are independent of zoo transactions and are rendered by inert visual replicas.

## Persistence

Before applying the first joined snapshot, a guest captures its own zoo and recording references. GameManager.SaveGame, SaveLoadSystem.SaveGame, and WavSaveLoadManager.Save are suppressed throughout the guest session and until restoration completes. Leaving restores the solo state before reenabling writes. Shared progress is saved only by the host through native save methods.

The opt-in data-directory override redirects SaveLoadSystem._path and animal recording filenames. Rooted recording filenames also redirect the game's pre-load File.Exists checks. The local test runner additionally backs up the shared LocalLow directory and restores it in finally, and never modifies the live Steam DLL.

## Boundaries

The mod does not migrate hosts or provide voice chat. Network compatibility requires the same protocol, mod build, game version and GameAssembly digest. Generated interop and native game artifacts remain local. Compile, managed transport tests, GSE gameplay, visual checks, and real Steam two-account validation are reported separately.
