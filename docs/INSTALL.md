# MVZ-MP 0.2.2

Steam co-op for MyVoiceZoo. Up to four players share the host's animals, recordings, placement, gold, areas, camps, and purchased costumes.

## Install

Install MelonLoader 0.7.3 x64 for MyVoiceZoo and launch once. Put the included MVZ.MP.dll in the game's Mods directory. Every player needs the same mod and game build.

## Play

Open the game's settings menu and choose Host in its co-op section, then Invite to use Steam's invite dialog. Friends join by accepting the invite. F6 hosts, F7 invites, and F8 leaves.

Use the normal game interfaces to adopt or edit animals, record their voices, buy upgrades, and move them. The host approves shared purchases. An animal is reserved while another player edits it. Each player moves independently and can equip a different costume.

Shared progress belongs to the host. Leaving restores the guest's own zoo; guests do not write shared progress into their solo save. If the host leaves, the session ends. There is no host migration or live microphone chat.

## Compatibility

Built for MyVoiceZoo's Unity 2022.3.62f2 IL2CPP build with MelonLoader 0.7.3. The mod rejects mismatched game binaries or mod protocols. Steam must be initialized by the game.

This package contains no game assemblies, generated wrappers, saves, recordings, or Steam emulator files. Remove Mods/MVZ.MP.dll to uninstall the mod.

Validated with two isolated GSE game processes, including rendered player/nameplate checks, shared purchases/recordings, reconnect, host departure, and guest save preservation. The user has also reported a successful two-account playtest of 0.2.0, with movement/depth issues addressed in this update. The new build still needs a repeat real-account playtest; four-player and long-session testing remain unverified.

Version 0.2.2 uses protocol 4. Both players must update together. Movement uses a separate latest-pose channel and buffered playback; native sprite pivot sorting is preserved. Microphone diagnostics distinguish capture silence from transfer failure.

On startup, a four-second fading hint points you to Settings once the splash, loading screen, and microphone guide have closed. The settings frame is wider, and Quit Game is centered below both columns.
