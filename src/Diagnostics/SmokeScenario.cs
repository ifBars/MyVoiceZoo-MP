using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using MvzMp.State;
using UnityEngine;

namespace MvzMp.Diagnostics;

/// <summary>Opt-in disposable-save scenario, never enabled during normal play.</summary>
internal sealed class SmokeScenario
{
    private readonly CoopRuntime _runtime;
    private readonly SteamLobby _lobby;
    private readonly bool _enabled;
    private readonly bool _host;
    private readonly bool _verifySave;
    private DateTime _next, _started = DateTime.UtcNow;
    private int _step, _animal = -1, _camp = -1, _costume = -1;
    private bool _failed;
    private ulong _originalLobby;
    private string? _soloFileHash;
    private const string Name = "Coop test";
    public SmokeScenario(CoopRuntime runtime, SteamLobby lobby)
    {
        _runtime = runtime; _lobby = lobby;
        _enabled = Environment.GetCommandLineArgs().Contains("--mvzmp-smoke=shared-zoo") && MvzMp.Game.SaveIsolation.IsIsolated;
        _verifySave = Environment.GetCommandLineArgs().Contains("--mvzmp-smoke=verify-save") && MvzMp.Game.SaveIsolation.IsIsolated;
        _enabled |= _verifySave;
        _host = Environment.GetCommandLineArgs().Contains("--mvzmp-host");
        if (_enabled && _host) lobby.MessageReceived += (peer, bytes) =>
        {
            try
            {
                var message = System.Text.Json.JsonSerializer.Deserialize<WireMessage>(bytes);
                if (_step == 99 && message?.Kind == "smoke-end")
                {
                    lobby.Leave(); MelonLogger.Msg("PASS|host-departure|host left after guest rejoined");
                }
            } catch { /* The normal receiver owns invalid-packet diagnostics. */ }
        };
    }
    private static void CaptureScreenshot()
    {
        if (!Environment.GetCommandLineArgs().Contains("-nographics")) ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, Environment.GetCommandLineArgs().Contains("--mvzmp-host") ? $"coop-{DateTime.UtcNow:HHmmssfff}.png" : "coop.png"));
    }
    public void Tick()
    {
        if (_enabled && _host && _step == 99 && _lobby.Members.Count > 1 && DateTime.UtcNow >= _next)
        {
            CaptureScreenshot(); _next = DateTime.UtcNow.AddSeconds(2);
        }
        if (!_enabled || _failed || _step == 99 || DateTime.UtcNow < _next) return;
        _next = DateTime.UtcNow.AddSeconds(1);
        try
        {
            if ((DateTime.UtcNow - _started).TotalSeconds > 100) throw new Exception($"scenario timed out at step {_step}; {_runtime.Status}");
            if (!_runtime.Zoo.Ready) return;
            if (_verifySave)
            {
                var loaded = _runtime.Zoo.Capture();
                var savedAnimal = loaded.Animals.FirstOrDefault(a => a.Name == Name && a.Collected);
                if (savedAnimal == null || savedAnimal.Voice.Length != 64 || !loaded.WindIsland || loaded.Camps.Length == 0 || Math.Abs(savedAnimal.X - 1.25f) > .01f) throw new Exception("Saved shared zoo did not reload completely.");
                var clip = _runtime.Zoo.Animal(savedAnimal.Id)!.Voice;
                if (clip == null || clip.samples != 240000 || clip.frequency != 48000) throw new Exception("Saved recording dimensions differ.");
                MelonLogger.Msg($"PASS|verify-save|native save and 5 second WAV reloaded animal={savedAnimal.Id}"); _step = 99; return;
            }
            if (_host)
            {
                if (_step == 0)
                {
                    Wallet.Instance.Init(1_000_000_000);
                    var costume = Enum.GetValues<CostumeID>().First(c => !CostumeManager.Instance.IsBuyCostume(c));
                    var required = DataManager.Instance.GetCostumeData(costume).conditionAnimalID;
                    if (_runtime.Zoo.Animal(required) is { IsCollected: false }) AnimalManager.Instance.AnimalCollectStateChange(required, true);
                    TutorialManager.Instance.SetIsTutorialCompleted(true);
                    _runtime.Zoo.Save(); _step = 1;
                    MelonLogger.Msg("SMOKE host seeded disposable zoo");
                }
                var state = _runtime.Zoo.Capture();
                var adopted = state.Animals.FirstOrDefault(a => a.Name == Name && a.Collected && a.Voice.Length == 64);
                if (adopted != null && Math.Abs(adopted.X - 1.25f) < .01f && state.WindIsland && state.Camps.Length > 0)
                {
                    CaptureScreenshot();
                    MelonLogger.Msg($"PASS|shared-zoo|host animal={adopted.Id} voice={adopted.Voice} position={adopted.X} camp={string.Join(',', state.Camps)} save={SaveLoadSystem._path}");
                    _step = 99;
                }
                return;
            }
            if (_step == 0 && !_lobby.IsInLobby)
            {
                var connect = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--mvzmp-smoke-lobby="));
                if (connect == null) return;
                Wallet.Instance.Init(12345); _runtime.Zoo.Save();
                _soloFileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(SaveLoadSystem._path)));
                _originalLobby = ulong.Parse(connect.Split('=')[1]);
                Il2CppSteamworks.SteamMatchmaking.JoinLobby(new Il2CppSteamworks.CSteamID(_originalLobby)); _step = -1; return;
            }
            if (_step is 0 or -1)
            {
                if (!_runtime.Synchronized) return;
                _originalLobby = _lobby.LobbyId;
                GameManager.Instance._player.transform.position += new Vector3(2, 0, 0);
                _animal = _runtime.Zoo.Capture().Animals.First(a => !a.Collected).Id;
                _runtime.BeginEdit(_animal, true); _step = 1;
                MelonLogger.Msg($"SMOKE client adopt requested {_animal}");
            }
            else if (_step == 1)
            {
                if (_runtime.EditingAnimal != _animal) return;
                var view = GameManager.Instance._uiManager._adoptView;
                if (view._animal == null) return; // The native adoption reveal opens the editor after its animation.
                view.OnNameInputValueChanged(Name);
                var clip = AudioClip.Create("co-op smoke recording", 240000, 1, 48000, false);
                var samples = new Il2CppStructArray<float>(240000);
                for (var i = 0; i < samples.Length; i++) samples[i] = (float)Math.Sin(i * Math.PI * 2 * 440 / 48000) * .2f;
                clip.SetData(samples, 0);
                view.OnRecordingEnd(clip);
                view.OnClickCompleteButton(); _step = 2;
                MelonLogger.Msg("SMOKE client native recording complete");
            }
            else if (_step == 2)
            {
                var animal = _runtime.Zoo.Animal(_animal)!;
                if (animal.Name != Name || animal.Voice == null || _runtime.Status.Contains("Waiting")) return;
                _runtime.Submit(new ZooCommand { Kind = "move", AnimalId = _animal, X = 1.25f, Y = 0, Z = 0, SortingOrder = 4 }); _step = 3;
            }
            else if (_step == 3)
            {
                _runtime.Purchase("area", 0); _step = 4;
            }
            else if (_step == 4)
            {
                _camp = (int)Enum.GetValues<CampType>().First(c => !CampManager.Instance.GetCampState(c));
                _runtime.Purchase("camp", _camp); _step = 5;
            }
            else if (_step == 5)
            {
                var state = _runtime.Zoo.Capture();
                var animal = state.Animals.First(a => a.Id == _animal);
                if (!state.WindIsland || !state.Camps.Contains(_camp) || animal.Name != Name || animal.Voice.Length != 64 || Math.Abs(animal.X - 1.25f) > .01f) return;
                MelonLogger.Msg($"SMOKE client converged animal={_animal} voice={animal.Voice}");
                if (_soloFileHash != null && _soloFileHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(SaveLoadSystem._path)))) throw new Exception("Guest save changed during co-op.");
                _costume = (int)Enum.GetValues<CostumeID>().First(c => !CostumeManager.Instance.IsBuyCostume(c) && CostumeManager.Instance.CanBuyCostumeCondition(c));
                _runtime.Purchase("costume", _costume); _step = 50;
            }
            else if (_step == 50)
            {
                if (!CostumeManager.Instance.IsBuyCostume((CostumeID)_costume)) return;
                CostumeManager.Instance.EquipCostume((CostumeID)_costume);
                _runtime.BeginEdit(_animal, false); _step = 52;
            }
            else if (_step == 52)
            {
                var view = GameManager.Instance._uiManager._adoptView;
                if (_runtime.EditingAnimal != _animal || view._animal == null) return;
                view.OnNameInputValueChanged(Name); view.OnClickCompleteButton(); _step = 53;
            }
            else if (_step == 53)
            {
                if (_runtime.Status.Contains("Waiting") || _runtime.EditingAnimal >= 0) return;
                MelonLogger.Msg($"SMOKE costume purchase and native edit verified costume={_costume}");
                CaptureScreenshot(); _step = 51;
            }
            else if (_step == 51)
            {
                _lobby.Leave(); _step = 6;
            }
            else if (_step == 6)
            {
                if (_runtime.IsGuest || _runtime.Zoo.Animal(_animal)!.IsCollected || (_soloFileHash != null && Wallet.Instance.CurrentGold != 12345)) throw new Exception("Guest's original zoo was not restored after leaving.");
                _runtime.Voices.Clear(); // Force a fresh host-to-guest recording transfer on rejoin.
                Il2CppSteamworks.SteamMatchmaking.JoinLobby(new Il2CppSteamworks.CSteamID(_originalLobby)); _step = 7;
            }
            else if (_step == 7)
            {
                if (!_runtime.Synchronized || _runtime.Zoo.Animal(_animal)!.Name != Name) return;
                _lobby.Send(_lobby.HostSteamId, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new WireMessage { Kind = "smoke-end" })); _step = 8;
            }
            else if (_step == 8)
            {
                if (_lobby.IsInLobby) return;
                if (_runtime.IsGuest || _runtime.Zoo.Animal(_animal)!.IsCollected) throw new Exception("Solo restore failed after reconnect.");
                MelonLogger.Msg("PASS|shared-zoo|client state converged, reconnected, and solo zoo restored after host departure"); _step = 99;
            }
        }
        catch (Exception e) { _failed = true; MelonLogger.Error($"FAIL|shared-zoo|{e}"); }
    }
}
