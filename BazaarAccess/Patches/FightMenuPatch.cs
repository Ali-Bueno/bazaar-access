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
        // Cooldown para evitar reabrir inmediatamente
        if (Time.time - _lastCloseTime < COOLDOWN)
        {
            return;
        }

        if (_isOpen) return;
        _isOpen = true;

        // Buscar el fightMenuDialog activo
        var fightMenuDialogField = __instance.GetType().GetField("fightMenuDialog",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

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
        var currentUI = FightMenuShowPatch.GetCurrentUI();
        if (currentUI != null)
        {
            AccessibilityMgr.HideUI(currentUI);
            FightMenuShowPatch.SetClosed();
            Plugin.Logger.LogInfo("FightMenuUI cerrada");
        }
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
        // Cerrar el FightMenuUI primero
        var fightMenuUI = FightMenuShowPatch.GetCurrentUI();
        if (fightMenuUI != null)
        {
            AccessibilityMgr.HideUI(fightMenuUI);
            FightMenuShowPatch.SetClosed();
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

        // Check if already created by OptionsDialogShowPatch
        if (OptionsDialogShowPatch.GetCurrentUI() != null)
        {
            Plugin.Logger.LogInfo("OptionsUI ya existe, no crear duplicado");
            yield break;
        }

        if (root != null && root.gameObject.activeInHierarchy)
        {
            var optionsUI = new OptionsUI(root);
            OptionsDialogShowPatch.RegisterUI(optionsUI); // Register so HidePatch can find it
            AccessibilityMgr.ShowUI(optionsUI);
            Plugin.Logger.LogInfo("OptionsUI abierta desde menú de pausa");
        }
    }
}

/// <summary>
/// Hook para cuando se cierra Options y vuelve al menú de pausa.
/// OptionsDialogHidePatch handles popping the OptionsUI.
/// This just recreates the FightMenuUI.
/// </summary>
[HarmonyPatch(typeof(FightMenuDialog), "OnOptionsClosed")]
public static class FightMenuOptionsClosedPatch
{
    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        Plugin.Logger.LogInfo("OnOptionsClosed: Volviendo al menú de pausa");

        // Recrear FightMenuUI (OptionsDialogHidePatch maneja el pop de OptionsUI)
        var fightMenuDialogField = __instance.GetType().GetField("fightMenuDialog",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (fightMenuDialogField != null)
        {
            var fightMenuDialog = fightMenuDialogField.GetValue(__instance) as GameObject;
            if (fightMenuDialog != null && fightMenuDialog.activeInHierarchy)
            {
                Plugin.Instance.StartCoroutine(CreateFightMenuUIDelayed(fightMenuDialog.transform));
            }
        }
    }

    private static System.Collections.IEnumerator CreateFightMenuUIDelayed(Transform root)
    {
        // Wait for OptionsDialogHidePatch to pop the OptionsUI first
        yield return null;
        yield return null;

        // Only create if no UI is currently on stack (OptionsUI should be gone)
        if (AccessibilityMgr.GetFocusedUI() == null)
        {
            var fightMenuUI = new FightMenuUI(root);
            FightMenuShowPatch.SetCurrentUI(fightMenuUI);
            AccessibilityMgr.ShowUI(fightMenuUI);
            Plugin.Logger.LogInfo("FightMenuUI reabierta");
        }
        else
        {
            Plugin.Logger.LogInfo("FightMenuUI no creada - ya hay una UI en el stack");
        }
    }
}
