using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MvzMp.Presentation;

/// <summary>Co-op controls inside the native settings window.</summary>
internal sealed class CoopPanel : IDisposable
{
    private readonly Func<string> _status;
    private readonly Func<bool> _connected;
    private readonly Action _host;
    private readonly Action _invite;
    private readonly Action _leave;
    private GameObject? _root;
    private SettingView? _settings;
    private RectTransform? _popup;
    private Vector2 _popupOriginalSize;
    private RectTransform? _frame;
    private Vector2 _frameOriginalSize;
    private readonly List<(RectTransform Rect, Vector2 Position)> _shifted = new();
    private TextMeshProUGUI? _statusText;
    private Button? _hostButton;
    private Button? _inviteButton;
    private Button? _leaveButton;
    private readonly List<UnityAction> _listeners = new();
    private bool _disposed;
    private readonly bool _probeSettings = Environment.GetCommandLineArgs().Contains("--mvzmp-smoke-settings");
    private int _settingsProbePhase;
    private float _settingsProbeAt;
    private bool _settingsProbeInitiallyConnected;

    private void ProbeSettings()
    {
        if (!_probeSettings || !MvzMp.Game.SaveIsolation.IsIsolated) return;
        var settings = GameManager.Instance?._uiManager?._settingView;
        if (settings == null || GameManager.Instance?._loadCompleted != true) return;
        if (_settingsProbePhase == 0)
        {
            _settingsProbeAt = Time.realtimeSinceStartup + 25f;
            _settingsProbePhase = 1;
        }
        if (_settingsProbePhase == 1 && Time.realtimeSinceStartup >= _settingsProbeAt)
        {
            _settingsProbeInitiallyConnected = _connected();
            settings.Show();
            _settingsProbeAt = Time.realtimeSinceStartup + 2f;
            _settingsProbePhase = 2;
        }
        if (_settingsProbePhase == 3 && Time.realtimeSinceStartup >= _settingsProbeAt)
        {
            if (_settingsProbeInitiallyConnected || _connected())
            {
                FinishSettingsProbe(settings);
            }
            else if (_hostButton != null)
            {
                _hostButton.onClick.Invoke();
                _settingsProbeAt = Time.realtimeSinceStartup + 15f;
                _settingsProbePhase = 4;
            }
            else FailSettingsProbe(settings, "Host button unavailable");
        }
        if (_settingsProbePhase == 4)
        {
            if (_connected() && _leaveButton != null)
            {
                _leaveButton.onClick.Invoke();
                _settingsProbeAt = Time.realtimeSinceStartup + 15f;
                _settingsProbePhase = 5;
            }
            else if (Time.realtimeSinceStartup >= _settingsProbeAt) FailSettingsProbe(settings, "Host button did not create lobby");
        }
        if (_settingsProbePhase == 5)
        {
            if (!_connected())
            {
                MelonLoader.MelonLogger.Msg("PASS|settings-actions|Native settings Host and Leave callbacks completed");
                FinishSettingsProbe(settings);
            }
            else if (Time.realtimeSinceStartup >= _settingsProbeAt) FailSettingsProbe(settings, "Leave button did not exit lobby");
        }
        if (_settingsProbePhase != 2 || Time.realtimeSinceStartup < _settingsProbeAt) return;
        foreach (var rect in settings.GetComponentsInChildren<RectTransform>(true))
        {
            var path = rect.name;
            for (var parent = rect.parent; parent != null && parent != settings.transform; parent = parent.parent) path = parent.name + "/" + path;
            MelonLoader.MelonLogger.Msg($"SETTINGS RECT {path} pos={rect.anchoredPosition} size={rect.rect.size} pivot={rect.pivot} active={rect.gameObject.activeSelf}");
        }
        var arg = Environment.GetCommandLineArgs().First(value => value.StartsWith("--mvzmp-data-dir=", StringComparison.Ordinal));
        ScreenCapture.CaptureScreenshot(Path.Combine(arg["--mvzmp-data-dir=".Length..].Trim('"'), "settings.png"));
        MelonLoader.MelonLogger.Msg("SETTINGS SCREENSHOT Native settings opened and screenshot requested");
        _settingsProbePhase = 3;
        _settingsProbeAt = Time.realtimeSinceStartup + 1f;
    }

    private void FinishSettingsProbe(SettingView settings)
    {
        settings.Hide();
        _settingsProbePhase = 6;
        MelonLoader.MelonLogger.Msg(_root == null || !_root.activeInHierarchy
            ? "PASS|settings-hidden|Co-op controls hidden with native settings"
            : "FAIL|settings-hidden|Co-op controls remain visible");
    }

    private void FailSettingsProbe(SettingView settings, string reason)
    {
        MelonLoader.MelonLogger.Msg($"FAIL|settings-actions|{reason}");
        FinishSettingsProbe(settings);
    }

    public CoopPanel(Func<string> status, Func<bool> connected, Action host, Action invite, Action leave)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _connected = connected ?? throw new ArgumentNullException(nameof(connected));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _invite = invite ?? throw new ArgumentNullException(nameof(invite));
        _leave = leave ?? throw new ArgumentNullException(nameof(leave));
    }

    public void Tick()
    {
        if (_disposed) return;
        ProbeSettings();
        var settings = GameManager.Instance?._uiManager?._settingView;
        if (settings == null)
        {
            DestroyPanel();
            return;
        }
        // Create TMP only after the native window is active; IL2CPP TMP needs Awake first.
        if (!settings.gameObject.activeInHierarchy) return;
        if (_root == null || _settings != settings)
        {
            DestroyPanel();
            CreatePanel(settings);
        }
        if (_statusText != null) _statusText.text = _status();
        var connected = _connected();
        if (_hostButton != null) _hostButton.interactable = !connected;
        if (_inviteButton != null) _inviteButton.interactable = connected;
        if (_leaveButton != null) _leaveButton.interactable = connected;
    }

    // Kept for callers previously invoking Draw from OnGUI. UGUI renders itself.
    public void Draw() { }

    public void Dispose()
    {
        _disposed = true;
        DestroyPanel();
    }

    private void CreatePanel(SettingView settings)
    {
        var popupTransform = settings.transform.Find("Contents/Popup");
        var quit = popupTransform?.Find("QuitGameButton")?.GetComponent<Button>();
        if (popupTransform == null || quit == null) return;
        _settings = settings;
        _popup = popupTransform.GetComponent<RectTransform>();
        _popupOriginalSize = _popup.sizeDelta;
        _popup.sizeDelta = new Vector2(_popupOriginalSize.x + 624f, _popupOriginalSize.y);
        _frame = popupTransform.Find("Frame")?.GetComponent<RectTransform>();
        if (_frame != null)
        {
            _frameOriginalSize = _frame.sizeDelta;
            // Stretch-anchored frames follow the popup automatically.
            if (_frame.anchorMin.x == _frame.anchorMax.x)
                _frame.sizeDelta = new Vector2(_frameOriginalSize.x + 624f, _frameOriginalSize.y);
        }
        foreach (var name in new[] { "Audio", "Language", "Resolution", "MicGuideText" })
        {
            var rect = popupTransform.Find(name)?.GetComponent<RectTransform>();
            if (rect == null) continue;
            _shifted.Add((rect, rect.anchoredPosition));
            rect.anchoredPosition += new Vector2(-220f, 0f);
        }
        var nativeText = quit.GetComponentInChildren<TextMeshProUGUI>(true);
        _root = NewUiObject("MVZ-MP Co-op", popupTransform);
        Position(_root.GetComponent<RectTransform>(), new Vector2(280f, 0f), new Vector2(360f, 600f));
        var headingText = popupTransform.Find("Audio/AudioTitleText")?.GetComponent<TextMeshProUGUI>() ?? nativeText;
        var title = MakeText("Title", _root.transform, headingText, "Co-op", headingText?.fontSize ?? 32f);
        Position(title.rectTransform, new Vector2(0f, 345f), new Vector2(350f, 60f));
        _statusText = MakeText("Status", _root.transform, nativeText, string.Empty, 24f);
        Position(_statusText.rectTransform, new Vector2(0f, 244f), new Vector2(340f, 112f));
        _hostButton = MakeButton("Host zoo", 120f, quit, nativeText, _host);
        _inviteButton = MakeButton("Invite friends", 26f, quit, nativeText, _invite);
        _leaveButton = MakeButton("Leave co-op", -68f, quit, nativeText, _leave);
        var note = MakeText("Help", _root.transform, nativeText, "Friends share the host's zoo.\nYour solo zoo returns when you leave.", 20f);
        Position(note.rectTransform, new Vector2(0f, -170f), new Vector2(340f, 104f));
        var inviteHelp = MakeText("Invite help", _root.transform, nativeText,
            "Friend appearing offline?\nPress Shift+Tab, select them, then choose Invite to Game.", 19f);
        Position(inviteHelp.rectTransform, new Vector2(0f, -293f), new Vector2(340f, 110f));
        MelonLoader.MelonLogger.Msg("COOP SETTINGS Native settings controls installed");
    }

    private Button MakeButton(string label, float y, Button template, TextMeshProUGUI? nativeText, Action callback)
    {
        var obj = NewUiObject(label, _root!.transform);
        Position(obj.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(340f, 69f));
        var image = obj.AddComponent<Image>();
        var templateImage = template.GetComponent<Image>();
        image.sprite = templateImage?.sprite;
        image.type = templateImage?.type ?? Image.Type.Simple;
        image.color = templateImage?.color ?? Color.white;
        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = template.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.86f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.65f, 1f);
        colors.disabledColor = new Color(0.46f, 0.46f, 0.46f, 0.62f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        var listener = DelegateSupport.ConvertDelegate<UnityAction>((Action)callback.Invoke)!;
        _listeners.Add(listener);
        button.onClick.AddListener(listener);
        var text = MakeText("Label", obj.transform, nativeText, label, 28f);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.raycastTarget = false;
        return button;
    }

    private static TextMeshProUGUI MakeText(string name, Transform parent, TextMeshProUGUI? template, string value, float size)
    {
        var obj = NewUiObject(name, parent);
        var text = obj.AddComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.color = template.color;
        }
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        var components = new Il2CppReferenceArray<Il2CppSystem.Type>(1);
        components[0] = Il2CppType.Of<RectTransform>();
        var obj = new GameObject(name, components);
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void Position(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void DestroyPanel()
    {
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = null;
        foreach (var (rect, position) in _shifted)
            if (rect != null) rect.anchoredPosition = position;
        _shifted.Clear();
        if (_popup != null) _popup.sizeDelta = _popupOriginalSize;
        if (_frame != null) _frame.sizeDelta = _frameOriginalSize;
        _popup = null;
        _frame = null;
        _settings = null;
        _statusText = null;
        _hostButton = null;
        _inviteButton = null;
        _leaveButton = null;
        _listeners.Clear();
    }
}
