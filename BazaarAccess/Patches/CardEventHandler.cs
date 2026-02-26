using System.Collections.Generic;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.GameSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Handles card transaction and modification events.
/// </summary>
public static class CardEventHandler
{
    public static void OnCardPurchased(GameSimEventCardPurchased evt)
    {
        Plugin.Logger.LogInfo($"Card purchased: {evt.InstanceId}");
        // Only refresh - ActionHelper already announced "Bought X"
        StateChangePatch.TriggerRefresh();
    }

    public static void OnCardSold(GameSimEventCardSold evt)
    {
        Plugin.Logger.LogInfo($"Card sold: {evt.InstanceId}");
        // Only refresh - ActionHelper already announced "Sold X"
        StateChangePatch.TriggerRefresh();
    }

    public static void OnSkillEquipped(GameSimEventPlayerSkillEquipped evt)
    {
        // Only refresh if skill is for the player, not the opponent
        if (evt.Owner == ECombatantId.Player)
        {
            Plugin.Logger.LogInfo($"Skill equipped: {evt.InstanceId}");
            // Small delay to ensure Player.Skills has been updated
            Plugin.Instance.StartCoroutine(DelayedRefreshAfterSkillEquipped());
        }
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterSkillEquipped()
    {
        // Short delay - Player.Skills should already be updated
        yield return new WaitForSeconds(0.1f);
        StateChangePatch.TriggerRefresh();
    }

    public static void OnCardDisposed(List<Card> cards)
    {
        Plugin.Logger.LogInfo($"Cards disposed: {cards?.Count ?? 0}");
        // Only refresh - selection/transition already announced
        StateChangePatch.TriggerRefresh();
    }

    public static void OnCardSelected()
    {
        Plugin.Logger.LogInfo("Card selected - triggering delayed refresh");
        // Use coroutine to wait for the game to process the selection
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterSelection());
    }

    public static void OnItemPurchased(Card card)
    {
        string cardName = card?.ToString() ?? "unknown";
        Plugin.Logger.LogInfo($"Item purchased/selected: {cardName} - triggering delayed refresh");
        // Use coroutine to wait for the game to process the selection
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterSelection());
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterSelection()
    {
        // Wait a bit for the game to process the selection
        yield return new WaitForSeconds(0.3f);
        // Only refresh, game events will handle announcement with debounce
        StateChangePatch.TriggerRefresh();
    }

    public static void OnBoardChanged()
    {
        Plugin.Logger.LogInfo("Board changed");
        StateChangePatch.TriggerRefresh();
    }

    /// <summary>
    /// When a card is enchanted.
    /// Clears the detail cache so updated stats are read correctly.
    /// </summary>
    public static void OnCardEnchanted(GameSimEventCardEnchanted evt)
    {
        Plugin.Logger.LogInfo($"Card enchanted: {evt.InstanceId}, type={evt.EnchantmentType}");

        // Clear the detail cache in GameplayNavigator
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.ClearDetailCache();

        // Refresh to pick up new attributes
        StateChangePatch.TriggerRefresh();
    }

    /// <summary>
    /// When a card is upgraded (tier increased).
    /// Clears the detail cache so updated stats are read correctly.
    /// </summary>
    public static void OnCardUpgraded(GameSimEventCardUpgraded evt)
    {
        Plugin.Logger.LogInfo($"Card upgraded: {evt.InstanceId}, newTier={evt.NewTier}");

        // Clear the detail cache in GameplayNavigator
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.ClearDetailCache();

        // Refresh to pick up new attributes
        StateChangePatch.TriggerRefresh();
    }
}
