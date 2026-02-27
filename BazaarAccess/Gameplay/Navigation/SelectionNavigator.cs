using System;
using System.Collections.Generic;
using System.Linq;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Manages selection data (shop items, encounters, skills, loot rewards).
/// </summary>
public class SelectionNavigator
{
    private List<NavItem> _selectionItems = new List<NavItem>();

    // ===============================================
    // PROPERTIES
    // ===============================================

    public int Count => _selectionItems.Count;
    public int CardCount => _selectionItems.Count(i => i.Type == NavItemType.Card);
    public bool HasContent => _selectionItems.Count > 0;

    // ===============================================
    // REFRESH
    // ===============================================

    public void Refresh(Func<bool> canReroll, Func<int> getRerollCost, Func<bool> canExit)
    {
        _selectionItems.Clear();
        try
        {
            var selectionSet = Data.CurrentState?.SelectionSet;
            if (selectionSet != null)
            {
                foreach (var id in selectionSet)
                {
                    var card = Data.GetCard(id);
                    if (card != null) _selectionItems.Add(NavItem.FromCard(card));
                }
            }

            if (canReroll())
            {
                _selectionItems.Add(NavItem.CreateReroll(getRerollCost()));
            }

            if (canExit())
            {
                _selectionItems.Add(NavItem.CreateExit());
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"RefreshSelection error: {ex.Message}");
        }
    }

    // ===============================================
    // DATA ACCESS
    // ===============================================

    public NavItem GetNavItem(int index)
    {
        if (index < 0 || index >= _selectionItems.Count) return null;
        return _selectionItems[index];
    }

    public Card GetCard(int index)
    {
        if (index < _selectionItems.Count)
        {
            var navItem = _selectionItems[index];
            return navItem.Type == NavItemType.Card ? navItem.Card : null;
        }
        return null;
    }

    // ===============================================
    // DESCRIPTION HELPERS
    // ===============================================

    public static bool IsEncounterCard(Card card) =>
        card.Type == ECardType.CombatEncounter ||
        card.Type == ECardType.EventEncounter ||
        card.Type == ECardType.PedestalEncounter ||
        card.Type == ECardType.EncounterStep ||
        card.Type == ECardType.PvpEncounter;

    public static bool IsSelectionFree()
    {
        try { return Data.CurrentState?.SelectionContextRules?.SelectionIsFree ?? false; }
        catch { return false; }
    }

    public string GetSelectionTypeName(ERunState state)
    {
        var cards = _selectionItems.Where(i => i.Type == NavItemType.Card).ToList();
        if (cards.Count == 0) return "options";

        if (state == ERunState.Loot) return "rewards";

        var firstCard = cards[0].Card;
        if (IsEncounterCard(firstCard)) return "encounters";
        if (firstCard.Type == ECardType.Skill) return "skills";
        return "items";
    }

    /// <summary>
    /// Gets a description of a card suitable for announcement.
    /// Works for selection, board, stash, and skills sections.
    /// </summary>
    public static string GetCardDescription(Card card, NavigationSection section, bool isFree)
    {
        if (section == NavigationSection.Selection)
        {
            if (IsEncounterCard(card))
                return ItemReader.GetEncounterInfo(card);

            string shopName = ItemReader.GetCardName(card);
            if (ItemReader.IsQuestItem(card))
            {
                string questProgress = ItemReader.GetQuestProgress(card);
                shopName = questProgress != null ? $"{questProgress}: {shopName}" : $"Quest: {shopName}";
            }
            string shopSize = card.Type != ECardType.Skill ? ItemReader.GetSizeName(card) : null;
            string shopTier = ItemReader.GetTierName(card);

            if (isFree)
            {
                if (card.Type == ECardType.Skill)
                    return !string.IsNullOrEmpty(shopTier) ? $"{shopName}, {shopTier.ToLower()}" : shopName;

                if (!string.IsNullOrEmpty(shopSize) && !string.IsNullOrEmpty(shopTier))
                    return $"{shopName}, {shopSize}, {shopTier.ToLower()}";
                if (!string.IsNullOrEmpty(shopSize))
                    return $"{shopName}, {shopSize}";
                return shopName;
            }

            int price = ItemReader.GetBuyPrice(card);
            if (price > 0)
            {
                if (card.Type == ECardType.Skill)
                    return !string.IsNullOrEmpty(shopTier) ? $"{shopName}, {shopTier.ToLower()}, {price} gold" : $"{shopName}, {price} gold";

                if (!string.IsNullOrEmpty(shopSize) && !string.IsNullOrEmpty(shopTier))
                    return $"{shopName}, {shopSize}, {shopTier.ToLower()}, {price} gold";
                if (!string.IsNullOrEmpty(shopSize))
                    return $"{shopName}, {shopSize}, {price} gold";
                return $"{shopName}, {price} gold";
            }
            return shopName;
        }

        // Board/Stash/Skills
        string name = ItemReader.GetCardName(card);

        if (card.Type == ECardType.Skill)
        {
            string tier = ItemReader.GetTierName(card);
            return !string.IsNullOrEmpty(tier) ? $"{name}, {tier.ToLower()}" : name;
        }

        string size = ItemReader.GetSizeName(card);
        string itemTier = ItemReader.GetTierName(card);

        if (!string.IsNullOrEmpty(size) && !string.IsNullOrEmpty(itemTier))
            return $"{name}, {size}, {itemTier.ToLower()}";
        if (!string.IsNullOrEmpty(size))
            return $"{name}, {size}";
        if (!string.IsNullOrEmpty(itemTier))
            return $"{name}, {itemTier.ToLower()}";
        return name;
    }
}
