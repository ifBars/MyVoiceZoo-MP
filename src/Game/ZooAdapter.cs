using Il2Cpp;
using MvzMp.State;
using UnityEngine;

namespace MvzMp.Game;

internal sealed class ZooAdapter
{
    private readonly VoiceStore _voices;
    public ZooAdapter(VoiceStore voices) => _voices = voices;
    public bool Ready => GameManager.Instance != null && GameManager.Instance._loadCompleted && AnimalManager.Instance?.AnimalDict != null;

    public Animal? Animal(int id) => AnimalManager.Instance?.AnimalDict != null && AnimalManager.Instance.AnimalDict.TryGetValue(id, out var animal) ? animal : null;

    public ZooState Capture()
    {
        if (!Ready) throw new InvalidOperationException("Zoo is still loading.");
        var state = new ZooState
        {
            Gold = Wallet.Instance.CurrentGold,
            TutorialCompleted = TutorialManager.Instance.IsTutorialCompleted,
            WindIsland = AreaManager.Instance.IsUnlock_WindIsland,
            DeepCave = AreaManager.Instance.IsUnlock_DeepCave,
            Camps = Enum.GetValues<CampType>().Where(x => CampManager.Instance.GetCampState(x)).Select(x => (int)x).ToArray(),
            Costumes = Enum.GetValues<CostumeID>().Where(x => CostumeManager.Instance.IsBuyCostume(x)).Select(x => (int)x).ToArray(),
            EquippedCostume = (int)CostumeManager.Instance.EquippedCostumeID
        };
        var list = AnimalManager.Instance.GetAnimalList();
        var animals = new List<AnimalState>(list.Count);
        var positions = GameManager.Instance._animalPrefabController._animalPosDict;
        for (var i = 0; i < list.Count; i++)
        {
            var animal = list[i];
            var id = animal.AnimalData.ID;
            var entry = new AnimalState { Id = id, Collected = animal.IsCollected, Name = animal.Name ?? "", Voice = _voices.Capture(animal.Voice) };
            if (positions.TryGetValue(id, out var pos) && pos != null)
            {
                var p = pos.transform.position;
                entry.X = p.x; entry.Y = p.y; entry.Z = p.z; entry.SortingOrder = pos.GetCurrentSortingOrder();
            }
            animals.Add(entry);
        }
        state.Animals = animals.OrderBy(x => x.Id).ToArray();
        return state;
    }

    public void Apply(ZooState state, int editingAnimal = -1, bool restoreCostume = false)
    {
        if (!Ready) throw new InvalidOperationException("Zoo is still loading.");
        Validate(state);
        using var scope = new GameMutationScope();
        var manager = AnimalManager.Instance;
        var controller = GameManager.Instance._animalPrefabController;
        foreach (var entry in state.Animals)
        {
            var animal = Animal(entry.Id)!;
            var existed = controller._spawnedAnimalPrefabDict.TryGetValue(entry.Id, out var prefab) && prefab != null;
            if (entry.Collected && !animal.IsCollected)
            {
                if (existed) { animal.SetIsCollected(true); prefab!.gameObject.SetActive(true); }
                else manager.AnimalCollectStateChange(entry.Id, true);
            }
            else if (!entry.Collected)
            {
                animal.SetIsCollected(false);
                if (existed) prefab!.gameObject.SetActive(false);
            }
            if (entry.Id != editingAnimal)
            {
                if (animal.Name != entry.Name) animal.SetName(entry.Name);
                if (_voices.Contains(entry.Voice))
                {
                    var clip = _voices.Clip(entry.Voice);
                    if (animal.Voice != clip) animal.SetVoice(clip, false);
                }
            }
            if (controller._animalPosDict.TryGetValue(entry.Id, out var pos) && pos != null && entry.Id != editingAnimal)
            {
                pos.transform.position = new Vector3(entry.X, entry.Y, entry.Z);
                pos.SetCurrentSortingOrder(entry.SortingOrder);
            }
        }
        Wallet.Instance.Init(state.Gold);
        if (state.TutorialCompleted && !TutorialManager.Instance.IsTutorialCompleted) TutorialManager.Instance.TryEndTutorial();
        else TutorialManager.Instance.SetIsTutorialCompleted(state.TutorialCompleted);
        AreaManager.Instance.ChangeStateWindIsland(state.WindIsland);
        AreaManager.Instance.ChangeStateDeepCave(state.DeepCave);
        foreach (var camp in Enum.GetValues<CampType>()) CampManager.Instance.CampStateDict[camp] = state.Camps.Contains((int)camp);
        CampManager.Instance.RefreshSetActive_AllCamps();
        foreach (var costume in Enum.GetValues<CostumeID>()) CostumeManager.Instance.CostumeBuyStateDict[costume] = state.Costumes.Contains((int)costume);
        if (restoreCostume) CostumeManager.Instance.EquipCostume((CostumeID)state.EquippedCostume);
        CostumeManager.Instance.UpdateAnimalsCostumeVoice();
    }

    public void Validate(ZooState state)
    {
        var known = AnimalManager.Instance.AnimalDict;
        if (state.Gold < 0 || state.Gold > Wallet.GOLD_LIMIT || state.Animals.Length != known.Count ||
            state.Animals.Select(x => x.Id).Distinct().Count() != state.Animals.Length || state.Camps.Length > 16 || state.Costumes.Length > 64)
            throw new InvalidDataException("Invalid zoo snapshot.");
        foreach (var entry in state.Animals)
            if (!known.ContainsKey(entry.Id) || entry.Name is null || entry.Name.Length > 256 || entry.Voice is null || entry.Voice.Length > 64 || !ValidPosition(entry.X, entry.Y, entry.Z))
                throw new InvalidDataException("Invalid animal state.");
    }

    public static bool ValidPosition(float x, float y, float z) => float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) && Math.Abs(x) < 10000 && Math.Abs(y) < 10000 && Math.Abs(z) < 10000;

    public void Move(ZooCommand command)
    {
        if (!ValidPosition(command.X, command.Y, command.Z)) throw new InvalidDataException("Invalid animal position.");
        var animal = Animal(command.AnimalId);
        if (animal == null || !animal.IsCollected) throw new InvalidOperationException("That animal is not adopted.");
        var positions = GameManager.Instance._animalPrefabController._animalPosDict;
        if (!positions.TryGetValue(command.AnimalId, out var pos) || pos == null) throw new InvalidOperationException("Animal position is unavailable.");
        pos.transform.position = new Vector3(command.X, command.Y, command.Z);
        pos.SetCurrentSortingOrder(Math.Clamp(command.SortingOrder, -32768, 32767));
    }

    public void Purchase(string kind, int value)
    {
        switch (kind)
        {
            case "area":
                if (value == 0) { if (AreaManager.Instance.IsUnlock_WindIsland) throw new InvalidOperationException("Area already unlocked."); if (!AreaManager.Instance.BuyWindIsland()) throw new InvalidOperationException("Not enough gold."); }
                else if (value == 1) { if (AreaManager.Instance.IsUnlock_DeepCave) throw new InvalidOperationException("Area already unlocked."); if (!AreaManager.Instance.BuyDeepCave()) throw new InvalidOperationException("Not enough gold."); }
                else throw new InvalidDataException("Unknown area.");
                break;
            case "camp":
                if (!Enum.IsDefined(typeof(CampType), value) || CampManager.Instance.GetCampState((CampType)value)) throw new InvalidOperationException("Camp unavailable.");
                if (!CampManager.Instance.BuyCamp((CampType)value)) throw new InvalidOperationException("Not enough gold.");
                break;
            case "costume":
                if (!Enum.IsDefined(typeof(CostumeID), value) || CostumeManager.Instance.IsBuyCostume((CostumeID)value)) throw new InvalidOperationException("Costume unavailable.");
                if (!CostumeManager.Instance.CanBuyCostumeCondition((CostumeID)value) || !Wallet.Instance.HasEnoughGold(DataManager.Instance.GetCostumeData((CostumeID)value).BuyCost)) throw new InvalidOperationException("Costume requirements are not met.");
                CostumeManager.Instance.BuyCostume((CostumeID)value);
                break;
            default: throw new InvalidDataException("Unknown purchase.");
        }
    }

    public void Save() { if (Ready) GameManager.Instance.SaveGame(false); }

    public void BeginEditor(int id, bool adoption)
    {
        var animal = Animal(id) ?? throw new InvalidOperationException("Unknown animal.");
        AnimalManager.Instance.Notify_OnAdoptEditProcessStart(animal);
        if (adoption) AnimalManager.Instance.OnAdoptAnimal?.Invoke(animal);
        else AnimalManager.Instance.OnEditAnimal?.Invoke(animal);
    }
}

internal readonly struct GameMutationScope : IDisposable
{
    private static int _depth;
    public static bool Active => _depth > 0;
    public GameMutationScope() => _depth++;
    public void Dispose() => _depth--;
}

