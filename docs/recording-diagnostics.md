# Guest recording checks

The local `shared-zoo` scenario creates a five-second test tone on the guest and passes it through the native animal editor's recording completion. It verifies non-silent PCM and playback `AudioClip` data after host receipt, guest convergence, and a fresh download after reconnect. The `verify-save` scenario checks the native WAV reload too. These tests do not capture a physical microphone or establish that a speaker was audible.

The inspected game build's `VoiceRecorder.StartRecording` chooses `Microphone.devices[0]` and records at 44,100 Hz. MVZ-MP leaves that choice and recording path unchanged. An incorrect input device can therefore produce silence before any co-op transfer.

An opt-in `--mvzmp-smoke-microphone` argument, combined with the isolated `shared-zoo` scenario, replaces the synthetic tone with two seconds of actual guest microphone capture. Pass it to both peers so both validate non-silent audio rather than the known tone. It uses the native record button and recorder stop method, requires an advancing microphone sample position, and fails if the completed clip is silent. Someone must speak into the selected input during capture; a quiet room can legitimately fail. The flag has no effect outside the isolated smoke scenario. This mode records and saves the short clip in the disposable test directory, so use it deliberately. A passing amplitude check still does not establish audible speaker output.

For a physical microphone check on the guest:

1. Choose the intended microphone in Windows, then restart the game so its device list is fresh.
2. Join the host, adopt or edit an animal, record a short phrase, and preview it before completing the editor.
3. Complete the editor and play the animal on both computers. Rejoin and play it again.
4. If any stage is silent, retain both computers' `MelonLoader/Latest.log` files before restarting.

`COOP RECORDING_STARTED` reports the device actually chosen by the game and whether recording started. `COOP RECORDING_FINISHED` reports the completed clip's peak and RMS amplitude before transfer. `COOP VOICE_CACHED` reports the PCM hash and amplitude when cached on each peer. A silent recording warning means the clip was already silent or nearly silent before transfer. Matching hashes and levels across peers distinguish capture problems from transfer problems. Logs contain metadata and levels, not microphone samples.

## Observed hardware test

The 2026-09-23 isolated guest test successfully started the local headset microphone, observed its sample clock advance, and transferred nonzero audio with matching PCM hashes and levels to the host and back after reconnect. It captured quiet input rather than a verified spoken phrase, so this does not establish intelligibility or speaker output on either real player's machine.
