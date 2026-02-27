using System;
using System.Collections.Generic;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Quests;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads quest-related data from quest cards.
/// </summary>
internal static class QuestReader
{
    public static bool IsQuestItem(Card card)
    {
        if (card == null) return false;
        return card.HiddenTags != null && card.HiddenTags.Contains(EHiddenTag.Quest);
    }

    /// <summary>
    /// Gets quest condition lines showing requirements, progress, and rewards.
    /// </summary>
    public static List<string> GetQuestLines(Card card)
    {
        var lines = new List<string>();
        if (card == null || !IsQuestItem(card)) return lines;

        try
        {
            var template = card.Template as TCardItem;
            if (template?.Quests == null || template.Quests.Count == 0) return lines;

            foreach (var questGroup in template.Quests)
            {
                if (questGroup?.Entries == null) continue;

                foreach (var entry in questGroup.Entries)
                {
                    if (entry == null) continue;

                    // Get quest condition description from localization tooltips
                    string questDesc = GetFirstTooltipText(entry.Localization?.Tooltips, card);

                    // Get progress
                    int current = card.GetAttributeValue(entry.AttributeType) ?? 0;
                    int target = entry.Target;
                    bool isComplete = current >= target;
                    string status = isComplete ? "Complete" : $"{current}/{target}";

                    lines.Add(!string.IsNullOrEmpty(questDesc)
                        ? $"Quest: {questDesc} ({status})"
                        : $"Quest: {status}");

                    // Get quest reward description
                    string rewardDesc = GetQuestRewardDescription(entry, card);
                    if (!string.IsNullOrEmpty(rewardDesc))
                    {
                        lines.Add($"Reward: {rewardDesc}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetQuestLines error: {ex.Message}");
        }

        return lines;
    }

    /// <summary>
    /// Gets compact quest progress for short descriptions.
    /// Returns null if not a quest item.
    /// </summary>
    public static string GetQuestProgress(Card card)
    {
        if (card == null || !IsQuestItem(card)) return null;

        try
        {
            var template = card.Template as TCardItem;
            if (template?.Quests == null || template.Quests.Count == 0) return null;

            foreach (var questGroup in template.Quests)
            {
                if (questGroup?.Entries == null) continue;
                foreach (var entry in questGroup.Entries)
                {
                    if (entry == null) continue;
                    int current = card.GetAttributeValue(entry.AttributeType) ?? 0;
                    int target = entry.Target;
                    return current >= target ? "Quest complete" : $"Quest {current}/{target}";
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetQuestProgress error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the reward/completion effect description for a quest entry.
    /// </summary>
    private static string GetQuestRewardDescription(TQuestEntry entry, Card card)
    {
        if (entry == null) return null;

        try
        {
            return GetFirstTooltipText(entry.Reward?.Localization?.Tooltips, card);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetQuestRewardDescription error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the first non-empty tooltip text from a list of tooltips.
    /// </summary>
    private static string GetFirstTooltipText(IList<BazaarGameShared.Domain.Tooltips.TTooltip> tooltips, Card card)
    {
        if (tooltips == null) return null;

        foreach (var tooltip in tooltips)
        {
            if (tooltip?.Content != null)
            {
                string text = TextResolver.GetLocalizedTextWithValues(tooltip.Content, card);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }

        return null;
    }
}
