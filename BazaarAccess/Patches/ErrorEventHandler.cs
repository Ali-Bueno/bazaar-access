using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarGameClient.Domain.Models.Cards;

namespace BazaarAccess.Patches;

/// <summary>
/// Handles error/rejection events from the game.
/// </summary>
public static class ErrorEventHandler
{
    /// <summary>
    /// When there's no space for an item.
    /// </summary>
    public static void OnNotEnoughSpace(Card card)
    {
        string name = card != null ? ItemReader.GetCardName(card) : "item";
        Plugin.Logger.LogInfo($"NotEnoughSpace: {name}");
        TolkWrapper.Speak($"No space for {name}");
    }

    /// <summary>
    /// When there's not enough gold to buy.
    /// </summary>
    public static void OnCantAffordCard(Card card)
    {
        string name = card != null ? ItemReader.GetCardName(card) : "item";
        int price = card != null ? ItemReader.GetBuyPrice(card) : 0;
        Plugin.Logger.LogInfo($"CantAffordCard: {name} costs {price}");
        TolkWrapper.Speak($"Cannot afford {name}");
    }

    /// <summary>
    /// When the game rejects a sale because the item is unsellable.
    /// </summary>
    public static void OnUnsellableItemAttempt(Card card)
    {
        string name = card != null ? ItemReader.GetCardName(card) : "item";
        Plugin.Logger.LogInfo($"UnsellableItemAttempt: {name}");
        TolkWrapper.Speak($"{name} cannot be sold");
    }
}
