using System.Collections.Generic;
using BazaarAccess.Gameplay.CardReading;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Cards.Enchantments;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Tooltips;
using BazaarGameShared.Domain.Values;
using TheBazaar;
using TheBazaar.Localization;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Public facade for card/item reading. Delegates to focused sub-modules in CardReading/.
/// </summary>
public static class ItemReader
{
    // --- Text resolution ---

    public static string GetLocalizedText(TLocalizableText text)
        => TextResolver.GetLocalizedText(text);

    public static string GetLocalizedTextWithValues(TLocalizableText text, Card card)
        => TextResolver.GetLocalizedTextWithValues(text, card);

    // --- Card properties ---

    public static string GetCardName(Card card)
        => CardProperties.GetCardName(card);

    public static string GetEnchantmentName(EEnchantmentType enchantment)
        => CardProperties.GetEnchantmentName(enchantment);

    public static string GetTierName(Card card)
        => CardProperties.GetTierName(card);

    public static string GetTierName(ETier tier)
        => CardProperties.GetTierName(tier);

    public static string GetSizeName(Card card)
        => CardProperties.GetSizeName(card);

    public static int GetBuyPrice(Card card)
        => CardProperties.GetBuyPrice(card);

    public static int GetSellPrice(Card card)
        => CardProperties.GetSellPrice(card);

    public static string GetTags(Card card)
        => CardProperties.GetTags(card);

    public static string GetTemperatureState(Card card)
        => CardProperties.GetTemperatureState(card);

    public static bool IsHeated(Card card)
        => CardProperties.IsHeated(card);

    public static bool IsChilled(Card card)
        => CardProperties.IsChilled(card);

    public static string GetFlavorText(Card card)
        => CardProperties.GetFlavorText(card);

    public static string GetDescription(Card card)
        => CardProperties.GetDescription(card);

    public static string GetAbilityTooltips(Card card)
        => CardProperties.GetAbilityTooltips(card);

    public static string GetFullDescription(Card card)
        => CardProperties.GetFullDescription(card);

    // --- Descriptions and detail lines ---

    public static string GetShortDescription(Card card)
        => DetailLineBuilder.GetShortDescription(card);

    public static string GetDetailedDescription(Card card)
        => DetailLineBuilder.GetDetailedDescription(card);

    public static string GetBuyInfo(Card card)
        => DetailLineBuilder.GetBuyInfo(card);

    public static string GetSellInfo(Card card)
        => DetailLineBuilder.GetSellInfo(card);

    public static List<string> GetDetailLines(Card card)
        => DetailLineBuilder.GetDetailLines(card);

    public static List<string> GetEnemyDetailLines(Card card)
        => DetailLineBuilder.GetEnemyDetailLines(card);

    public static string GetEnemyCompactDescription(Card card)
        => DetailLineBuilder.GetEnemyCompactDescription(card);

    // --- Quest ---

    public static bool IsQuestItem(Card card)
        => QuestReader.IsQuestItem(card);

    public static List<string> GetQuestLines(Card card)
        => QuestReader.GetQuestLines(card);

    public static string GetQuestProgress(Card card)
        => QuestReader.GetQuestProgress(card);

    // --- Encounters ---

    public static string GetEncounterInfo(Card card)
        => EncounterReader.GetEncounterInfo(card);

    public static string GetEncounterDetailedInfo(Card card)
        => EncounterReader.GetEncounterDetailedInfo(card);

    public static string GetPvpOpponentHeroName(object pvpOpponent)
        => EncounterReader.GetPvpOpponentHeroName(pvpOpponent);

    public static string GetPvpOpponentRank(object pvpOpponent)
        => EncounterReader.GetPvpOpponentRank(pvpOpponent);

    // --- Properties ---

    public static List<string> GetTagDescriptions(Card card)
        => PropertyDescriber.GetTagDescriptions(card);

    public static List<string> GetKeywordDescriptions(Card card)
        => PropertyDescriber.GetKeywordDescriptions(card);

    public static List<string> GetAllPropertyDescriptions(Card card)
        => PropertyDescriber.GetAllPropertyDescriptions(card);

    // --- Rank ---

    public static string GetPlayerRank()
        => RankReader.GetPlayerRank();

    public static bool IsRankedMode()
        => RankReader.IsRankedMode();
}
