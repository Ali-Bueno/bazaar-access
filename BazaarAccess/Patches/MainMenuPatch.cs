using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Screens;
using HarmonyLib;
using TheBazaar.UI.MainMenu;

namespace BazaarAccess.Patches;

/// <summary>
/// The main menu and hero select are one scene now (MainMenuBaseView); create the menu screen when it enables.
/// </summary>
[HarmonyPatch]
public static class MainMenuEnablePatch
{
    static MethodBase TargetMethod() => AccessTools.Method(typeof(MainMenuBaseView), "OnEnable");

    [HarmonyPostfix]
    public static void Postfix(MainMenuBaseView __instance)
    {
        Plugin.Logger.LogInfo("MainMenuBaseView enabled");
        AccessibilityMgr.SetScreen(new MainMenuScreen(__instance));
    }
}
