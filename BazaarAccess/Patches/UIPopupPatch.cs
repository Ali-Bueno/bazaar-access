using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.UI;
using HarmonyLib;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Most popups derive UIScripts.UIPopup in the current build; route the ones we support to their accessible UI.
/// </summary>
[HarmonyPatch]
public static class UIPopupShowPatch
{
    private static readonly Dictionary<MonoBehaviour, IAccessibleUI> OpenUIs = new Dictionary<MonoBehaviour, IAccessibleUI>();

    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UIScripts.UIPopup"), "Show");

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance)
    {
        if (__instance == null) return;
        if (PendingClose.Remove(__instance)) return; // shown again while switching screens: keep its UI
        if (OpenUIs.ContainsKey(__instance)) return;

        try
        {
            IAccessibleUI ui = Create(__instance);
            if (ui == null)
            {
                Plugin.Logger.LogInfo($"UIPopup.Show: {__instance.GetType().Name} (no accessible UI)");
                return;
            }

            OpenUIs[__instance] = ui;
            AccessibilityMgr.ShowUI(ui);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"UIPopupShowPatch error: {ex.Message}");
        }
    }

    private static IAccessibleUI Create(MonoBehaviour popup)
    {
        switch (popup)
        {
            case GenericPopup generic:
                return GenericPopupUI.CreateFromPopup(generic);
        }

        switch (popup.GetType().Name)
        {
            case "HeroSelectPopupController":
                return new HeroSelectUI(popup);
            case "PurchaseGemsView":
            case "PurchaseChestsView":
                return new PurchasePanelUI(popup);
            case "GemPurchasePromptComponent":
                return new GemPromptUI(popup);
            case "LoadoutPopupController":
                return new LoadoutUI(popup);
            case "ConfirmationComponent":
                return new PurchaseConfirmationUI(popup);
            default:
                return null;
        }
    }

    // Store flows hide and re-show the same popup while switching screens; closing its UI at once would
    // briefly announce the screen underneath ("Store, Main menu, Store").
    private const float ReshowGraceSeconds = 0.3f;
    private static readonly HashSet<MonoBehaviour> PendingClose = new HashSet<MonoBehaviour>();

    internal static void Close(MonoBehaviour popup)
    {
        if (popup == null || !OpenUIs.ContainsKey(popup)) return;

        // Generic popups reuse one instance for a different message: rebuild those right away.
        if (popup is GenericPopup)
        {
            CloseNow(popup);
            return;
        }

        if (PendingClose.Add(popup)) Plugin.Instance.StartCoroutine(CloseAfterGrace(popup));
    }

    private static System.Collections.IEnumerator CloseAfterGrace(MonoBehaviour popup)
    {
        yield return new WaitForSecondsRealtime(ReshowGraceSeconds);
        if (PendingClose.Remove(popup)) CloseNow(popup);
    }

    private static void CloseNow(MonoBehaviour popup)
    {
        if (!OpenUIs.TryGetValue(popup, out var ui)) return;
        OpenUIs.Remove(popup);
        AccessibilityMgr.HideUI(ui);
    }
}

[HarmonyPatch]
public static class UIPopupHidePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UIScripts.UIPopup"), "Hide");

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance) => UIPopupShowPatch.Close(__instance);
}

/// <summary>
/// Store flows activate some SequencePopups (e.g. the purchase confirmation) without calling Show:
/// treat enabling / disabling as show / hide too (the Show hook ignores popups it already handles).
/// </summary>
[HarmonyPatch]
public static class SequencePopupEnablePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UIScripts.SequencePopup"), "OnEnable");

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance) => UIPopupShowPatch.Postfix(__instance);
}

[HarmonyPatch]
public static class SequencePopupDisablePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("UIScripts.SequencePopup"), "OnDisable");

    [HarmonyPostfix]
    public static void Postfix(MonoBehaviour __instance) => UIPopupShowPatch.Close(__instance);
}
