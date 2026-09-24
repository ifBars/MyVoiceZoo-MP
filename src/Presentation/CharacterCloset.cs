using Il2Cpp;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MvzMp.Presentation;

/// <summary>Per-player character selection and previews in the native costume closet.</summary>
internal static class CharacterCloset
{
    [HarmonyPatch(typeof(CostumeBuyNoticeView), nameof(CostumeBuyNoticeView.Show))]
    private static class PurchasePreview
    {
        private static void Postfix(CostumeBuyNoticeView __instance, CostumeID costumeID)
            => UpdatePreview(__instance._costumeIcon, (int)costumeID, false);
    }

    private sealed class Preview
    {
        public Image Image = null!;
        public Sprite? Native;
        public Sprite? Replacement;
    }

    private static readonly Dictionary<int, Preview> Previews = new();
    private static readonly List<UnityAction> Listeners = new();
    private static CostumeUI? _closet;
    private static GameObject? _root;
    private static Button? _lucy;
    private static Button? _male;
    private static TextMeshProUGUI? _title;
    private static string? _nativeTitle;

    public static void Tick()
    {
        var closet = GameManager.Instance?._uiManager?._costumeUI;
        if (closet == null) { Dispose(); return; }
        if (!closet.gameObject.activeInHierarchy) return;
        if (_closet != closet || _root == null)
        {
            Dispose();
            CreatePanel(closet);
        }
        UpdateButton(_lucy, "Lucy", CharacterAppearance.Selected == 0, true);
        UpdateButton(_male, "Male character", CharacterAppearance.Selected == 1, CharacterAppearance.Ready);
        if (_title != null) _title.text = "Closet";
        var detail = closet._costumeDetailPanel;
        if (detail != null) UpdatePreview(detail._costumeIcon, (int)detail._costumeID);
        if (closet._costumeCells != null)
            foreach (var cell in closet._costumeCells)
                if (cell != null) UpdatePreview(cell._costumeIcon, (int)cell.CostumeID, portrait: true);
    }

    private static void UpdateButton(Button? button, string name, bool selected, bool available)
    {
        if (button == null) return;
        button.interactable = available && !selected;
        var colors = button.colors;
        colors.disabledColor = selected ? Color.white : new Color(.5f, .5f, .5f, .65f);
        button.colors = colors;
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = selected ? $"<size=23>{name}</size>\n<size=15>Selected</size>" : name;
    }

    private static void UpdatePreview(Image? image, int costume, bool portrait = false)
    {
        if (image == null) return;
        var id = image.GetInstanceID();
        if (!Previews.TryGetValue(id, out var preview))
        {
            preview = new Preview { Image = image, Native = image.sprite };
            Previews.Add(id, preview);
        }
        // Native UI refreshes replace the sprite when a different outfit is selected.
        if (image.sprite != preview.Replacement)
        {
            preview.Native = image.sprite;
        }
        if (CharacterAppearance.Selected == 0)
        {
            if (image.sprite == preview.Replacement) image.sprite = preview.Native;
            preview.Replacement = null;
            return;
        }
        var replacement = portrait ? CharacterAppearance.GetPortrait(costume) : CharacterAppearance.GetPreview(costume);
        if (replacement == null) return;
        preview.Replacement = replacement;
        if (image.sprite != replacement) image.sprite = replacement;
    }

    private static void CreatePanel(CostumeUI closet)
    {
        var frame = closet.transform.Find("Contents/Frame");
        var template = closet._costumeDetailPanel?._equipButton;
        if (frame == null || template == null) return;
        _closet = closet;
        _title = frame.Find("Title/TitleText")?.GetComponent<TextMeshProUGUI>();
        _nativeTitle = _title?.text;
        if (_title != null) _title.text = "Closet";
        _root = NewUiObject("MVZ-MP Character", frame);
        // The native frame is 1005 units tall; both outfit columns end above -390.
        Position(_root.GetComponent<RectTransform>(), new Vector2(0f, -440f), new Vector2(1000f, 64f));
        var notice = frame.Find("CostumeBuyNoticeView");
        if (notice != null) _root.transform.SetSiblingIndex(notice.GetSiblingIndex());
        var heading = NewUiObject("Character", _root.transform).AddComponent<TextMeshProUGUI>();
        var nativeText = template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nativeText != null)
        {
            heading.font = nativeText.font;
            heading.fontSharedMaterial = nativeText.fontSharedMaterial;
            heading.color = nativeText.color;
        }
        heading.fontSize = 27f;
        heading.alignment = TextAlignmentOptions.Center;
        heading.text = "Character";
        heading.raycastTarget = false;
        Position(heading.rectTransform, new Vector2(-355f, 0f), new Vector2(210f, 60f));
        _lucy = MakeButton("Lucy", 0, new Vector2(-80f, 0f), template);
        _male = MakeButton("Male character", 1, new Vector2(205f, 0f), template);
        MelonLoader.MelonLogger.Msg("CHARACTER CLOSET Native character controls installed");
    }

    private static Button MakeButton(string label, int character, Vector2 position, Button template)
    {
        var obj = NewUiObject(label, _root!.transform);
        Position(obj.GetComponent<RectTransform>(), position, new Vector2(250f, 60f));
        var image = obj.AddComponent<Image>();
        var nativeImage = template.transform.Find("Frame")?.GetComponent<Image>() ?? template.GetComponent<Image>();
        image.sprite = nativeImage?.sprite;
        image.type = nativeImage?.type ?? Image.Type.Simple;
        image.color = nativeImage?.color ?? Color.white;
        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = template.colors;
        button.transition = Selectable.Transition.ColorTint;
        var listener = DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => CharacterAppearance.Select(character)))!;
        Listeners.Add(listener);
        button.onClick.AddListener(listener);
        var labelObject = NewUiObject("Label", obj.transform);
        var text = labelObject.AddComponent<TextMeshProUGUI>();
        var nativeText = template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nativeText != null)
        {
            text.font = nativeText.font;
            text.fontSharedMaterial = nativeText.fontSharedMaterial;
            text.color = nativeText.color;
        }
        text.fontSize = 25f;
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
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
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Dispose()
    {
        foreach (var preview in Previews.Values)
            if (preview.Image != null && preview.Image.sprite == preview.Replacement)
                preview.Image.sprite = preview.Native;
        Previews.Clear();
        if (_title != null && _title.text == "Closet") _title.text = _nativeTitle ?? string.Empty;
        _title = null;
        _nativeTitle = null;
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = null;
        _closet = null;
        _lucy = _male = null;
        Listeners.Clear();
    }
}
