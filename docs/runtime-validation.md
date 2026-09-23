# Runtime validation — 2026-09-23

## Initial 0.2.0 environment

- MyVoiceZoo: installed Unity 2022.3.62f2 IL2CPP build, game version 1.0.
- MelonLoader 0.7.3, mod 0.2.0, wire protocol 3.
- Two isolated GSE identities: 76561198000040001 and 76561198000040002.
- Original Steam installation and user save are kept separate from test installs. The runner restores the complete original user-data directory after each run.

## Verified

- Release compilation without warnings/errors; managed transport framing, ordering, reassembly expiry, limits, and epoch checks.
- Native SteamNetworkingMessages sends and receives between the two game processes.
- Initial full-state synchronization, guest animal adoption, native reveal/editor flow, naming, synthetic audio through native recording completion, host persistence, placement, area and camp purchases.
- Costume purchase/equip and editing an existing adopted animal.
- Five-second mono 48 kHz recording transferred to the host and saved as a 480,044-byte WAV.
- Existing guest save remains byte-identical while joined. Guest's original gold and animals return on leaving.
- Leave/rejoin into the same lobby, renewed session epochs, a fresh 480,000-byte recording download, and automatic solo restoration when the host leaves.
- Native game reload restores the host's saved animal, name, position, areas/camps, and five-second recording.
- Rendered game shows remote character replicas and distinct costumes; native UI panel is readable. Both viewpoints show a readable nameplate above the other player, with no self nameplate.

Primary local evidence is retained outside Git under `E:\MVZ-MP-Testing`. The native gameplay run `run-20260923-052109-35f2edaf` passed both peers. The larger recording run `run-20260923-052214-68f018b0` passed gameplay and reload; its nameplate construction error was subsequently repaired. The rendered costume/edit run `run-20260923-052532-1aa0045d` passed gameplay with no avatar creation errors; nameplate sizing was finalized in the subsequent run.

The follow-up native run `run-20260923-052902-5f08a9b7` passed the fresh recording download and host-departure checks.

Final rendered acceptance run: `run-20260923-053105-c446f967`. Both peers passed all shared-zoo actions, costume/edit, existing-save preservation, fresh recording download, reconnect, and host departure. The client screenshot and host frame `coop-123150036.png` were visually inspected and confirm other-player-only nameplates. No avatar exceptions or rejected actions were logged.

## Remaining validation

A real two-account Steam playtest of 0.2.0 was subsequently reported by the user: most features worked, but remote movement/depth and possibly guest microphone capture needed investigation. This is user-reported evidence; the automated GSE run does not establish real Steam invite acceptance. Four simultaneous players remain untested. The mod does not migrate the host.

## Interop findings

Generated IL2CPP wrappers are not sufficient evidence of safe calls. SteamNetworkingMessagesSessionFailed callback registration rejected its non-blittable struct; GetSessionConnectionInfo out-structure access crashed natively. The transport uses supported callbacks, bounded queues, send-result backpressure, and lobby ownership/liveness instead. Animator parameter arrays failed managed constraints and indexed parameter introspection crashed natively; the replica uses the game's verified `isRun` parameter directly. TextMeshPro font assignment requires an active initialized renderer.



## 0.2.1 playtest fixes

- Protocol 4 puts 20 Hz player poses on a separate latest-only, sequenced unreliable channel. Managed tests cover replacement, loss, reorder, duplicate rejection, sequence wrap, session epochs, and isolation from reliable reassembly.
- Timestamped presentation tests cover irregular arrival, following a corner, holding on packet loss, stale samples, invalid coordinates, and teleport snapping. No velocity extrapolation is used.
- Runtime source inspection confirmed the native player uses `SpriteSortPoint.Pivot`; replicas now copy it. They also preserve any source sorting groups.
- `run-20260923-063111-e4934fc8` passed the rendered shared-zoo regression and native movement probe. The native player stopped at approximately (0, 2.12) against the tent while upward input remained held. Both viewpoints at `motion-03.png` were inspected: the source and remote replica stand in front of the tent, with only remote nameplates. The top-right overlay is absent.
- `run-20260923-062821-68210529` exercised actual guest microphone capture on the local headset through the native record button and recorder stop. Sample position advanced to 86,877; native trimming returned 8,820 samples at 44.1 kHz. PCM peak was 0.002716 and RMS 0.000652. Identical hash/levels were verified on the host, guest, and guest after reconnect. This proves nonzero capture and transfer on this machine; it does not establish intelligible speech, speaker audibility, or the other player's hardware configuration.
- The normal synthetic-tone regression verifies non-silent PCM and playback AudioClip contents on both peers and after reconnect. Native save reload remains covered by the earlier persistence test; current microphone WAV reload was not separately rerun.
- Recording diagnostics identify the game-selected device, start failures, and silence before transfer without changing microphone selection.

The scripted adoption flow can log native `CollectionView.Hide` null-reference exceptions because it invokes the editor without first browsing the collection. Shared-state and recording assertions still pass; this is not being reported as an exception-free manual playtest. Repeat two-account movement feel and recording with the intended microphone remains useful after updating both players.

- Final native settings inspection used the retained populated host clone from `run-20260923-062450-62dfa8a5`; `evidence/settings-Latest.log` records Host/Leave button callback success and hide success. The final `host-data/settings.png` was visually inspected: native audio/language/resolution/quit controls remain within the frame, the co-op column fits inside it, and disabled actions are visibly muted. The settings-only launch needs explicit window width/height; an earlier hidden launch without them produced a black capture and was rejected as visual evidence.

## 0.2.2 settings layout

Widened the native settings popup from 1400 to 1560 UI units and retained Quit Game at the popup's horizontal center. Release build passed. The isolated settings test passed Host/Leave callbacks and hide checks; its fresh screenshot was inspected at 1920x1080 and confirms left-side text stays inside the painted frame and Quit Game is centered. No gameplay or transport changes.

The startup hint waits for the splash, game loading screen, and microphone guide to finish, then uses a four-second unscaled timer (brief fade-in, full visibility, final one-second fade-out). Earlier captures hidden behind the microphone guide were rejected; the final readiness condition includes that native guide explicitly.

Final startup capture was visually inspected at full opacity and at alpha 0.50, over the loaded zoo. Logs show display at 06:43:09.789 and removal at 06:43:13.766. The settings Host/Leave and hide checks also passed on this build.
