using System;
using System.Collections.Generic;
using BazaarGameClient.Domain.Models.Cards;
using BazaarAccess.Core;
using TheBazaar;
using TheBazaar.Localization;
using TheBazaar.Utilities;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Describes tag and keyword properties for cards (used by the I key).
/// </summary>
internal static class PropertyDescriber
{
    public static List<string> GetTagDescriptions(Card card)
    {
        var descriptions = new List<string>();
        if (card == null || card.Tags == null || card.Tags.Count == 0)
            return descriptions;

        foreach (var tag in card.Tags)
        {
            if (!CardProperties.IsRelevantTag(tag))
                continue;

            string tagName = tag.ToString();
            string description = GetLegendDescription(tagName);
            descriptions.Add(description != null
                ? $"{tagName}: {description}"
                : $"{tagName}: No description available");
        }

        return descriptions;
    }

    public static List<string> GetKeywordDescriptions(Card card)
    {
        var descriptions = new List<string>();
        if (card == null) return descriptions;

        string fullDesc = CardProperties.GetFullDescription(card);
        if (string.IsNullOrEmpty(fullDesc)) return descriptions;

        var keywordsToCheck = new[]
        {
            "Damage", "Healing", "Shield", "Burn", "Poison", "Regen", "Joy",
            "Slow", "Haste", "Freeze", "Charge",
            "Crit Chance", "Multicast", "Lifesteal", "Flying",
            "Ammo", "Cooldown", "Income", "Upgrade",
            "Heated", "Chilled",
            "Enchant", "Transform", "Unsellable"
        };

        var addedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywordsToCheck)
        {
            if (fullDesc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !addedKeywords.Contains(keyword))
            {
                string description = GetLegendDescription(keyword);
                if (description != null)
                {
                    descriptions.Add($"{keyword}: {description}");
                    addedKeywords.Add(keyword);
                }
            }
        }

        return descriptions;
    }

    public static List<string> GetAllPropertyDescriptions(Card card)
    {
        var allDescriptions = new List<string>();
        allDescriptions.AddRange(GetTagDescriptions(card));
        allDescriptions.AddRange(GetKeywordDescriptions(card));
        return allDescriptions;
    }

    /// <summary>
    /// Looks up a keyword in the game's tooltip legend dictionary.
    /// Returns the localized description or null.
    /// </summary>
    private static string GetLegendDescription(string keyword)
    {
        if (Data.TooltipLegendStringDictionary.TryGetValue(keyword, out var symbol))
        {
            if (symbol != null && !string.IsNullOrEmpty(symbol.Keyword))
            {
                string description = new LocalizableText(symbol.Keyword).GetLocalizedText();
                if (!string.IsNullOrEmpty(description))
                {
                    return TextHelper.CleanText(description);
                }
            }
        }

        return null;
    }
}
