using System;
using System.Reflection;
using HarmonyLib;

namespace BazaarAccess.Screens;

/// <summary>
/// The game's navigation menu (NavigationMenuController, internal) shared by the out-of-run scenes.
/// </summary>
public static class NavigationMenuHelper
{
    /// <summary>Returns to the main menu through the navigation menu's Home button. False if it is not available.</summary>
    public static bool TryGoHome()
    {
        try
        {
            var menuType = AccessTools.TypeByName("TheBazaar.UI.Menu.NavigationMenuController");
            var menu = menuType != null ? UnityEngine.Object.FindObjectOfType(menuType) : null;
            var homeProp = menuType?.GetProperty("HomeButton", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(homeProp?.GetValue(menu, null) is BazaarButtonController button) || !button.isActiveAndEnabled) return false;

            button.onClick.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"NavigationMenuHelper.TryGoHome: {e.Message}");
            return false;
        }
    }
}
