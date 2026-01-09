using BazaarAccess.Accessibility;
using BazaarAccess.Screens;
using HarmonyLib;
using TheBazaar.UI;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en ViewController.SwitchView para detectar cambios de vista.
/// </summary>
[HarmonyPatch(typeof(ViewController), nameof(ViewController.SwitchView), typeof(string))]
public static class ViewControllerPatch
{
    static void Postfix(ViewController __instance, string name)
    {
        var currentView = __instance.CurrentView;
        if (currentView == null) return;

        string viewName = currentView.ViewName;
        if (string.IsNullOrEmpty(viewName))
            viewName = currentView.GetType().Name;

        Plugin.Logger.LogInfo($"ViewController.SwitchView: {viewName}");

        // Crear la pantalla accesible apropiada
        IAccessibleScreen screen = CreateScreen(viewName, currentView.transform);

        if (screen != null)
        {
            AccessibilityMgr.SetScreen(screen);
        }
    }

    /// <summary>
    /// Crea la pantalla accesible para cada vista del juego.
    /// </summary>
    private static IAccessibleScreen CreateScreen(string viewName, Transform root)
    {
        switch (viewName.ToLower())
        {
            case "heroselect":
            case "heroselectview":
                return new HeroSelectScreen(root);

            case "mainmenu":
            case "mainmenuview":
                return new MainMenuScreen(root);

            default:
                Plugin.Logger.LogInfo($"Vista sin pantalla accesible: {viewName}");
                return null;
        }
    }
}
