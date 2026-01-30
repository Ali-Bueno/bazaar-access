using BazaarAccess.Accessibility;
using BazaarAccess.UI;
using HarmonyLib;
using UnityEngine;
using TheBazaar;
using TheBazaar.UI;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en OptionsDialogController para hacer accesible el menú de opciones.
/// Solo activa la UI cuando el menú está realmente visible.
/// </summary>
[HarmonyPatch(typeof(OptionsDialogController), "OnEnable")]
public static class OptionsDialogShowPatch
{
    private static OptionsUI _currentOptionsUI;
    private static bool _isOpen = false;
    private static float _lastCloseTime = 0f;

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        // Cooldown para evitar reabrir inmediatamente después de cerrar
        if (Time.time - _lastCloseTime < 0.3f)
        {
            return;
        }

        // Evitar abrir múltiples veces
        if (_isOpen) return;

        // Verificar si el diálogo está realmente visible antes de crear UI
        if (!IsReallyVisible(__instance.transform))
        {
            return;
        }

        _isOpen = true;
        _currentOptionsUI = new OptionsUI(__instance.transform);
        AccessibilityMgr.ShowUI(_currentOptionsUI);
        Plugin.Logger.LogInfo("OptionsUI abierta (desde OnEnable)");
    }

    /// <summary>
    /// Verifica si el menú está realmente visible para el usuario.
    /// </summary>
    private static bool IsReallyVisible(Transform root)
    {
        if (root == null) return false;
        if (!root.gameObject.activeInHierarchy) return false;

        // Verificar CanvasGroup (alpha > 0, interactable)
        var canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            if (canvasGroup.alpha < 0.5f) return false; // Más estricto
            if (!canvasGroup.interactable) return false;
            if (canvasGroup.blocksRaycasts == false) return false;
        }

        // Verificar escala (no es 0)
        var scale = root.localScale;
        if (scale.x < 0.5f || scale.y < 0.5f) return false;

        // IMPORTANTE: Verificar que hay sliders activos e interactuables
        // Esto es la mejor forma de saber si el menú de opciones está realmente visible
        var sliders = root.GetComponentsInChildren<UnityEngine.UI.Slider>(false);
        bool hasActiveSlider = false;
        foreach (var slider in sliders)
        {
            if (slider.gameObject.activeInHierarchy && slider.interactable)
            {
                hasActiveSlider = true;
                break;
            }
        }

        if (!hasActiveSlider)
        {
            Plugin.Logger.LogDebug("IsReallyVisible: No active interactable sliders found");
            return false;
        }

        return true;
    }

    public static void SetClosed()
    {
        _isOpen = false;
        _currentOptionsUI = null;
        _lastCloseTime = Time.time;
    }

    /// <summary>
    /// Called by FightMenuOptionsClickPatch to register the OptionsUI it created.
    /// </summary>
    public static void RegisterUI(OptionsUI ui)
    {
        _currentOptionsUI = ui;
        _isOpen = true;
    }

    public static OptionsUI GetCurrentUI() => _currentOptionsUI;
}

[HarmonyPatch(typeof(OptionsDialogController), "OnDisable")]
public static class OptionsDialogHidePatch
{
    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        var currentUI = OptionsDialogShowPatch.GetCurrentUI();
        if (currentUI != null)
        {
            AccessibilityMgr.HideUI(currentUI);
            OptionsDialogShowPatch.SetClosed();
            Plugin.Logger.LogInfo("OptionsUI cerrada");
        }
    }
}
