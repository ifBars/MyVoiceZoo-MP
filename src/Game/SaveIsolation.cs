using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace MvzMp.Game;

/// <summary>Redirects test data to an explicit directory and blocks guest persistence.</summary>
internal static class SaveIsolation
{
    private static string? _dataDirectory;
    private static bool _initialized;

    /// <summary>Assigned by the lobby coordinator; true only while joined as a guest.</summary>
    public static Func<bool>? IsGuest { get; set; }

    public static bool IsIsolated => _dataDirectory != null;
    public static bool ShouldSave => !(IsGuest?.Invoke() ?? false);

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        const string option = "--mvzmp-data-dir=";
        var argument = Environment.GetCommandLineArgs()
            .FirstOrDefault(value => value.StartsWith(option, StringComparison.Ordinal));
        if (argument is null)
            return;

        var path = argument[option.Length..].Trim('"');
        if (!Path.IsPathFullyQualified(path))
        {
            MelonLogger.Warning("Ignoring --mvzmp-data-dir because it is not an absolute path.");
            return;
        }

        _dataDirectory = Path.GetFullPath(path);
        Directory.CreateDirectory(_dataDirectory);
        MelonLogger.Msg($"Isolated game data directory: {_dataDirectory}");
    }

    private static void SetSavePath()
    {
        if (_dataDirectory != null)
            SaveLoadSystem._path = Path.Combine(_dataDirectory, "save.json");
    }

    private static string RedirectVoicePath(string path)
    {
        if (_dataDirectory is null || string.IsNullOrWhiteSpace(path))
            return path;
        return Path.Combine(_dataDirectory, Path.GetFileName(path));
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveGame))]
    private static class GameManagerSavePatch
    {
        [HarmonyPrefix]
        private static bool Prefix() => ShouldSave;
    }

    [HarmonyPatch(typeof(SaveLoadSystem), nameof(SaveLoadSystem.SaveGame))]
    private static class SaveGamePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (!ShouldSave)
                return false;
            SetSavePath();
            return true;
        }
    }

    [HarmonyPatch(typeof(SaveLoadSystem), nameof(SaveLoadSystem.LoadGame))]
    private static class LoadGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix() => SetSavePath();
    }

    [HarmonyPatch(typeof(DataManager), nameof(DataManager.Init))]
    private static class DataManagerInitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DataManager __instance)
        {
            if (_dataDirectory is null)
                return;
            var animals = __instance.GetAnimalDataDict();
            if (animals is null)
                return;
            foreach (var entry in animals)
            {
                var animal = entry.Value;
                if (animal != null && !string.IsNullOrWhiteSpace(animal.VoiceFileName))
                    animal.VoiceFileName = RedirectVoicePath(animal.VoiceFileName);
            }
        }
    }

    [HarmonyPatch(typeof(WavSaveLoadManager), nameof(WavSaveLoadManager.Save))]
    private static class WavSavePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref string filePath)
        {
            if (!ShouldSave)
                return false;
            filePath = RedirectVoicePath(filePath);
            return true;
        }
    }

    [HarmonyPatch(typeof(WavSaveLoadManager), nameof(WavSaveLoadManager.Load))]
    private static class WavLoadPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ref string filePath) => filePath = RedirectVoicePath(filePath);
    }
}

