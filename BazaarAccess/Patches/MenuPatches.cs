using System.Collections.Generic;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Screens;
using BazaarAccess.UI;
using HarmonyLib;
using TheBazaar;
using TheBazaar.Feature.Chest.Scene;
using TheBazaar.Feature.Chest.Scene.States;
using TheBazaar.UI;
using UnityEngine;

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
        Plugin.Logger.LogInfo("CollectionUIController.Start - Creating accessible collection screen");
        var screen = new CollectionScreen(__instance.transform, __instance);
        AccessibilityMgr.SetScreen(screen);
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

// ===== CHEST REWARDS =====

/// <summary>
/// Shows chest rewards after a chest is opened.
/// We hook into the CollectionsPopulated event which fires after the chest animation.
/// </summary>
public static class ChestRewardsPatch
{
    private static bool _isSubscribed = false;
    private static ChestSceneController _currentController = null;
    private static object _collectionsPopulatedEvent = null;
    private static System.Reflection.MethodInfo _addListenerMethod = null;
    private static System.Reflection.MethodInfo _removeListenerMethod = null;
    private static System.Action _onChestOpenedDelegate = null;

    public static void Subscribe(ChestSceneController controller)
    {
        if (_isSubscribed) return;

        _currentController = controller;

        try
        {
            // Access internal Events class via reflection
            var eventsType = typeof(TheBazaar.Data).Assembly.GetType("TheBazaar.Events");
            if (eventsType == null)
            {
                Plugin.Logger.LogError("ChestRewardsPatch: Could not find Events type");
                return;
            }

            var collectionsPopulatedField = eventsType.GetField("CollectionsPopulated",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (collectionsPopulatedField == null)
            {
                Plugin.Logger.LogError("ChestRewardsPatch: Could not find CollectionsPopulated field");
                return;
            }

            _collectionsPopulatedEvent = collectionsPopulatedField.GetValue(null);
            if (_collectionsPopulatedEvent == null)
            {
                Plugin.Logger.LogError("ChestRewardsPatch: CollectionsPopulated event is null");
                return;
            }

            var eventType = _collectionsPopulatedEvent.GetType();
            _addListenerMethod = eventType.GetMethod("AddListener",
                new System.Type[] { typeof(System.Action) });
            _removeListenerMethod = eventType.GetMethod("RemoveListener",
                new System.Type[] { typeof(System.Action) });

            if (_addListenerMethod == null)
            {
                Plugin.Logger.LogError("ChestRewardsPatch: Could not find AddListener method");
                return;
            }

            _onChestOpenedDelegate = OnChestOpened;
            _addListenerMethod.Invoke(_collectionsPopulatedEvent, new object[] { _onChestOpenedDelegate });

            _isSubscribed = true;
            Plugin.Logger.LogInfo("ChestRewardsPatch: Subscribed to CollectionsPopulated");
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"ChestRewardsPatch: Error subscribing: {e.Message}");
        }
    }

    public static void Unsubscribe()
    {
        if (!_isSubscribed) return;

        try
        {
            if (_removeListenerMethod != null && _collectionsPopulatedEvent != null && _onChestOpenedDelegate != null)
            {
                _removeListenerMethod.Invoke(_collectionsPopulatedEvent, new object[] { _onChestOpenedDelegate });
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"ChestRewardsPatch: Error unsubscribing: {e.Message}");
        }

        _isSubscribed = false;
        _currentController = null;
        _collectionsPopulatedEvent = null;
        _onChestOpenedDelegate = null;
        Plugin.Logger.LogInfo("ChestRewardsPatch: Unsubscribed from CollectionsPopulated");
    }

    private static void OnChestOpened()
    {
        Plugin.Logger.LogInfo("ChestRewardsPatch: Chest opened, showing rewards");

        if (_currentController == null || _currentController.playerChestInventory == null)
        {
            TolkWrapper.Speak("Chest opened");
            return;
        }

        var rewards = _currentController.playerChestInventory.openedChestRewards;
        if (rewards == null || rewards.Count == 0)
        {
            TolkWrapper.Speak("Chest opened");
            return;
        }

        // Create and show the rewards UI
        var ui = new ChestRewardsUI(_currentController.transform, rewards);
        AccessibilityMgr.ShowUI(ui);
    }
}

/// <summary>
/// Subscribe to chest events when entering the chest scene.
/// </summary>
[HarmonyPatch(typeof(ChestSceneController), "Start")]
public static class ChestSceneSubscribePatch
{
    static void Postfix(ChestSceneController __instance)
    {
        ChestRewardsPatch.Subscribe(__instance);
    }
}

/// <summary>
/// Unsubscribe when leaving the chest scene.
/// </summary>
[HarmonyPatch(typeof(ChestSceneController), "OnDestroy")]
public static class ChestSceneUnsubscribePatch
{
    static void Postfix()
    {
        ChestRewardsPatch.Unsubscribe();
    }
}
