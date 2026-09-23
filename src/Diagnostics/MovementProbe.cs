using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MvzMp.Game;
using UnityEngine;

namespace MvzMp.Diagnostics;

/// <summary>Disposable-save native movement probe. Physics stays owned by the real player.</summary>
internal static class MovementProbe
{
    private static bool _started, _done;
    private static float _start;
    private static int _frame;
    private static Vector3 _origin, _previous;
    private static bool _moved;
    private static bool Enabled => SaveIsolation.IsIsolated && Environment.GetCommandLineArgs().Contains("--mvzmp-smoke-motion");
    internal static bool Completed => !Enabled || _done;
    private static bool Host => Environment.GetCommandLineArgs().Contains("--mvzmp-host");

    internal static void Tick(SteamLobby lobby)
    {
        if (!Enabled || _done || !lobby.IsInLobby || lobby.Members.Count < 2 || GameManager.Instance?._player == null) return;
        var player = GameManager.Instance._player;
        if (!_started)
        {
            if (NativeHooks.Runtime?.Zoo.Capture().Animals.Any(a => a.Name == "Coop test" && a.Voice.Length == 64) != true) return;
            _started = true; _start = Time.realtimeSinceStartup; _origin = _previous = player.transform.position;
            MelonLogger.Msg($"SMOKE motion native origin={_origin}");
        }
        var elapsed = Time.realtimeSinceStartup - _start;
        if (elapsed >= 10)
        {
            _done = true;
            if (Host) MelonLogger.Msg(_moved ? "PASS|native-motion|native physics moved the host along the probe route" : "FAIL|native-motion|host did not move");
            return;
        }
        if (elapsed < _frame + 1) return;
        _frame++;
        var position = player.transform.position;
        if (Host)
        {
            _moved |= (position - _origin).sqrMagnitude > .1f;
            MelonLogger.Msg($"SMOKE motion second={_frame} position={position} displacement={(position - _previous).magnitude:F3}");
            if (_moved && _frame is > 1 and < 5 && (position - _previous).sqrMagnitude < .001f)
                MelonLogger.Msg("PASS|native-collision-stop|upward native movement blocked while input held");
        }
        _previous = position;
        if (!Environment.GetCommandLineArgs().Contains("-nographics"))
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, $"motion-{_frame:00}.png"));
    }

    [HarmonyPatch(typeof(Player), nameof(Player.FixedUpdate))]
    private static class Drive
    {
        private static void Prefix(Player __instance)
        {
            if (!Enabled || !Host || !_started || _done) return;
            var elapsed = Time.realtimeSinceStartup - _start;
            var direction = elapsed < 4 ? Vector2.up : elapsed < 6 ? Vector2.right : elapsed < 8 ? Vector2.down : Vector2.left;
            __instance._input = direction * .15f;
            __instance.animator.SetBool("isRun", true);
        }
    }
}
