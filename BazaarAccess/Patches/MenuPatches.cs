using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Screens;
using HarmonyLib;
using TheBazaar;
using TheBazaar.Feature.Chest.Scene;
using TheBazaar.UI;

namespace BazaarAccess.Patches;

/// <summary>
/// Patches for detecting when various menus are opened.
/// </summary>

// ===== CHEST SCENE =====

/// <summary>
/// Detects when the chest scene is loaded.
/// </summary>
[HarmonyPatch(typeof(ChestSceneController), "Start")]
public static class ChestSceneStartPatch
{
    static void Postfix(ChestSceneController __instance)
    {
        Plugin.Logger.LogInfo("ChestSceneController.Start - Creating accessible chest screen");
        var screen = new ChestSceneScreen(__instance.transform, __instance);
        AccessibilityMgr.SetScreen(screen);
    }
}

// Note: ChangeState is inherited from BaseFiniteStateMachine and cannot be patched directly.
// State changes are announced via the ChestSceneScreen when user performs actions.

// ===== COLLECTIONS SCREEN =====

/// <summary>
/// Detects when the collection UI controller starts.
/// This is the main entry point for the collection scene.
/// </summary>
[HarmonyPatch(typeof(TheBazaar.UI.CollectionUIController), "Start")]
public static class CollectionUIControllerStartPatch
{
    static void Postfix(TheBazaar.UI.CollectionUIController __instance)
    {
        // The collection scene is reused for chest-opening (entered from the battle pass, hero
        // select, main menu chests or purchase-chests with SetStartInCollections(false)). In that
        // mode the collection wheel isn't shown — the chest scene is (handled by ChestSceneStartPatch).
        // Our CollectionScreen loads its categories straight from CollectionManager regardless of the
        // visible UI, so creating it here made the mod read collection items (skins, card backs) over
        // the chest flow. Only take over when the player is actually browsing collections.
        if (!IsStartingInCollections())
        {
            Plugin.Logger.LogInfo("CollectionUIController.Start - chest mode, leaving it to the chest screen");
            return;
        }

        Plugin.Logger.LogInfo("CollectionUIController.Start - Creating accessible collection screen");
        var screen = new CollectionScreen(__instance.transform, __instance);
        AccessibilityMgr.SetScreen(screen);
    }

    /// <summary>
    /// Reads the game's static _startInCollections flag (set true only for the collection-browsing
    /// entry point, false for every chest-opening path).
    /// </summary>
    private static bool IsStartingInCollections()
    {
        try
        {
            var field = typeof(TheBazaar.UI.CollectionUIController).GetField("_startInCollections",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return field != null && (bool)field.GetValue(null);
        }
        catch (System.Exception e)
        {
            // Fail open: reading collections is a better fallback than staying silent.
            Plugin.Logger.LogWarning($"IsStartingInCollections failed, assuming collections: {e.Message}");
            return true;
        }
    }
}

// ===== BATTLE PASS / SEASON PASS =====

/// <summary>
/// Detects when the battle pass view is shown.
/// </summary>
[HarmonyPatch(typeof(BattlePassView), "Awake")]
public static class BattlePassViewAwakePatch
{
    static void Postfix(BattlePassView __instance)
    {
        Plugin.Logger.LogInfo("BattlePassView.Awake - Creating accessible battle pass screen");
        var screen = new BattlePassScreen(__instance.transform, __instance);
        AccessibilityMgr.SetScreen(screen);
    }
}

/// <summary>
/// Announce tier unlock animations.
/// </summary>
[HarmonyPatch(typeof(BattlePassTier), nameof(BattlePassTier.UnlockTier))]
public static class BattlePassTierUnlockPatch
{
    static void Postfix(BattlePassTier __instance)
    {
        int tierNum = __instance.TierNumber;
        TolkWrapper.Speak($"Tier {tierNum} unlocked");
    }
}

// ===== MARKETPLACE =====

/// <summary>
/// Detects when marketplace screen is shown.
/// </summary>
[HarmonyPatch(typeof(MarketplaceScreenController), "Awake")]
public static class MarketplaceScreenAwakePatch
{
    static void Postfix(MarketplaceScreenController __instance)
    {
        Plugin.Logger.LogInfo("MarketplaceScreenController.Awake - Marketplace opened");
        // TODO: Create MarketplaceScreen when implemented
        TolkWrapper.Speak("Marketplace");
    }
}

// ===== PROFILE / CAREER =====

/// <summary>
/// Detects when profile career view is shown.
/// </summary>
[HarmonyPatch(typeof(ProfileCareerViewController), "Awake")]
public static class ProfileCareerAwakePatch
{
    static void Postfix(ProfileCareerViewController __instance)
    {
        Plugin.Logger.LogInfo("ProfileCareerViewController.Awake - Profile opened");
        // TODO: Create ProfileScreen when implemented
        TolkWrapper.Speak("Player Profile");
    }
}

// Note: Chest rewards are now handled directly in ChestSceneScreen
// after OpenChest() and ClickMultiOpen() complete their animations.
