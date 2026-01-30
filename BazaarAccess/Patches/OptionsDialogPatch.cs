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

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        // Evitar abrir múltiples veces
        if (_isOpen) return;

        // Usar coroutine para esperar a que el diálogo esté listo
        Plugin.Instance.StartCoroutine(CreateOptionsUIDelayed(__instance.transform));
    }

    private static System.Collections.IEnumerator CreateOptionsUIDelayed(Transform root)
    {
        // Esperar un poco para que el UI esté completamente visible
        yield return new WaitForSeconds(0.15f);

        // Double-check que no se haya abierto mientras esperábamos
        if (_isOpen) yield break;

        if (root != null && root.gameObject.activeInHierarchy)
        {
            _isOpen = true;
            _currentOptionsUI = new OptionsUI(root);
            AccessibilityMgr.ShowUI(_currentOptionsUI);
            Plugin.Logger.LogInfo("OptionsUI abierta");
        }
    }

    public static void SetClosed()
    {
        _isOpen = false;
        _currentOptionsUI = null;
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
