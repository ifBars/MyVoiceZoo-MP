---
name: mvz-runtime-testing
description: Validate MVZ-MP builds and Steam lobby or shared-zoo behavior with phase-specific evidence and isolated saves.
---

# MVZ-MP Runtime Testing

Adapted from `ScheduleOne/.agents/skills/schedule-one-automated-testing`; use MyVoiceZoo-specific scenes, saves, and readiness signals discovered from this build.

For each smoke run, record: fresh build, intended DLL installed, MelonLoader mod loaded, game Steam initialized, lobby callback result, actual peer member count, packet handshake, and (when relevant) zoo state observed on both peers. A single-process launch cannot prove invite acceptance or co-op.

Use an isolated run when save behavior is under test. MyVoiceZoo writes `LocalLow/DefaultCompany/MyVoiceZoo/save.json` even during a GSE menu-only run, so back up and restore that user directory for every multi-process run. Keep separate installs and `steam_settings/configs.user.ini` identities for GSE, and never replace the live installation's `steam_api64.dll`. Capture logs before another launch overwrites `Latest.log`. Stop only processes started by the test. Never publish game-owned files, emulator DLLs, or test saves.

Use `scripts/Test-GseCoop.ps1` and `docs/testing-runner.md` for repeatable local GSE runs. Start with `-PlanOnly`; the real run retains unique test installs and evidence for inspection. The runner passes separate `--mvzmp-data-dir` paths and retains a shared LocalLow backup/restore guard. An optional `-Scenario` requires matching `PASS|<scenario>|...` lines from both peers. Use `scripts/Package-Mod.ps1` for a DLL-and-README-only ZIP. Report compile, local GSE, and two-account live Steam results separately.
