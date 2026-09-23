using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace MvzMp.Diagnostics;

/// <summary>Reports capture failures without changing the game's microphone selection or audio.</summary>
internal static class RecordingDiagnostics
{
    [HarmonyPatch(typeof(VoiceRecorder), nameof(VoiceRecorder.StartRecording))]
    private static class Started
    {
        private static void Postfix(VoiceRecorder __instance)
        {
            try
            {
                var device = (__instance._deviceName ?? "<none>").Replace('\r', ' ').Replace('\n', ' ');
                MelonLogger.Msg($"COOP RECORDING_STARTED guest={Game.NativeHooks.Runtime?.IsGuest ?? false} device={device} active={__instance._isRecording} clip={__instance._recordedClip != null}");
                if (!__instance._isRecording || __instance._recordedClip == null)
                    MelonLogger.Warning("Recording did not start. Check the game's microphone input and Windows microphone permissions.");
            }
            catch (Exception e) { MelonLogger.Warning($"Recording diagnostics unavailable: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(AdoptView), nameof(AdoptView.OnRecordingEnd))]
    private static class Finished
    {
        private static void Postfix(AudioClip? __0)
        {
            try
            {
                if (__0 == null) { MelonLogger.Warning("COOP RECORDING_FINISHED clip=null"); return; }
                var count = (long)__0.samples * __0.channels;
                if (count <= 0 || count > Game.VoiceStore.MaxSamples) return;
                var samples = new Il2CppStructArray<float>(count);
                if (!__0.GetData(samples, 0)) { MelonLogger.Warning("COOP RECORDING_FINISHED clip=unreadable"); return; }
                double peak = 0, squares = 0;
                foreach (var value in samples)
                {
                    if (!float.IsFinite(value)) continue;
                    peak = Math.Max(peak, Math.Abs(value)); squares += value * value;
                }
                MelonLogger.Msg(FormattableString.Invariant($"COOP RECORDING_FINISHED guest={Game.NativeHooks.Runtime?.IsGuest ?? false} samples={__0.samples} hz={__0.frequency} channels={__0.channels} peak={peak:F6} rms={Math.Sqrt(squares / count):F6}"));
                if (peak < 0.0001) MelonLogger.Warning("The recorded clip is silent or nearly silent before network transfer. Check the selected microphone and try previewing another recording.");
            }
            catch (Exception e) { MelonLogger.Warning($"Recording diagnostics unavailable: {e.Message}"); }
        }
    }
}
