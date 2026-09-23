using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MvzMp.MvzMpMod), "MVZ-MP", "0.1.0", "Bars")]
[assembly: MelonGame("DefaultCompany", "MyVoiceZoo")]

namespace MvzMp;

public sealed class MvzMpMod : MelonMod
{
    private SteamLobby? _lobby;

    public override void OnInitializeMelon()
    {
        _lobby = new SteamLobby();
        MelonLogger.Msg("Loaded. F6 host, F7 invite, F8 leave.");
    }

    public override void OnUpdate()
    {
        if (_lobby is null)
            return;

        _lobby.TryInitialize();
        if (!_lobby.IsReady)
            return;

        if (Input.GetKeyDown(KeyCode.F6))
            _lobby.Host();
        if (Input.GetKeyDown(KeyCode.F7))
            _lobby.Invite();
        if (Input.GetKeyDown(KeyCode.F8))
            _lobby.Leave();
    }

    public override void OnDeinitializeMelon() => _lobby?.Dispose();
}
