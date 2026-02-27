using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Enchantments;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar.Localization;
using TheBazaar.Utilities;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads basic card metadata: name, tier, size, price, tags, temperature, descriptions.
/// </summary>
internal static class CardProperties
{
    /// <summary>
    /// Tags relevant to the user (item types).
    /// Excludes technical tags like Unsellable, Unstashable, etc.
    /// </summary>
    private static readonly HashSet<ECardTag> RelevantTags = new HashSet<ECardTag>
    {
        ECardTag.Weapon,
        ECardTag.Property,
        ECardTag.Food,
        ECardTag.Potion,
        ECardTag.Tool,
        ECardTag.Vehicle,
        ECardTag.Aquatic,
        ECardTag.Friend,
        ECardTag.Core,
        ECardTag.Ray,
        ECardTag.Dinosaur,
        ECardTag.Apparel,
        ECardTag.Toy,
        ECardTag.Tech,
        ECardTag.Dragon,
        ECardTag.Ingredient,
        ECardTag.Relic,
        ECardTag.Reagent,
        ECardTag.Map,
        ECardTag.Key,
        ECardTag.Drone,
        ECardTag.Loot
    };

    public static string GetCardName(Card card)
    {
        if (card == null) return "Empty";

        var template = card.Template;
        string baseName = string.Empty;

        if (template?.Localization?.Title != null)
        {
            baseName = TextResolver.GetLocalizedText(template.Localization.Title);
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = template?.InternalName ?? "Unknown";
        }

        // Prepend enchantment name if enchanted
        if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
        {
            string enchantName = GetEnchantmentName(itemCard.Enchantment.Value);
            if (!string.IsNullOrEmpty(enchantName))
            {
                return $"{enchantName} {baseName}";
            }
        }

        return baseName;
    }

    public static string GetEnchantmentName(EEnchantmentType enchantment)
    {
        try
        {
            var locText = new LocalizableText(enchantment.ToString());
            string localized = locText.GetLocalizedText();
            if (!string.IsNullOrEmpty(localized))
                return localized;

            return enchantment.ToString();
        }
        catch
        {
            return enchantment.ToString();
        }
    }

    public static string GetTierName(Card card)
    {
        if (card == null) return string.Empty;
        return GetTierName(card.Tier);
    }

    public static string GetTierName(ETier tier)
    {
        return tier switch
        {
            ETier.Bronze => "Bronze",
            ETier.Silver => "Silver",
            ETier.Gold => "Gold",
            ETier.Diamond => "Diamond",
            ETier.Legendary => "Legendary",
            _ => tier.ToString()
        };
    }

    public static string GetSizeName(Card card)
    {
        var template = card?.Template;
        if (template == null) return "";

        return template.Size switch
        {
            ECardSize.Small => "small",
            ECardSize.Medium => "medium",
            ECardSize.Large => "large",
            _ => ""
        };
    }

    public static int GetBuyPrice(Card card)
    {
        if (card == null) return 0;
        return card.GetAttributeValue(ECardAttributeType.BuyPrice) ?? 0;
    }

    public static int GetSellPrice(Card card)
    {
        if (card == null) return 0;
        return card.GetAttributeValue(ECardAttributeType.SellPrice) ?? 0;
    }

    public static string GetTags(Card card)
    {
        if (card == null || card.Tags == null || card.Tags.Count == 0)
            return string.Empty;

        var relevantTags = card.Tags
            .Where(t => RelevantTags.Contains(t))
            .Select(t => t.ToString())
            .ToList();

        return string.Join(", ", relevantTags);
    }

    /// <summary>
    /// Checks if a tag is in the relevant tags set. Used by PropertyDescriber.
    /// </summary>
    internal static bool IsRelevantTag(ECardTag tag) => RelevantTags.Contains(tag);

    public static string GetTemperatureState(Card card)
    {
        if (card == null) return string.Empty;

        bool isHeated = card.GetAttributeValue(ECardAttributeType.Heated) > 0;
        bool isChilled = card.GetAttributeValue(ECardAttributeType.Chilled) > 0;

        if (isHeated && isChilled)
            return "Heated and Chilled";
        if (isHeated)
            return "Heated";
        if (isChilled)
            return "Chilled";

        return string.Empty;
    }

    public static bool IsHeated(Card card)
    {
        if (card == null) return false;
        return card.GetAttributeValue(ECardAttributeType.Heated) > 0;
    }

    public static bool IsChilled(Card card)
    {
        if (card == null) return false;
        return card.GetAttributeValue(ECardAttributeType.Chilled) > 0;
    }

    public static string GetFlavorText(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        if (template?.Localization?.FlavorText != null)
        {
            return TextResolver.GetLocalizedText(template.Localization.FlavorText);
        }

        return string.Empty;
    }

    public static string GetDescription(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        if (template?.Localization?.Description != null)
        {
            return TextResolver.GetLocalizedTextWithValues(template.Localization.Description, card);
        }

        return string.Empty;
    }

    public static string GetAbilityTooltips(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        var sb = new StringBuilder();

        var tooltips = template?.Localization?.Tooltips;
        if (tooltips != null && tooltips.Count > 0)
        {
            foreach (var tooltip in tooltips)
            {
                if (tooltip?.Content != null)
                {
                    string text = TextResolver.GetLocalizedTextWithValues(tooltip.Content, card);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(text);
                    }
                }
            }
        }

        // Enchantment tooltips
        if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
        {
            var enchantmentTooltips = GetEnchantmentTooltips(itemCard);
            if (!string.IsNullOrEmpty(enchantmentTooltips))
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(enchantmentTooltips);
            }
        }

        return sb.ToString();
    }

    private static string GetEnchantmentTooltips(ItemCard itemCard)
    {
        if (!itemCard.Enchantment.HasValue) return string.Empty;

        try
        {
            var template = itemCard.Template as TCardItem;
            if (template == null) return string.Empty;

            if (!template.TryGetEnchantmentTemplate(itemCard.Enchantment.Value, out TEnchantment enchantmentTemplate))
                return string.Empty;

            if (enchantmentTemplate?.Localization?.Tooltips == null)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var tooltip in enchantmentTemplate.Localization.Tooltips)
            {
                if (tooltip?.Content != null)
                {
                    string text = TextResolver.GetLocalizedTextWithValues(tooltip.Content, itemCard);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(text);
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetEnchantmentTooltips error: {ex.Message}");
            return string.Empty;
        }
    }

    public static string GetFullDescription(Card card)
    {
        if (card == null) return string.Empty;

        var parts = new List<string>();

        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
            parts.Add(desc);

        string abilities = GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities))
            parts.Add(abilities);

        return string.Join(". ", parts);
    }
}
