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
    private const float COOLDOWN = 0.5f; // Medio segundo de cooldown

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        // Cooldown para evitar reabrir inmediatamente después de cerrar
        if (Time.time - _lastCloseTime < COOLDOWN)
        {
            return;
        }

        // Verificar si el diálogo está realmente visible
        if (!IsReallyVisible(__instance.transform))
        {
            return;
        }

        // Evitar abrir múltiples veces
        if (_isOpen) return;
        _isOpen = true;

        // Crear UI accesible
        _currentOptionsUI = new OptionsUI(__instance.transform);
        AccessibilityMgr.ShowUI(_currentOptionsUI);

        Plugin.Logger.LogInfo("OptionsUI abierta");
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
            if (canvasGroup.alpha < 0.1f) return false;
            if (!canvasGroup.interactable) return false;
        }

        // Verificar escala (no es 0)
        var scale = root.localScale;
        if (scale.x < 0.1f || scale.y < 0.1f) return false;

        // Verificar que hay sliders activos (siempre hay sliders de audio en opciones)
        var sliders = root.GetComponentsInChildren<UnityEngine.UI.Slider>(false);
        foreach (var slider in sliders)
        {
            if (slider.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    public static void SetClosed()
    {
        _isOpen = false;
        _currentOptionsUI = null;
        _lastCloseTime = Time.time;
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
