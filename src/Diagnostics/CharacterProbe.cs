using Il2Cpp;
using MelonLoader;
using MvzMp.Game;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace MvzMp.Diagnostics;

/// <summary>Exports native costume sprite evidence only in an explicitly isolated diagnostic run.</summary>
internal static class CharacterProbe
{
    private static readonly bool Requested = Environment.GetCommandLineArgs().Contains("--mvzmp-character-probe");
    private static readonly CostumeID[] Costumes = Enum.GetValues<CostumeID>();
    private static readonly Dictionary<int, string> Textures = new();
    private static readonly List<object> Sprites = new();
    private static int _index;
    private static float _readyAt = -1, _closetShownAt = -1;
    private static CostumeUI? _closet;
    private static bool _selectedMale;
    private static bool _noticeShown;
    private static bool _done;

    public static void Tick()
    {
        if (!Requested || _done || !SaveIsolation.IsIsolated || GameManager.Instance == null ||
            !GameManager.Instance._loadCompleted || DataManager.Instance == null || string.IsNullOrEmpty(SaveLoadSystem._path))
            return;
        try
        {
            var directory = Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, "character-probe");
            Directory.CreateDirectory(directory);
            // One costume per frame keeps this optional inventory bounded during startup.
            if (_index < Costumes.Length)
            {
                ExportCostume(Costumes[_index++], directory);
                return;
            }
            if (_closetShownAt < 0)
            {
                var game = GameManager.Instance;
                var ui = game._uiManager;
                if (!UnityEngine.Rendering.SplashScreen.isFinished ||
                    (game._loadingScreen != null && game._loadingScreen.gameObject.activeInHierarchy) ||
                    (game._micGuideScreen != null && game._micGuideScreen.gameObject.activeInHierarchy) ||
                    ui?._settingButton == null || !ui._settingButton.gameObject.activeInHierarchy ||
                    (ui._allUIBlock != null && ui._allUIBlock.activeInHierarchy))
                {
                    _readyAt = -1;
                    return;
                }
                if (_readyAt < 0) _readyAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - _readyAt < 1f) return;
                _closet = Resources.FindObjectsOfTypeAll<CostumeUI>().FirstOrDefault(ui => ui.gameObject.scene.IsValid());
                if (_closet == null) throw new InvalidOperationException("Native closet was unavailable after game readiness.");
                _closet.Show();
                _closetShownAt = Time.realtimeSinceStartup;
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--mvzmp-character-male") && !_selectedMale)
            {
                var button = _closet?.transform.Find("Contents/Frame/MVZ-MP Character/Male character")?.GetComponent<UnityEngine.UI.Button>();
                if (button == null) return;
                if (Presentation.CharacterAppearance.Selected != 1) button.onClick.Invoke();
                if (Presentation.CharacterAppearance.Selected != 1) throw new InvalidOperationException("Closet character button did not switch character.");
                _selectedMale = true;
                _closetShownAt = Time.realtimeSinceStartup;
                MelonLogger.Msg("PASS|character-closet-button|Native closet male selection callback succeeded");
            }
            if (Time.realtimeSinceStartup - _closetShownAt < 5f) return;
            if (Environment.GetCommandLineArgs().Contains("--mvzmp-character-notice") && !_noticeShown)
            {
                var notice = _closet!._costumeDetailPanel._costumeBuyNoticeView;
                notice.Show(CostumeID.Default);
                if (notice._costumeIcon.sprite != Presentation.CharacterAppearance.GetPreview((int)CostumeID.Default))
                    throw new InvalidOperationException("Purchase notice character preview mismatch.");
                _noticeShown = true;
                _closetShownAt = Time.realtimeSinceStartup;
                MelonLogger.Msg("PASS|character-purchase-preview|Native purchase notice uses selected character");
                return;
            }
            if (_closet != null)
            {
                var hierarchy = new List<object>();
                foreach (var rect in _closet.GetComponentsInChildren<RectTransform>(true))
                {
                    var path = rect.name;
                    var parent = rect.parent;
                    while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
                    var position = rect.anchoredPosition;
                    var size = rect.sizeDelta;
                    hierarchy.Add(new { Path = path, Active = rect.gameObject.activeInHierarchy,
                        Position = new { position.x, position.y }, Size = new { size.x, size.y } });
                }
                File.WriteAllText(Path.Combine(directory, "closet.json"), System.Text.Json.JsonSerializer.Serialize(hierarchy,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                if (!Environment.GetCommandLineArgs().Contains("-nographics"))
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory, _noticeShown ? "purchase.png" : "closet.png"));
            }
            File.WriteAllText(Path.Combine(directory, "sprites.json"), System.Text.Json.JsonSerializer.Serialize(Sprites,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            _done = true;
            MelonLogger.Msg($"PASS|character-probe|costumes={Costumes.Length} sprites={Sprites.Count} textures={Textures.Count} output={directory}");
        }
        catch (Exception exception)
        {
            _done = true;
            MelonLogger.Error($"FAIL|character-probe|{exception}");
        }
    }

    private static void ExportCostume(CostumeID costume, string directory)
    {
        var data = DataManager.Instance.GetCostumeData(costume);
        if (data == null || string.IsNullOrEmpty(data.SpriteLibraryPath))
            throw new InvalidOperationException($"Missing sprite library path for {costume}.");
        var library = Resources.Load<SpriteLibraryAsset>(data.SpriteLibraryPath);
        if (library == null) throw new InvalidOperationException($"Unable to load {data.SpriteLibraryPath}.");
        foreach (var nativeCategory in library.categories)
        foreach (var nativeLabel in nativeCategory.categoryList)
        {
            var category = nativeCategory.name;
            var label = nativeLabel.name;
            var sprite = library.GetSprite(category, label);
            if (sprite == null) continue;
            var texture = sprite.texture;
            var id = texture.GetInstanceID();
            if (!Textures.TryGetValue(id, out var textureFile))
            {
                textureFile = $"texture-{Textures.Count:D2}.png";
                ExportTexture(texture, Path.Combine(directory, textureFile));
                Textures.Add(id, textureFile);
            }
            var rect = sprite.rect;
            var pivot = sprite.pivot;
            Sprites.Add(new
            {
                Costume = costume.ToString(), CostumeId = (int)costume, Library = data.SpriteLibraryPath,
                Category = category, Label = label, Name = sprite.name,
                Texture = textureFile, TextureName = texture.name, TextureWidth = texture.width, TextureHeight = texture.height,
                Rect = new { rect.x, rect.y, rect.width, rect.height }, Pivot = new { pivot.x, pivot.y },
                PixelsPerUnit = sprite.pixelsPerUnit, Packed = sprite.packed
            });
            MelonLogger.Msg($"CHARACTER|costume={costume} category={category} label={label} sprite={sprite.name} texture={textureFile} rect={rect} pivot={pivot}");
        }
    }

    private static void ExportTexture(Texture2D source, string path)
    {
        var previous = RenderTexture.active;
        RenderTexture? target = null;
        Texture2D? readable = null;
        try
        {
            // GPU readback also supports native textures imported without Read/Write enabled.
            target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false, false);
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(readable).ToArray());
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (readable != null) UnityEngine.Object.Destroy(readable);
        }
    }
}
