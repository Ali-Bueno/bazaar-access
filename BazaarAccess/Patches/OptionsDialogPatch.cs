using System.Collections;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Patches;

/// <summary>
/// Detects the options dialog opening. Hooks UIPopup.Show() (type-filtered) instead of OnEnable
/// to avoid the cached dialog's spurious load-time enable/disable. See Progress.md
/// ("Options dialog rework") for the double-open root cause.
/// </summary>
[HarmonyPatch]
public static class OptionsDialogShowPatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UIPopup"), "Show");

    private static OptionsUI _currentOptionsUI;
    private static bool _isOpen = false;
    private static bool _pendingOpen = false;
    private static float _lastCloseTime = 0f;

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        // Show() is virtual on UIPopup and shared by all popups; we only want the options dialog.
        if (__instance == null || __instance.GetType().Name != "OptionsDialogController") return;

        // Cooldown to avoid reopening right after closing.
        if (Time.time - _lastCloseTime < 0.3f)
        {
            return;
        }

        // Avoid opening (or trying to) more than once.
        if (_isOpen || _pendingOpen) return;

        if (IsReallyVisible(__instance.transform))
        {
            Open(__instance);
            return;
        }

        // Still animating in: retry briefly instead of giving up (which forced reopening).
        Plugin.Instance.StartCoroutine(WaitAndOpen(__instance));
    }

    private static IEnumerator WaitAndOpen(MonoBehaviour instance)
    {
        _pendingOpen = true;
        float deadline = Time.time + 1.5f;

        while (Time.time < deadline)
        {
            if (_isOpen) break;
            if (instance == null || !instance.gameObject.activeInHierarchy) break;

            if (IsReallyVisible(instance.transform))
            {
                Open(instance);
                break;
            }

            yield return null;
        }

        _pendingOpen = false;
    }

    private static void Open(MonoBehaviour instance)
    {
        _isOpen = true;
        _currentOptionsUI = new OptionsUI(instance.transform);
        AccessibilityMgr.ShowUI(_currentOptionsUI);
        Plugin.Logger.LogInfo("OptionsUI opened (from UIPopup.Show)");
    }

    // True once the dialog is actually visible and loaded (not just mid-animation).
    private static bool IsReallyVisible(Transform root)
    {
        if (root == null) return false;
        if (!root.gameObject.activeInHierarchy) return false;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            if (canvasGroup.alpha < 0.5f) return false;
            if (!canvasGroup.interactable) return false;
            if (canvasGroup.blocksRaycasts == false) return false;
        }

        var scale = root.localScale;
        if (scale.x < 0.5f || scale.y < 0.5f) return false;

        // At least one active, interactable setting control = the dialog finished loading.
        // Accept slider/toggle/dropdown so we don't rely on the default section having a slider.
        if (!HasActiveInteractable<Slider>(root)
            && !HasActiveInteractable<Toggle>(root)
            && !HasActiveInteractable<TMP_Dropdown>(root))
        {
            Plugin.Logger.LogDebug("IsReallyVisible: no active setting controls yet");
            return false;
        }

        return true;
    }

    private static bool HasActiveInteractable<T>(Transform root) where T : Selectable
    {
        foreach (var control in root.GetComponentsInChildren<T>(false))
        {
            if (control.gameObject.activeInHierarchy && control.IsInteractable())
                return true;
        }
        return false;
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

[HarmonyPatch]
public static class OptionsDialogHidePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("OptionsDialogController"), "OnDisable");

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
