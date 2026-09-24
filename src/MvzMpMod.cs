using MelonLoader;
using MvzMp.Game;
using MvzMp.Presentation;
using UnityEngine;

[assembly: MelonInfo(typeof(MvzMp.MvzMpMod), "MyVoiceZoo Together", "1.0.0", "Bars")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0")]
[assembly: MelonGame("DefaultCompany", "MyVoiceZoo")]

namespace MvzMp;

public sealed class MvzMpMod : MelonMod
{
    private SteamLobby? _lobby;
    private CoopRuntime? _runtime;
    private CoopPanel? _panel;
    private readonly StartupHint _startupHint = new();
    private DateTime _lastError;
    private MvzMp.Diagnostics.SmokeScenario? _smoke;

    public override void OnInitializeMelon()
    {
        SaveIsolation.Initialize();
        _lobby = new SteamLobby();
        _runtime = new CoopRuntime(_lobby);
        _smoke = new MvzMp.Diagnostics.SmokeScenario(_runtime, _lobby);
        NativeHooks.Runtime = _runtime;
        SaveIsolation.IsGuest = () => _runtime.IsGuest;
        _panel = new CoopPanel(() => _runtime.Status, () => _lobby.IsInLobby, _lobby.Host, _lobby.Invite, _lobby.Leave);
        MelonLogger.Msg("Loaded. F6 host, F7 invite, F8 leave.");
    }

    public override void OnUpdate()
    {
        if (_lobby is null) return;
        try
        {
            CharacterAppearance.Tick();
            CharacterCloset.Tick();
            _lobby.TryInitialize();
            _lobby.Tick();
            _runtime!.Tick();
            _smoke!.Tick();
            MvzMp.Diagnostics.CharacterSmoke.Tick(_lobby);
            MvzMp.Diagnostics.CharacterProbe.Tick();
            MvzMp.Diagnostics.MovementProbe.Tick(_lobby);
            _startupHint.Tick();
            _panel!.Tick();
            if (!_lobby.IsReady) return;
            if (Input.GetKeyDown(KeyCode.F6)) _lobby.Host();
            if (Input.GetKeyDown(KeyCode.F7)) _lobby.Invite();
            if (Input.GetKeyDown(KeyCode.F8)) _lobby.Leave();
        }
        catch (Exception e)
        {
            if ((DateTime.UtcNow - _lastError).TotalSeconds > 5) { MelonLogger.Error(e); _lastError = DateTime.UtcNow; }
        }
    }

    public override void OnDeinitializeMelon()
    {
        _runtime?.Dispose(); CharacterCloset.Dispose(); CharacterAppearance.Dispose(); _startupHint.Dispose(); _panel?.Dispose(); _lobby?.Dispose(); NativeHooks.Runtime = null;
    }
}


