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
    private DateTime _next, _started = DateTime.UtcNow;
    private int _step, _animal = -1, _camp = -1;
    private bool _failed;
    private const string Name = "Coop test";
    public SmokeScenario(CoopRuntime runtime, SteamLobby lobby)
    {
        _runtime = runtime; _lobby = lobby;
        _enabled = Environment.GetCommandLineArgs().Contains("--mvzmp-smoke=shared-zoo") && Environment.GetCommandLineArgs().Any(a => a.StartsWith("--mvzmp-data-dir="));
        _host = Environment.GetCommandLineArgs().Contains("--mvzmp-host");
    }
    public void Tick()
    {
        if (!_enabled || _failed || _step == 99 || DateTime.UtcNow < _next) return;
        _next = DateTime.UtcNow.AddSeconds(1);
        try
        {
            if ((DateTime.UtcNow - _started).TotalSeconds > 100) throw new Exception($"scenario timed out at step {_step}; {_runtime.Status}");
            if (!_runtime.Zoo.Ready) return;
            if (_host)
            {
                if (_step == 0)
                {
                    Wallet.Instance.Init(1_000_000_000);
                    TutorialManager.Instance.SetIsTutorialCompleted(true);
                    _runtime.Zoo.Save(); _step = 1;
                    MelonLogger.Msg("SMOKE host seeded disposable zoo");
                }
                var state = _runtime.Zoo.Capture();
                var adopted = state.Animals.FirstOrDefault(a => a.Name == Name && a.Collected && a.Voice.Length == 64);
                if (adopted != null && Math.Abs(adopted.X - 1.25f) < .01f && state.WindIsland && state.Camps.Length > 0)
                {
                    MelonLogger.Msg($"PASS|shared-zoo|host animal={adopted.Id} voice={adopted.Voice} position={adopted.X} camp={string.Join(',', state.Camps)} save={SaveLoadSystem._path}");
                    _step = 99;
                }
                return;
            }
            if (_step == 0)
            {
                if (!_runtime.Synchronized) return;
                _animal = _runtime.Zoo.Capture().Animals.First(a => !a.Collected).Id;
                _runtime.BeginEdit(_animal, true); _step = 1;
                MelonLogger.Msg($"SMOKE client adopt requested {_animal}");
            }
            else if (_step == 1)
            {
                if (_runtime.EditingAnimal != _animal) return;
                var view = GameManager.Instance._uiManager._adoptView;
                view.OnNameInputValueChanged(Name);
                var clip = AudioClip.Create("co-op smoke recording", 48000, 1, 48000, false);
                var samples = new Il2CppStructArray<float>(48000);
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
                _lobby.Leave(); _step = 6;
            }
            else if (_step == 6)
            {
                if (_runtime.IsGuest || _runtime.Zoo.Animal(_animal)!.IsCollected) throw new Exception("Guest's original zoo was not restored after leaving.");
                MelonLogger.Msg("PASS|shared-zoo|client state converged and solo zoo restored"); _step = 99;
            }
        }
        catch (Exception e) { _failed = true; MelonLogger.Error($"FAIL|shared-zoo|{e}"); }
    }
}
