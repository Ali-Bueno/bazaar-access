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
        if (__instance == null || OpenUIs.ContainsKey(__instance)) return;

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
            default:
                return null;
        }
    }

    internal static void Close(MonoBehaviour popup)
    {
        if (popup == null || !OpenUIs.TryGetValue(popup, out var ui)) return;
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
