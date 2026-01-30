using BazaarAccess.Accessibility;
using BazaarAccess.UI;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en FightMenuDialog para hacer accesible el menú de pausa durante el gameplay.
/// </summary>
[HarmonyPatch(typeof(FightMenuDialog), "ShowDialogs")]
public static class FightMenuShowPatch
{
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

[HarmonyPatch(typeof(FightMenuDialog), "HideDialogs")]
public static class FightMenuHidePatch
{
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
[HarmonyPatch(typeof(FightMenuDialog), "OnOptionsClick")]
public static class FightMenuOptionsClickPatch
{
    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        Plugin.Logger.LogInfo("OnOptionsClick: Abriendo settings");

        // Cerrar el FightMenuUI pero NO resetear _isOpen para evitar que se cree otra
        var fightMenuUI = FightMenuShowPatch.GetCurrentUI();
        if (fightMenuUI != null)
        {
            AccessibilityMgr.HideUI(fightMenuUI);
            // NO llamar SetClosed() - mantenemos _isOpen = true
        }

        // Buscar el optionsDialogParent y crear OptionsUI
        var optionsField = __instance.GetType().GetField("optionsDialogParent",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (optionsField != null)
        {
            var optionsController = optionsField.GetValue(__instance) as MonoBehaviour;
            if (optionsController != null)
            {
                Plugin.Instance.StartCoroutine(CreateOptionsUIDelayed(optionsController.transform));
            }
        }
    }

    private static System.Collections.IEnumerator CreateOptionsUIDelayed(Transform root)
    {
        yield return null;
        yield return null;

        // Si hay una OptionsUI vieja, limpiarla
        var oldUI = OptionsDialogShowPatch.GetCurrentUI();
        if (oldUI != null)
        {
            Plugin.Logger.LogInfo("Limpiando OptionsUI anterior");
            AccessibilityMgr.HideUI(oldUI);
            OptionsDialogShowPatch.SetClosed();
        }

        if (root != null && root.gameObject.activeInHierarchy)
        {
            var optionsUI = new OptionsUI(root);
            OptionsDialogShowPatch.RegisterUI(optionsUI);
            AccessibilityMgr.ShowUI(optionsUI);
            Plugin.Logger.LogInfo("OptionsUI abierta desde menú de pausa");
        }
    }
}

/// <summary>
/// Hook para cuando se cierra Options.
/// Escape cierra todo el sistema de pausa, así que solo limpiamos OptionsUI.
/// </summary>
[HarmonyPatch(typeof(FightMenuDialog), "OnOptionsClosed")]
public static class FightMenuOptionsClosedPatch
{
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
