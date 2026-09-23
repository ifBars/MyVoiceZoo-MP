using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MvzMp.Presentation;

/// <summary>A single, non-interactive hint once the game's normal controls are available.</summary>
internal sealed class StartupHint : IDisposable
{
    private GameObject? _root;
    private CanvasGroup? _group;
    private float _readyAt = -1, _shownAt;
    private bool _finished, _captured, _capturedFade;
    private readonly bool _probe = Environment.GetCommandLineArgs().Contains("--mvzmp-smoke-settings");

    public void Tick()
    {
        if (_finished) return;
        var ui = GameManager.Instance?._uiManager;
        if (_root == null && (GameManager.Instance?._loadCompleted != true ||
            !UnityEngine.Rendering.SplashScreen.isFinished ||
            (GameManager.Instance._loadingScreen != null && GameManager.Instance._loadingScreen.gameObject.activeInHierarchy) ||
            (GameManager.Instance._micGuideScreen != null && GameManager.Instance._micGuideScreen.gameObject.activeInHierarchy) ||
            ui?._settingButton == null || !ui._settingButton.gameObject.activeInHierarchy ||
            (ui._allUIBlock != null && ui._allUIBlock.activeInHierarchy)))
        {
            _readyAt = -1;
            return;
        }
        var now = Time.realtimeSinceStartup;
        if (_readyAt < 0) _readyAt = now;
        if (_root == null)
        {
            if (now - _readyAt < 1f) return;
            var canvas = ui!._settingButton.GetComponentInParent<Canvas>();
            var template = ui._settingView?.transform.Find("Contents/Popup/QuitGameButton");
            var nativeText = template?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (canvas == null || nativeText == null) return;
            var types = new Il2CppReferenceArray<Il2CppSystem.Type>(1);
            types[0] = Il2CppType.Of<RectTransform>();
            _root = new GameObject("MVZ-MP startup hint", types);
            _root.transform.SetParent(canvas.rootCanvas.transform, false);
            var rect = _root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -28);
            rect.sizeDelta = new Vector2(720, 68);
            var image = _root.AddComponent<Image>();
            var nativeImage = template!.GetComponent<Image>();
            image.sprite = nativeImage.sprite;
            image.type = nativeImage.type;
            image.color = new Color(1, 1, 1, .95f);
            image.raycastTarget = false;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0;
            var label = new GameObject("Message", types);
            label.transform.SetParent(_root.transform, false);
            var text = label.AddComponent<TextMeshProUGUI>();
            text.font = nativeText.font;
            text.fontSharedMaterial = nativeText.fontSharedMaterial;
            text.fontSize = 26;
            text.color = nativeText.color;
            text.text = "Open Settings to host a co-op lobby";
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(16, 4);
            text.rectTransform.offsetMax = new Vector2(-16, -4);
            _shownAt = now;
            if (Probe) MelonLoader.MelonLogger.Msg("SMOKE startup hint shown after splash, loading, and microphone guide finished");
        }
        var elapsed = now - _shownAt;
        _group!.alpha = Mathf.Min(Mathf.Clamp01(elapsed / .25f), Mathf.Clamp01(4f - elapsed));
        if (Probe && !_captured && elapsed >= 1)
        {
            Capture("startup-hint.png"); _captured = true;
        }
        if (Probe && !_capturedFade && elapsed >= 3.5f)
        {
            Capture("startup-hint-fading.png"); _capturedFade = true;
            MelonLoader.MelonLogger.Msg($"SMOKE startup hint fading alpha={_group.alpha:F2}");
        }
        if (elapsed >= 4f)
        {
            Dispose();
            if (Probe) MelonLoader.MelonLogger.Msg("PASS|startup-hint|Four-second non-interactive hint faded and removed");
        }
    }

    private bool Probe => _probe && Game.SaveIsolation.IsIsolated;
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, name));

    public void Dispose()
    {
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = null; _group = null; _finished = true;
    }
}
