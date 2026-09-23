# Local GSE lobby smoke test

Use this only for local testing with a user-owned MyVoiceZoo installation and a known GSE x64 `steam_api64.dll`. GSE is not part of this repository or release package. The two-process lobby test passed on 2026-09-23 using a GSE Client API build with file version `08.33.09.23` and SHA-256 `EF32F9BB1FEF9E9B58F3EA06F88B2EA1E206C861A4D0431D287E537C59A1A391`.

1. Verify MyVoiceZoo is closed. Back up the entire `%USERPROFILE%\AppData\LocalLow\DefaultCompany\MyVoiceZoo` directory, including whether `save.json` existed. A menu-only launch can create it.
2. Make two fresh, isolated copies of the installed game outside the repository. Put `MVZ.MP.dll` in each copy's `Mods` folder. Do not replace the original game's Steam DLL.
3. In each copy, replace only `MyVoiceZoo_Data\Plugins\x86_64\steam_api64.dll` with the test GSE DLL. Keep a copy of the original in the disposable install. Write `4015530` to `steam_appid.txt` beside that DLL and at the game root.
4. Create `MyVoiceZoo_Data\Plugins\x86_64\steam_settings\configs.user.ini` for each process. Use distinct valid Steam IDs, for example:

   ```ini
   [user::general]
   account_name=MVZMP-host
   account_steamid=76561198000040001
   language=english
   ```

   Use a different name and `account_steamid=76561198000040002` for the client.
5. Start the host with `--mvzmp-host -batchmode -nographics`. Read its current MelonLoader log for `Created lobby <id>` and `Joined lobby <id> ... role=host`.
6. Start the client with `+connect_lobby <id> -batchmode -nographics`. Require distinct Steam IDs, the same lobby ID, `members=2`, and a handshake message from the opposite peer in **both** logs.
7. Stop only those two processes. Preserve the short result summary, restore the backed-up `LocalLow` directory, and remove disposable installs and test settings.

This proves Steamworks.NET lobby and chat transport through GSE. It does not prove real Steam overlay invites or zoo state synchronization.
