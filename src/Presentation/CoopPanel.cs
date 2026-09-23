using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MvzMp.Presentation;

/// <summary>Scene-safe co-op controls styled from the game's settings button.</summary>
internal sealed class CoopPanel : IDisposable
{
    private readonly Func<string> _status;
    private readonly Func<bool> _connected;
    private readonly Action _host;
    private readonly Action _invite;
    private readonly Action _leave;
    private GameObject? _root;
    private Canvas? _canvas;
    private TextMeshProUGUI? _statusText;
    private Button? _hostButton;
    private Button? _inviteButton;
    private Button? _leaveButton;
    private readonly List<UnityAction> _listeners = new();
    private bool _disposed;

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
        var nativeButton = GameManager.Instance?._uiManager?._settingButton;
        var canvas = nativeButton == null ? null : nativeButton.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            DestroyPanel();
            return;
        }
        if (_root == null || _canvas != canvas)
        {
            DestroyPanel();
            CreatePanel(canvas, nativeButton!);
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

    private void CreatePanel(Canvas canvas, Button nativeButton)
    {
        var nativeText = nativeButton.GetComponentInChildren<TextMeshProUGUI>() ??
            GameManager.Instance?._uiManager?.GetComponentInChildren<TextMeshProUGUI>(true);
        _root = NewUiObject("MVZ-MP Co-op", canvas.transform);
        _canvas = canvas;
        var panel = _root.GetComponent<RectTransform>();
        panel.anchorMin = Vector2.one;
        panel.anchorMax = Vector2.one;
        panel.pivot = Vector2.one;
        panel.anchoredPosition = new Vector2(-18f, -18f);
        panel.sizeDelta = new Vector2(280f, 124f);
        var background = _root.AddComponent<Image>();
        background.color = new Color(0.12f, 0.16f, 0.17f, 0.9f);
        background.raycastTarget = false;

        var title = MakeText("Title", _root.transform, nativeText, "CO-OP", 18f);
        Position(title.rectTransform, new Vector2(0f, 36f), new Vector2(260f, 28f));
        _statusText = MakeText("Status", _root.transform, nativeText, string.Empty, 13f);
        Position(_statusText.rectTransform, new Vector2(0f, 4f), new Vector2(260f, 40f));
        _hostButton = MakeButton("Host", -88f, nativeButton, nativeText, _host);
        _inviteButton = MakeButton("Invite", 0f, nativeButton, nativeText, _invite);
        _leaveButton = MakeButton("Leave", 88f, nativeButton, nativeText, _leave);
    }

    private Button MakeButton(string label, float x, Button template, TextMeshProUGUI? nativeText, Action callback)
    {
        var obj = NewUiObject(label, _root!.transform);
        Position(obj.GetComponent<RectTransform>(), new Vector2(x, -42f), new Vector2(82f, 29f));
        var image = obj.AddComponent<Image>();
        var nativeImage = template.GetComponent<Image>();
        if (nativeImage != null)
        {
            image.sprite = nativeImage.sprite;
            image.type = nativeImage.type;
            image.color = nativeImage.color;
        }
        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = template.colors;
        button.transition = template.transition;
        var listener = DelegateSupport.ConvertDelegate<UnityAction>((Action)callback.Invoke)!;
        _listeners.Add(listener);
        button.onClick.AddListener(listener);
        var text = MakeText("Label", obj.transform, nativeText, label, 14f);
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
        _canvas = null;
        _statusText = null;
        _hostButton = null;
        _inviteButton = null;
        _leaveButton = null;
        _listeners.Clear();
    }
}
