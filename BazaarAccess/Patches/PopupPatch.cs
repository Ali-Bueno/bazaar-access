using BazaarAccess.Accessibility;
using HarmonyLib;
using TheBazaar;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en PopupBase.Show para detectar cuando se abre un popup.
/// TODO: Implementar UIs accesibles para popups.
/// </summary>
[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Show))]
public static class PopupShowPatch
{
    static void Postfix(PopupBase __instance)
    {
        string popupName = __instance.GetType().Name;
        Plugin.Logger.LogInfo($"PopupBase.Show: {popupName}");

        // TODO: Crear UI accesible para el popup y hacer ShowUI
    }
}

/// <summary>
/// Hook en PopupBase.Hide para detectar cuando se cierra un popup.
/// </summary>
[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
public static class PopupHidePatch
{
    static void Postfix(PopupBase __instance)
    {
        string popupName = __instance.GetType().Name;
        Plugin.Logger.LogInfo($"PopupBase.Hide: {popupName}");

        // Pop de la UI si había una
        AccessibilityMgr.PopUI();
    }
}
