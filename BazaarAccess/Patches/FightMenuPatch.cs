using BazaarAccess.Accessibility;
using BazaarAccess.UI;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en FightMenuDialog para hacer accesible el menú de pausa durante el gameplay.
/// </summary>
[HarmonyPatch]
public static class FightMenuShowPatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("FightMenuDialog"), "ShowDialogs");

    private static FightMenuUI _currentUI;
    private static bool _isOpen = false;
    private static float _lastCloseTime = 0f;
    private const float COOLDOWN = 0.5f;

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        // No crear si ya hay alguna UI en el stack (OptionsUI, etc.)
        if (AccessibilityMgr.GetFocusedUI() != null)
        {
            Plugin.Logger.LogInfo("FightMenuShowPatch: Skipped - UI already on stack");
            return;
        }

        // Cooldown para evitar reabrir inmediatamente
        if (Time.time - _lastCloseTime < COOLDOWN)
        {
            return;
        }

        if (_isOpen) return;
        _isOpen = true;

        // Buscar el fightMenuDialog activo
        var fightMenuDialogField = __instance.GetType().GetField("fightMenuDialog",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (fightMenuDialogField != null)
        {
            var fightMenuDialog = fightMenuDialogField.GetValue(__instance) as GameObject;
            if (fightMenuDialog != null && fightMenuDialog.activeInHierarchy)
            {
                _currentUI = new FightMenuUI(fightMenuDialog.transform);
                AccessibilityMgr.ShowUI(_currentUI);
                Plugin.Logger.LogInfo("FightMenuUI abierta");
            }
        }
    }

    public static void SetClosed()
    {
        _isOpen = false;
        _currentUI = null;
        _lastCloseTime = Time.time;
    }

    public static void SetCurrentUI(FightMenuUI ui)
    {
        _currentUI = ui;
        _isOpen = ui != null;
    }

    public static FightMenuUI GetCurrentUI() => _currentUI;
}

[HarmonyPatch]
public static class FightMenuHidePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("FightMenuDialog"), "HideDialogs");

    [HarmonyPostfix]
    public static void Postfix()
    {
        Plugin.Logger.LogInfo("HideDialogs: Cerrando todo el sistema de pausa");

        // Cerrar OptionsUI si existe
        var optionsUI = OptionsDialogShowPatch.GetCurrentUI();
        if (optionsUI != null)
        {
            AccessibilityMgr.HideUI(optionsUI);
            OptionsDialogShowPatch.SetClosed();
            Plugin.Logger.LogInfo("OptionsUI cerrada desde HideDialogs");
        }

        // Cerrar FightMenuUI si existe
        var fightMenuUI = FightMenuShowPatch.GetCurrentUI();
        if (fightMenuUI != null)
        {
            AccessibilityMgr.HideUI(fightMenuUI);
            Plugin.Logger.LogInfo("FightMenuUI cerrada desde HideDialogs");
        }

        // Siempre resetear el estado
        FightMenuShowPatch.SetClosed();
    }
}

/// <summary>
/// Hook para cuando se abre Options desde el menú de pausa.
/// </summary>
[HarmonyPatch]
public static class FightMenuOptionsClickPatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("FightMenuDialog"), "OnOptionsClick");

    [HarmonyPostfix]
    public static void Postfix()
    {
        Plugin.Logger.LogInfo("OnOptionsClick: Abriendo settings");

        // El juego abre las opciones como un popup aparte
        // (_popupManager.ShowSettings -> OptionsDialogController.OnEnable), que
        // OptionsDialogShowPatch apila encima como OptionsUI.
        //
        // NO sacamos la FightMenuUI del stack: al dejarla debajo evitamos que el stack
        // quede vacío (y que la pantalla de gameplay reciba el foco y se mezcle con las
        // opciones) durante la transición, y conseguimos que al cerrar las opciones se
        // restaure automáticamente el menú de pausa que hay debajo.
    }
}

/// <summary>
/// Hook para cuando se cierra Options.
/// Escape cierra todo el sistema de pausa, así que solo limpiamos OptionsUI.
/// </summary>
[HarmonyPatch]
public static class FightMenuOptionsClosedPatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("FightMenuDialog"), "OnOptionsClosed");

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        Plugin.Logger.LogInfo("OnOptionsClosed: Cerrando OptionsUI");

        // Cerrar OptionsUI
        var optionsUI = OptionsDialogShowPatch.GetCurrentUI();
        if (optionsUI != null)
        {
            AccessibilityMgr.HideUI(optionsUI);
            OptionsDialogShowPatch.SetClosed();
        }

        // NO crear FightMenuUI aquí - Escape cierra todo el sistema de pausa
        // Si el usuario quiere volver a pausar, presionará Escape de nuevo
    }
}
