using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MvzMp.Game;
using MvzMp.Presentation;
using MvzMp.State;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace MvzMp.Diagnostics;

/// <summary>Opt-in character checks in disposable two-player saves.</summary>
internal static class CharacterSmoke
{
    private static readonly CostumeID[] Costumes = Enum.GetValues<CostumeID>();
    private static bool Enabled => SaveIsolation.IsIsolated && Environment.GetCommandLineArgs().Contains("--mvzmp-smoke-characters");
    private static bool _prepared, _subscribed, _guestReady, _checked, _acknowledged, _done, _failed;
    private static bool _sawRun, _driving;
    private static bool _positioned;
    private static int _phase = -1;
    private static float _phaseAt, _sendAt;
    private static string? _screenshot;
    private static string? _runningScreenshot;
    private static bool Host => Environment.GetCommandLineArgs().Contains("--mvzmp-host");
    public static bool Completed => !Enabled || _done;
    internal static void Prepare() => _prepared = true;

    public static void Tick(SteamLobby lobby)
    {
        if (!Enabled || _done || _failed) return;
        if (!_subscribed)
        {
            _subscribed = true;
            lobby.MessageReceived += (sender, bytes) => Receive(lobby, sender, bytes);
        }
        if (!_prepared || !lobby.IsInLobby || lobby.Members.Count < 2 ||
            (!lobby.IsHost && NativeHooks.Runtime?.Synchronized != true) || GameManager.Instance?._player == null) return;
        try
        {
            if (!CharacterAppearance.Ready) throw new InvalidOperationException("Male artwork is not ready.");
            if (Environment.GetCommandLineArgs().Contains("-nographics")) throw new InvalidOperationException("Character smoke requires a rendered game.");
            var now = Time.realtimeSinceStartup;
            if (_phase < 0)
            {
                if (lobby.IsHost)
                {
                    if (_guestReady) BeginPhase(lobby, 0);
                }
                else if (now >= _sendAt)
                {
                    if (!_positioned)
                    {
                        // The disposable fixture starts beside the tent on open grass;
                        // this small rightward offset separates Lucy from its recorded sheep.
                        GameManager.Instance._player.transform.position += new Vector3(1.5f, 0, 0);
                        _positioned = true;
                    }
                    if (!CharacterAppearance.Select(0)) throw new InvalidOperationException("Could not select Lucy.");
                    CostumeManager.Instance.EquipCostume(Costumes[0]);
                    Send(lobby, lobby.HostSteamId, "ready", -1);
                    _sendAt = now + .5f;
                }
                return;
            }
            if (now - _phaseAt > 10) throw new InvalidOperationException($"Character phase {_phase} timed out.");
            ObserveRun(lobby);
            if (lobby.IsHost && now >= _sendAt)
            {
                foreach (var peer in lobby.Members.Where(p => p != lobby.LocalSteamId)) Send(lobby, peer, "phase", _phase);
                _sendAt = now + .5f;
            }
            if (!_checked && now - _phaseAt >= 2)
            {
                Verify(lobby);
                _screenshot = Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, $"char-{_phase}.png");
                ScreenCapture.CaptureScreenshot(_screenshot);
                _checked = true;
            }
            if (!_checked || !ScreenshotSaved(_screenshot) || !ScreenshotSaved(_runningScreenshot)) return;
            if (!lobby.IsHost)
            {
                if (now >= _sendAt) { Send(lobby, lobby.HostSteamId, "ack", _phase); _sendAt = now + .5f; }
                return;
            }
            if (!_acknowledged || now - _phaseAt < 3) return;
            MelonLogger.Msg($"PASS|character-phase|phase={_phase} host and guest graphics checked; screenshots saved");
            if (_phase < Costumes.Length + 1) BeginPhase(lobby, _phase + 1);
            else
            {
                foreach (var peer in lobby.Members.Where(p => p != lobby.LocalSteamId)) Send(lobby, peer, "complete", _phase);
                Finish();
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void BeginPhase(SteamLobby lobby, int phase)
    {
        _phase = phase;
        _phaseAt = Time.realtimeSinceStartup;
        _sendAt = 0;
        _checked = _acknowledged = false;
        _sawRun = false;
        _screenshot = _runningScreenshot = null;
        if (!lobby.IsHost) return;
        var character = phase == Costumes.Length ? 0 : 1;
        if (!CharacterAppearance.Select(character)) throw new InvalidOperationException($"Could not select character {character}.");
        var costume = Costumes[Math.Min(phase, Costumes.Length - 1)];
        PrepareCostumeFixture(costume);
        CostumeManager.Instance.EquipCostume(costume);
        CharacterAppearance.Tick();
    }

    private static void PrepareCostumeFixture(CostumeID costume)
    {
        // EquipCostume applies the required animal prefab's voice to every animal.
        // Each disposable fixture must therefore contain that collected, recorded animal.
        // Host changes are distributed by the existing snapshot/recording transport.
        var runtime = NativeHooks.Runtime ?? throw new InvalidOperationException("Co-op runtime is unavailable.");
        var required = DataManager.Instance.GetCostumeData(costume).conditionAnimalID;
        if (required > 0)
        {
            var animal = runtime.Zoo.Animal(required) ?? throw new InvalidOperationException($"Costume {costume} requires missing animal {required}.");
            if (animal.Voice == null)
            {
                var recorded = runtime.Zoo.Capture().Animals.FirstOrDefault(a => a.Collected && a.Voice.Length == 64)
                    ?? throw new InvalidOperationException("The shared-zoo recording fixture is missing.");
                var clip = runtime.Zoo.Animal(recorded.Id)?.Voice ?? throw new InvalidOperationException("The shared recording has no native clip.");
                animal.SetVoice(clip, false);
            }
            var controller = GameManager.Instance._animalPrefabController;
            var existed = controller._spawnedAnimalPrefabDict.TryGetValue(required, out var prefab) && prefab != null;
            if (!existed) controller._spawnedAnimalPrefabDict.Remove(required);
            if (!animal.IsCollected)
            {
                if (existed) { animal.SetIsCollected(true); prefab!.gameObject.SetActive(true); }
                else AnimalManager.Instance.AnimalCollectStateChange(required, true);
            }
            if (!controller._spawnedAnimalPrefabDict.TryGetValue(required, out prefab) || prefab == null)
                controller.SpawnAnimalPrefab(animal);
            if (!controller._spawnedAnimalPrefabDict.TryGetValue(required, out prefab) || prefab == null)
                throw new InvalidOperationException($"Costume {costume} prerequisite prefab did not spawn.");
        }
        if (!CostumeManager.Instance.IsBuyCostume(costume))
            CostumeManager.Instance.CostumeBuyStateDict[costume] = true;
        MelonLogger.Msg($"CHARACTER FIXTURE costume={costume} requiredAnimal={required} recorded prerequisite ready");
    }

    private static void Verify(SteamLobby lobby)
    {
        if (!_sawRun) throw new InvalidOperationException("Host running animation was not observed for this costume.");
        var hostCharacter = _phase == Costumes.Length ? 0 : 1;
        var hostCostume = (int)Costumes[Math.Min(_phase, Costumes.Length - 1)];
        var player = GameManager.Instance._player;
        var localCharacter = lobby.IsHost ? hostCharacter : 0;
        var localCostume = lobby.IsHost ? hostCostume : (int)Costumes[0];
        var expected = CharacterAppearance.GetLibrary(localCharacter, localCostume);
        if (player._spriteLibrary.spriteLibraryAsset != expected || CharacterAppearance.Selected != localCharacter ||
            (int)CostumeManager.Instance.EquippedCostumeID != localCostume)
            throw new InvalidOperationException("Local character or costume changed unexpectedly.");
        if (!player.spriteRenderer.enabled || !player.spriteRenderer.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Local character sprite is not rendered.");
        VerifySprite(player.spriteRenderer.sprite, expected!, localCharacter);
        var peer = lobby.Members.First(id => id != lobby.LocalSteamId);
        var replica = GameObject.Find($"MVZ-MP peer {peer}") ?? throw new InvalidOperationException("Remote player is missing.");
        var remoteCharacter = lobby.IsHost ? 0 : hostCharacter;
        var remoteCostume = lobby.IsHost ? (int)Costumes[0] : hostCostume;
        var remoteExpected = CharacterAppearance.GetLibrary(remoteCharacter, remoteCostume);
        var library = replica.GetComponentInChildren<SpriteLibrary>(true);
        if (library == null || library.spriteLibraryAsset != remoteExpected) throw new InvalidOperationException("Remote character or costume library does not match.");
        var rendered = replica.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r =>
            r.sprite != null && Contains(remoteExpected!, r.sprite));
        if (rendered == null || !rendered.enabled || !rendered.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Remote character sprite is not rendered.");
        VerifySprite(rendered.sprite, remoteExpected!, remoteCharacter);
        MelonLogger.Msg($"CHARACTER CHECK phase={_phase} local={player.spriteRenderer.sprite.name} remote={rendered.sprite.name}");
    }

    private static bool ScreenshotSaved(string? path) => path != null && File.Exists(path) && new FileInfo(path).Length > 0;

    private static void ObserveRun(SteamLobby lobby)
    {
        if (_sawRun) return;
        var character = _phase == Costumes.Length ? 0 : 1;
        var costume = (int)Costumes[Math.Min(_phase, Costumes.Length - 1)];
        var library = CharacterAppearance.GetLibrary(character, costume)!;
        var sprite = lobby.IsHost ? GameManager.Instance._player.spriteRenderer.sprite :
            GameObject.Find($"MVZ-MP peer {lobby.HostSteamId}")?.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => renderer.sprite != null && Contains(library, renderer.sprite))?.sprite;
        if (sprite == null || !sprite.name.Contains("Run", StringComparison.Ordinal) || !Contains(library, sprite)) return;
        VerifySprite(sprite, library, character);
        _sawRun = true;
        _runningScreenshot = Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, $"char-{_phase}-running.png");
        ScreenCapture.CaptureScreenshot(_runningScreenshot);
        MelonLogger.Msg($"CHARACTER RUN phase={_phase} sprite={sprite.name}");
    }

    [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
    private static class Drive
    {
        private static void Prefix(Player __instance) => ApplyDrive(__instance);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    private static class DriveAnimation
    {
        // Native Update rereads keyboard axes and overwrites both _input and isRun.
        // Reapply after that method so Unity's animation update samples the probe input.
        private static void Postfix(Player __instance) => ApplyDrive(__instance);
    }

    private static void ApplyDrive(Player player)
    {
        if (!Enabled || !Host || _done || _failed || _phase < 0 || player != GameManager.Instance?._player) return;
        var elapsed = Time.realtimeSinceStartup - _phaseAt;
        if (elapsed < 1.2f)
        {
            _driving = true;
            player._input = (elapsed < .6f ? Vector2.right : Vector2.left) * .15f;
            player.animator.SetBool("isRun", true);
        }
        else StopMovement();
    }

    private static void StopMovement()
    {
        if (!_driving) return;
        _driving = false;
        var player = GameManager.Instance?._player;
        if (player == null) return;
        player._input = Vector2.zero;
        player.animator.SetBool("isRun", false);
    }

    private static bool Contains(SpriteLibraryAsset library, Sprite sprite)
    {
        foreach (var category in library.categories)
        foreach (var entry in category.categoryList)
            if (entry.sprite == sprite) return true;
        return false;
    }

    private static void VerifySprite(Sprite? sprite, SpriteLibraryAsset library, int character)
    {
        if (sprite == null || !Contains(library, sprite) || sprite.name.StartsWith("Male_", StringComparison.Ordinal) != (character == 1))
            throw new InvalidOperationException("Rendered sprite does not belong to the expected character.");
    }

    private static void Receive(SteamLobby lobby, ulong sender, byte[] bytes)
    {
        if (!Enabled || _done || _failed || !lobby.IsInLobby || sender == lobby.LocalSteamId || !lobby.Members.Contains(sender)) return;
        try
        {
            var message = JsonSerializer.Deserialize<WireMessage>(bytes);
            if (message?.Kind != "character-smoke") return;
            if (lobby.IsHost)
            {
                if (message.Text == "ready") _guestReady = true;
                if (message.Text == "ack" && message.AnimalId == _phase) _acknowledged = true;
            }
            else if (_prepared && sender == lobby.HostSteamId)
            {
                if (message.Text == "phase" && message.AnimalId == _phase + 1 && message.AnimalId <= Costumes.Length + 1)
                    BeginPhase(lobby, message.AnimalId);
                if (message.Text == "complete" && _phase == Costumes.Length + 1 && _checked && message.AnimalId == _phase)
                    Finish();
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Send(SteamLobby lobby, ulong peer, string text, int phase) =>
        lobby.Send(peer, JsonSerializer.SerializeToUtf8Bytes(new WireMessage { Kind = "character-smoke", Text = text, AnimalId = phase }));

    private static void Finish()
    {
        _done = true;
        StopMovement();
        MelonLogger.Msg("PASS|character-smoke|all costumes rendered locally and remotely; Lucy restore and male reselection verified; screenshots saved");
    }

    private static void Fail(Exception exception)
    {
        _failed = true;
        StopMovement();
        MelonLogger.Error($"FAIL|character-smoke|{exception}");
    }
}
