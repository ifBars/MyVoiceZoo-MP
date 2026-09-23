using HarmonyLib;
using Il2Cpp;

namespace MvzMp.Game;

internal static class NativeHooks
{
    internal static CoopRuntime? Runtime;

    [HarmonyPatch(typeof(CollectionDetailPanel), nameof(CollectionDetailPanel.OnClickAdoptButton))]
    private static class Adopt
    {
        private static bool Prefix(CollectionDetailPanel __instance) => __instance._animal == null || (Runtime?.BeginEdit(__instance._animal.AnimalData.ID, true) ?? true);
    }
    [HarmonyPatch(typeof(CollectionDetailPanel), nameof(CollectionDetailPanel.OnClickEditButton))]
    private static class Edit
    {
        private static bool Prefix(CollectionDetailPanel __instance) => __instance._animal == null || (Runtime?.BeginEdit(__instance._animal.AnimalData.ID, false) ?? true);
    }
    [HarmonyPatch(typeof(AdoptView), nameof(AdoptView.Hide))]
    private static class FinishEdit { private static void Prefix(AdoptView __instance) => Runtime?.EndEdit(__instance); }
    [HarmonyPatch(typeof(AreaShopUI), nameof(AreaShopUI.OnClickBuyWindIsland))]
    private static class Wind { private static bool Prefix() => Runtime?.Purchase("area", 0) ?? true; }
    [HarmonyPatch(typeof(AreaShopUI), nameof(AreaShopUI.OnClickBuyDeepCave))]
    private static class Cave { private static bool Prefix() => Runtime?.Purchase("area", 1) ?? true; }
    [HarmonyPatch(typeof(CampCell), nameof(CampCell.OnClickBuyButton))]
    private static class Camp { private static bool Prefix(CampCell __instance) => Runtime?.Purchase("camp", (int)__instance._campType) ?? true; }
    [HarmonyPatch(typeof(CostumeDetailPanel), nameof(CostumeDetailPanel.OnClick_BuyButton))]
    private static class Costume { private static bool Prefix(CostumeDetailPanel __instance) => Runtime?.Purchase("costume", (int)__instance._costumeID) ?? true; }
    [HarmonyPatch(typeof(AnimalPickController), nameof(AnimalPickController.DropCurrent))]
    private static class Drop
    {
        private static void Prefix(AnimalPickController __instance, out AnimalPos? __state) => __state = __instance._pickedAnimalPos;
        private static void Postfix(AnimalPos? __state) => Runtime?.Moved(__state);
    }
    [HarmonyPatch(typeof(AnimalPrefab), nameof(AnimalPrefab.AddIncome))]
    private static class Income { private static bool Prefix() => !(Runtime?.IsGuest ?? false); }
}
