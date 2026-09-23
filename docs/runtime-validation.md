# Runtime validation — 2026-09-23

## Environment

- MyVoiceZoo: installed Unity 2022.3.62f2 IL2CPP build, game version 1.0.
- MelonLoader 0.7.3, mod 0.2.0, wire protocol 3.
- Two isolated GSE identities: 76561198000040001 and 76561198000040002.
- Original Steam installation and user save are kept separate from test installs. The runner restores the complete original user-data directory after each run.

## Verified

- Release compilation without warnings/errors; managed transport framing, ordering, reassembly expiry, limits, and epoch checks.
- Native SteamNetworkingMessages sends and receives between the two game processes.
- Initial full-state synchronization, guest animal adoption, native reveal/editor flow, naming, voice recording, host persistence, placement, area and camp purchases.
- Costume purchase/equip and editing an existing adopted animal.
- Five-second mono 48 kHz recording transferred to the host and saved as a 480,044-byte WAV.
- Existing guest save remains byte-identical while joined. Guest's original gold and animals return on leaving.
- Leave/rejoin into the same lobby, renewed session epochs, a fresh 480,000-byte recording download, and automatic solo restoration when the host leaves.
- Native game reload restores the host's saved animal, name, position, areas/camps, and five-second recording.
- Rendered game shows remote character replicas and distinct costumes; native UI panel is readable.

Primary local evidence is retained outside Git under `E:\MVZ-MP-Testing`. The native gameplay run `run-20260923-052109-35f2edaf` passed both peers. The larger recording run `run-20260923-052214-68f018b0` passed gameplay and reload; its nameplate construction error was subsequently repaired. The rendered costume/edit run `run-20260923-052532-1aa0045d` passed gameplay with no avatar creation errors; nameplate readability is under final visual verification.

The follow-up native run `run-20260923-052902-5f08a9b7` passed the fresh recording download and host-departure checks.

## Remaining validation

A real two-account Steam overlay invite/accept session is not covered by GSE. WAN latency and four simultaneous players are also not established by the local two-process run. The mod does not migrate the host.

## Interop findings

Generated IL2CPP wrappers are not sufficient evidence of safe calls. SteamNetworkingMessagesSessionFailed callback registration rejected its non-blittable struct; GetSessionConnectionInfo out-structure access crashed natively. The transport uses supported callbacks, bounded queues, send-result backpressure, and lobby ownership/liveness instead. Animator parameter arrays failed managed constraints and indexed parameter introspection crashed natively; the replica uses the game's verified `isRun` parameter directly. TextMeshPro font assignment requires an active initialized renderer.

