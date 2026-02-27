using System.Collections.Generic;
using System.Text;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Builds navigable detail-line lists and description strings for cards.
/// </summary>
internal static class DetailLineBuilder
{
    private struct StatEntry
    {
        public readonly ECardAttributeType Type;
        public readonly string Label;
        public StatEntry(ECardAttributeType type, string label) { Type = type; Label = label; }
    }

    private static readonly StatEntry[] PlayerCombatStats =
    {
        new StatEntry(ECardAttributeType.Ammo, "Ammo"),
        new StatEntry(ECardAttributeType.AmmoMax, "Max Ammo"),
        new StatEntry(ECardAttributeType.DamageAmount, "Damage"),
        new StatEntry(ECardAttributeType.HealAmount, "Heal"),
        new StatEntry(ECardAttributeType.ShieldApplyAmount, "Shield"),
        new StatEntry(ECardAttributeType.PoisonApplyAmount, "Poison"),
        new StatEntry(ECardAttributeType.BurnApplyAmount, "Burn"),
        new StatEntry(ECardAttributeType.RegenApplyAmount, "Regeneration"),
        new StatEntry(ECardAttributeType.HasteAmount, "Haste"),
        new StatEntry(ECardAttributeType.SlowAmount, "Slow"),
        new StatEntry(ECardAttributeType.FreezeAmount, "Freeze"),
        new StatEntry(ECardAttributeType.ChargeAmount, "Charge"),
        new StatEntry(ECardAttributeType.CritChance, "Crit Chance"),
        new StatEntry(ECardAttributeType.Lifesteal, "Lifesteal"),
        new StatEntry(ECardAttributeType.Multicast, "Multicast"),
        new StatEntry(ECardAttributeType.RepairTargets, "Repair Targets"),
    };

    private static readonly StatEntry[] EnemyCombatStats =
    {
        new StatEntry(ECardAttributeType.DamageAmount, "Damage"),
        new StatEntry(ECardAttributeType.HealAmount, "Heal"),
        new StatEntry(ECardAttributeType.ShieldApplyAmount, "Shield"),
        new StatEntry(ECardAttributeType.PoisonApplyAmount, "Poison"),
        new StatEntry(ECardAttributeType.BurnApplyAmount, "Burn"),
        new StatEntry(ECardAttributeType.RegenApplyAmount, "Regeneration"),
        new StatEntry(ECardAttributeType.HasteAmount, "Haste"),
        new StatEntry(ECardAttributeType.SlowAmount, "Slow"),
        new StatEntry(ECardAttributeType.FreezeAmount, "Freeze"),
        new StatEntry(ECardAttributeType.ChargeAmount, "Charge"),
        new StatEntry(ECardAttributeType.Ammo, "Ammo"),
        new StatEntry(ECardAttributeType.AmmoMax, "Max Ammo"),
        new StatEntry(ECardAttributeType.CritChance, "Crit Chance"),
        new StatEntry(ECardAttributeType.Lifesteal, "Lifesteal"),
        new StatEntry(ECardAttributeType.Multicast, "Multicast"),
        new StatEntry(ECardAttributeType.RepairTargets, "Repair Targets"),
    };

    // --- Public description methods ---

    public static string GetShortDescription(Card card)
    {
        if (card == null) return "Empty slot";

        string name = CardProperties.GetCardName(card);
        string tier = CardProperties.GetTierName(card);
        string tempState = CardProperties.GetTemperatureState(card);

        var parts = new List<string> { name, tier };

        if (QuestReader.IsQuestItem(card))
        {
            string questProgress = QuestReader.GetQuestProgress(card);
            parts.Add(questProgress ?? "Quest");
        }

        if (!string.IsNullOrEmpty(tempState))
            parts.Add(tempState);

        return string.Join(", ", parts);
    }

    public static string GetDetailedDescription(Card card)
    {
        if (card == null) return "Empty slot";

        var sb = new StringBuilder();

        sb.Append(CardProperties.GetCardName(card));
        sb.Append(", ");
        sb.Append(CardProperties.GetTierName(card));

        string tempState = CardProperties.GetTemperatureState(card);
        if (!string.IsNullOrEmpty(tempState))
            sb.Append($", {tempState}");

        var template = card.Template;
        if (template != null)
            sb.Append($", Size {(int)template.Size}");

        float? cdSeconds = GetCooldownSeconds(card);
        if (cdSeconds.HasValue)
            sb.Append($", Cooldown {cdSeconds.Value:F1}s");

        // Combat stats
        AppendStatIfPresent(sb, card, ECardAttributeType.Ammo, "Ammo");
        AppendStatIfPresent(sb, card, ECardAttributeType.AmmoMax, "Max Ammo");
        AppendStatIfPresent(sb, card, ECardAttributeType.DamageAmount, "Damage");
        AppendStatIfPresent(sb, card, ECardAttributeType.HealAmount, "Heal");
        AppendStatIfPresent(sb, card, ECardAttributeType.ShieldApplyAmount, "Shield");
        AppendStatIfPresent(sb, card, ECardAttributeType.PoisonApplyAmount, "Poison");
        AppendStatIfPresent(sb, card, ECardAttributeType.BurnApplyAmount, "Burn");
        AppendStatIfPresent(sb, card, ECardAttributeType.RegenApplyAmount, "Regen");

        // Speed stats
        AppendStatIfPresent(sb, card, ECardAttributeType.HasteAmount, "Haste");
        AppendStatIfPresent(sb, card, ECardAttributeType.SlowAmount, "Slow");
        AppendStatIfPresent(sb, card, ECardAttributeType.FreezeAmount, "Freeze");
        AppendStatIfPresent(sb, card, ECardAttributeType.ChargeAmount, "Charge");

        // Other stats
        AppendStatIfPresent(sb, card, ECardAttributeType.CritChance, "Crit%");
        AppendStatIfPresent(sb, card, ECardAttributeType.Lifesteal, "Lifesteal");
        AppendStatIfPresent(sb, card, ECardAttributeType.Multicast, "Multicast");
        AppendStatIfPresent(sb, card, ECardAttributeType.RepairTargets, "Repair Targets");

        string fullDesc = CardProperties.GetFullDescription(card);
        if (!string.IsNullOrEmpty(fullDesc))
        {
            sb.Append(". ");
            sb.Append(fullDesc);
        }

        string flavor = CardProperties.GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            sb.Append(". ");
            sb.Append(flavor);
        }

        return sb.ToString();
    }

    public static string GetBuyInfo(Card card)
    {
        if (card == null) return string.Empty;
        return $"{CardProperties.GetCardName(card)}, {CardProperties.GetBuyPrice(card)} gold";
    }

    public static string GetSellInfo(Card card)
    {
        if (card == null) return string.Empty;
        return $"{CardProperties.GetCardName(card)}, sells for {CardProperties.GetSellPrice(card)} gold";
    }

    // --- Detail line lists ---

    public static List<string> GetDetailLines(Card card)
    {
        if (card == null) return new List<string>();

        if (card.Type == ECardType.PvpEncounter)
            return EncounterReader.GetPvpEncounterDetailLines(card);

        return BuildDetailLines(card, enemyOrder: false);
    }

    public static List<string> GetEnemyDetailLines(Card card)
    {
        if (card == null) return new List<string>();
        return BuildDetailLines(card, enemyOrder: true);
    }

    public static string GetEnemyCompactDescription(Card card)
    {
        if (card == null) return "Empty slot";

        var parts = new List<string>();
        parts.Add(CardProperties.GetCardName(card));

        float? cdSeconds = GetCooldownSeconds(card);
        if (cdSeconds.HasValue)
            parts.Add($"{cdSeconds.Value:F1}s");

        // One primary combat stat for quick reading
        var damage = card.GetAttributeValue(ECardAttributeType.DamageAmount);
        if (damage.HasValue && damage.Value > 0)
        {
            parts.Add($"{damage.Value} damage");
        }
        else
        {
            var heal = card.GetAttributeValue(ECardAttributeType.HealAmount);
            if (heal.HasValue && heal.Value > 0)
            {
                parts.Add($"{heal.Value} heal");
            }
            else
            {
                var shield = card.GetAttributeValue(ECardAttributeType.ShieldApplyAmount);
                if (shield.HasValue && shield.Value > 0)
                {
                    parts.Add($"{shield.Value} shield");
                }
            }
        }

        return string.Join(", ", parts);
    }

    // --- Private helpers ---

    /// <summary>
    /// Builds detail lines for a card. enemyOrder controls section arrangement:
    /// Player: name, tier, quest, tags, temp, enchant, size, price, cooldown, stats, desc, abilities, flavor
    /// Enemy: name, desc, abilities, cooldown, stats, tier, tags, size
    /// </summary>
    private static List<string> BuildDetailLines(Card card, bool enemyOrder)
    {
        var lines = new List<string>();
        if (card == null) return lines;

        lines.Add(CardProperties.GetCardName(card));

        if (enemyOrder)
        {
            AddDescriptionLines(lines, card);
            AddAbilityLines(lines, card);

            string cd = GetCooldownLineText(card);
            if (cd != null) lines.Add(cd);

            AddAllCombatStats(lines, card, EnemyCombatStats);

            lines.Add(CardProperties.GetTierName(card));

            string tags = CardProperties.GetTags(card);
            if (!string.IsNullOrEmpty(tags)) lines.Add(tags);

            string sizeText = GetSizeText(card);
            if (sizeText != null) lines.Add(sizeText);
        }
        else
        {
            lines.Add(CardProperties.GetTierName(card));

            if (QuestReader.IsQuestItem(card))
            {
                var questLines = QuestReader.GetQuestLines(card);
                if (questLines.Count > 0)
                    lines.AddRange(questLines);
                else
                    lines.Add("Quest item");
            }

            string tags = CardProperties.GetTags(card);
            if (!string.IsNullOrEmpty(tags)) lines.Add(tags);

            string tempState = CardProperties.GetTemperatureState(card);
            if (!string.IsNullOrEmpty(tempState)) lines.Add($"State: {tempState}");

            if (card is ItemCard enchantedItem && enchantedItem.Enchantment.HasValue)
            {
                string enchantName = CardProperties.GetEnchantmentName(enchantedItem.Enchantment.Value);
                lines.Add($"Enchanted: {enchantName}");
            }

            string sizeText = GetSizeText(card);
            if (sizeText != null) lines.Add(sizeText);

            int buyPrice = CardProperties.GetBuyPrice(card);
            if (buyPrice > 0) lines.Add($"Buy {buyPrice} gold");

            int sellPrice = CardProperties.GetSellPrice(card);
            if (sellPrice > 0) lines.Add($"Sell {sellPrice} gold");

            string cd = GetCooldownLineText(card);
            if (cd != null) lines.Add(cd);

            AddAllCombatStats(lines, card, PlayerCombatStats);

            AddDescriptionLines(lines, card);
            AddAbilityLines(lines, card);

            string flavor = CardProperties.GetFlavorText(card);
            if (!string.IsNullOrEmpty(flavor)) lines.Add(flavor);
        }

        return lines;
    }

    private static void AppendStatIfPresent(StringBuilder sb, Card card, ECardAttributeType type, string label)
    {
        var value = card.GetAttributeValue(type);
        if (value.HasValue && value.Value > 0)
        {
            sb.Append($", {label} {value.Value}");
        }
    }

    private static void AddStatLine(List<string> lines, Card card, ECardAttributeType type, string label)
    {
        var value = card.GetAttributeValue(type);
        if (value.HasValue && value.Value != 0)
        {
            lines.Add($"{label}: {value.Value}");
        }
    }

    private static void AddAllCombatStats(List<string> lines, Card card, StatEntry[] stats)
    {
        foreach (var entry in stats)
            AddStatLine(lines, card, entry.Type, entry.Label);
    }

    internal static float? GetCooldownSeconds(Card card)
    {
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (!cooldown.HasValue || cooldown.Value <= 0)
        {
            cooldown = card.GetAttributeValue(ECardAttributeType.CooldownMax);
        }
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            return cooldown.Value / 1000f;
        }
        return null;
    }

    private static string GetCooldownLineText(Card card)
    {
        float? seconds = GetCooldownSeconds(card);
        if (seconds.HasValue)
        {
            return $"Cooldown {seconds.Value:F1} seconds";
        }
        return null;
    }

    private static string GetSizeText(Card card)
    {
        var template = card?.Template;
        if (template == null) return null;

        int size = (int)template.Size;
        string sizeName = template.Size switch
        {
            ECardSize.Small => "Small",
            ECardSize.Medium => "Medium",
            ECardSize.Large => "Large",
            _ => ""
        };
        return $"Size: {size} slots ({sizeName})";
    }

    private static void AddDescriptionLines(List<string> lines, Card card)
    {
        string desc = CardProperties.GetDescription(card);
        if (!string.IsNullOrEmpty(desc)) lines.Add(desc);
    }

    private static void AddAbilityLines(List<string> lines, Card card)
    {
        string abilities = CardProperties.GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities)) lines.Add(abilities);
    }
}
