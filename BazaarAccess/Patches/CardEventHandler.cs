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
    // Names of cards the mod has described, for events that arrive after the game removed the card (sales).
    private const int MaxRememberedNames = 200;
    private static readonly Dictionary<string, string> RememberedNames = new Dictionary<string, string>();

    // Resuming a run re-equips its skills a few seconds after the board loads (seen ~3 s in testing).
    private const float RunLoadGraceSeconds = 10f;

    // Items/skills just announced as acquired, so the skill-equipped event that follows a skill purchase stays quiet.
    private static readonly HashSet<string> AnnouncedAcquisitions = new HashSet<string>();

    /// <summary>Remembers a card's name so a later sale can still be announced.</summary>
    public static void RememberName(Card card)
    {
        if (card == null) return;
        if (RememberedNames.Count >= MaxRememberedNames) RememberedNames.Clear();
        RememberedNames[card.InstanceId.Value] = ItemReader.GetCardName(card);
    }

    public static void OnCardPurchased(GameSimEventCardPurchased evt)
    {
        Plugin.Logger.LogInfo($"Card purchased: {evt.InstanceId}");
        // Choosing an encounter or event path is also reported as a purchase: only items and skills are announced.
        var bought = Data.GetCard(evt.InstanceId);
        if (evt.CombatantId == ECombatantId.Player && bought != null && (bought.Type == ECardType.Item || bought.Type == ECardType.Skill))
        {
            RememberName(bought);
            AnnouncedAcquisitions.Add(evt.InstanceId);
            string name = ItemReader.GetCardName(bought);
            Record(evt.InstanceId, evt.BuyPrice > 0 ? Loc.T("action.bought", name, evt.BuyPrice) : Loc.T("action.acquired", name));
        }
        StateChangePatch.TriggerRefresh();
    }

    public static void OnCardSold(GameSimEventCardSold evt)
    {
        Plugin.Logger.LogInfo($"Card sold: {evt.InstanceId}");
        string name = CardName(evt.InstanceId);
        if (name != null) Record(evt.InstanceId, Loc.T("action.sold", name, evt.SellPrice));
        StateChangePatch.TriggerRefresh();
    }

    private static string CardName(string id)
    {
        var card = Data.GetCard(id);
        if (card != null) return ItemReader.GetCardName(card);
        return RememberedNames.TryGetValue(id, out var name) ? name : null;
    }

    // Always kept in the history; spoken only when the mod did not send it (the game's controller navigation or mouse).
    private static void Record(string id, string text)
    {
        MessageBuffer.Add(text);
        if (!ActionHelper.ConsumeSentTransaction(id)) TolkWrapper.Speak(text);
    }

    public static void OnSkillEquipped(GameSimEventPlayerSkillEquipped evt)
    {
        // Only refresh if skill is for the player, not the opponent
        if (evt.Owner == ECombatantId.Player)
        {
            Plugin.Logger.LogInfo($"Skill equipped: {evt.InstanceId}");

            // Skills learned without a purchase (e.g. selling a Skill Coupon) were silent.
            string id = evt.InstanceId.ToString();
            var skill = Data.GetCard(id);
            bool loadingRun = Time.realtimeSinceStartup - GameplayPatch.EnteredGameplayAt < RunLoadGraceSeconds;
            bool alreadySpoken = AnnouncedAcquisitions.Remove(id) | ActionHelper.ConsumeSentTransaction(id); // mod's own pick or a purchase
            if (skill != null && !alreadySpoken && !loadingRun)
            {
                string text = Loc.T("action.skill_learned", ItemReader.GetCardName(skill));
                MessageBuffer.Add(text);
                TolkWrapper.Speak(text, interrupt: false);
            }

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
