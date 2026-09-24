using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using MelonLoader;
using MvzMp.Game;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace MvzMp.Presentation;

/// <summary>Owns optional character artwork; native costume assets remain untouched.</summary>
internal static class CharacterAppearance
{
    private static readonly Dictionary<int, SpriteLibraryAsset> Native = new();
    private static readonly Dictionary<int, SpriteLibraryAsset> Male = new();
    private static readonly Dictionary<int, Sprite> Portraits = new();
    private static readonly List<UnityEngine.Object> Owned = new();
    private static readonly CostumeID[] Costumes = Enum.GetValues<CostumeID>();
    private static bool _loadedPreference, _failed;
    private static int _nextCostume;
    private static Player? _player;
    private static int _appliedCharacter = -1, _appliedCostume = -1;
    public static int Selected { get; private set; }
    public static bool Ready => Male.Count == Costumes.Length && !_failed;

    private static string PreferencePath => SaveIsolation.IsIsolated
        ? Path.Combine(Path.GetDirectoryName(SaveLoadSystem._path)!, "character.json")
        : Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MyVoiceZooTogether.character.json");

    public static void Tick()
    {
        var player = GameManager.Instance?._player;
        if (player == null || player._spriteLibrary == null || DataManager.Instance == null) return;
        if (!_loadedPreference)
        {
            _loadedPreference = true;
            try
            {
                if (File.Exists(PreferencePath)) Selected = JsonSerializer.Deserialize<int>(File.ReadAllText(PreferencePath)) == 1 ? 1 : 0;
            }
            catch (Exception e) { MelonLogger.Warning($"Could not read character preference: {e.Message}"); }
        }
        if (!_failed && _nextCostume < Costumes.Length)
        {
            try { LoadCostume((int)Costumes[_nextCostume++]); }
            catch (Exception e)
            {
                _failed = true;
                Selected = 0;
                MelonLogger.Error($"Character artwork could not load; using Lucy. {e.Message}");
            }
        }
        var costume = (int)CostumeManager.Instance.EquippedCostumeID;
        var character = Ready ? Selected : 0;
        var asset = GetLibrary(character, costume);
        if (asset == null) return;
        if (_player != player || _appliedCharacter != character || _appliedCostume != costume || player._spriteLibrary.spriteLibraryAsset != asset)
        {
            player._spriteLibrary.spriteLibraryAsset = asset;
            player._spriteLibrary.RefreshSpriteResolvers();
            _player = player;
            _appliedCharacter = character;
            _appliedCostume = costume;
        }
    }

    public static bool Select(int character)
    {
        if (character is < 0 or > 1 || (character == 1 && !Ready)) return false;
        try { File.WriteAllText(PreferencePath, JsonSerializer.Serialize(character)); }
        catch (Exception e) { MelonLogger.Warning($"Could not save character preference: {e.Message}"); return false; }
        Selected = character;
        Tick();
        return true;
    }

    public static Sprite? GetPreview(int costumeId) => GetLibrary(Selected, costumeId)?.GetSprite("Idle", "Idle01");

    public static Sprite? GetPortrait(int costumeId)
    {
        if (Selected == 0) return null; // The closet retains its native Lucy portraits.
        if (Portraits.TryGetValue(costumeId, out var portrait)) return portrait;
        var full = GetPreview(costumeId);
        if (full == null) return null;
        portrait = Sprite.Create(full.texture, new Rect(0, 0, 256, 256), new Vector2(.5f, .5f), full.pixelsPerUnit);
        Owned.Add(portrait);
        Portraits.Add(costumeId, portrait);
        return portrait;
    }

    public static SpriteLibraryAsset? GetLibrary(int character, int costume)
    {
        if (character == 1 && Male.TryGetValue(costume, out var male)) return male;
        if (Native.TryGetValue(costume, out var native)) return native;
        var data = DataManager.Instance?.GetCostumeData((CostumeID)costume);
        if (data == null || string.IsNullOrEmpty(data.SpriteLibraryPath)) return null;
        native = Resources.Load<SpriteLibraryAsset>(data.SpriteLibraryPath);
        if (native != null) Native[costume] = native;
        return native;
    }

    private static void LoadCostume(int costume)
    {
        var native = GetLibrary(0, costume) ?? throw new InvalidOperationException($"Missing native costume {costume}.");
        var library = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
        library.name = $"MVZ Together Male {costume}";
        Owned.Add(library);
        foreach (var category in native.categories)
        foreach (var entry in category.categoryList)
        {
            var original = entry.sprite;
            var resource = $"MvzMp.Characters.Male.{costume}.{entry.name}.png";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing artwork {resource}.");
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Owned.Add(texture);
            if (!ImageConversion.LoadImage(texture, bytes.ToArray(), false)) throw new InvalidOperationException($"Invalid artwork {resource}.");
            texture.filterMode = original.texture.filterMode;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (texture.width != (int)original.rect.width || texture.height != (int)original.rect.height)
                throw new InvalidOperationException($"Artwork size mismatch {resource}.");
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height), original.pixelsPerUnit);
            Owned.Add(sprite);
            sprite.name = $"Male_{costume}_{entry.name}";
            library.AddCategoryLabel(sprite, category.name, entry.name);
        }
        Male.Add(costume, library);
    }

    public static void Dispose()
    {
        if (_player != null && _player._spriteLibrary != null && Native.TryGetValue(_appliedCostume, out var native))
        {
            _player._spriteLibrary.spriteLibraryAsset = native;
            _player._spriteLibrary.RefreshSpriteResolvers();
        }
        foreach (var asset in Owned) if (asset != null) UnityEngine.Object.Destroy(asset);
        Owned.Clear(); Male.Clear(); Native.Clear(); Portraits.Clear();
    }
}

