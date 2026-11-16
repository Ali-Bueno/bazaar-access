using System;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads the socket effects (e.g. music note slots) under an item, mirroring the game's socket legend.
/// </summary>
public static class SocketEffectReader
{
    /// <summary>
    /// One line per socket effect under the item's slots: "Slot effect, title: description".
    /// </summary>
    public static List<string> GetSocketEffectLines(Card card)
    {
        var lines = new List<string>();
        if (!(card is ItemCard item) || item.Owner == null) return lines;

        try
        {
            var sockets = new HashSet<EContainerSocketId>(item.GetSockets());
            if (sockets.Count == 0) return lines;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var effect in Data.Entities.Values)
            {
                if (effect.Type != ECardType.SocketEffect || effect.Owner != item.Owner || effect.Section != item.Section) continue;
                if (!effect.LeftSocketId.HasValue || !sockets.Contains(effect.LeftSocketId.Value)) continue;

                string line = Describe(effect);
                if (!string.IsNullOrEmpty(line) && seen.Add(line)) lines.Add(line);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"SocketEffectReader error: {ex.Message}");
        }

        return lines;
    }

    private static string Describe(Card effect)
    {
        var localization = effect.Template?.Localization;
        if (localization == null) return null;

        string title = localization.Title != null ? TextResolver.GetLocalizedText(localization.Title) : null;
        if (string.IsNullOrEmpty(title)) return null;

        var content = localization.Tooltips?.FirstOrDefault(t => t?.Content != null)?.Content;
        string description = content != null ? TextHelper.CleanText(TextResolver.GetLocalizedTextWithValues(content, effect)) : null;

        return string.IsNullOrEmpty(description)
            ? Loc.T("card.socket_effect", title)
            : Loc.T("card.socket_effect_described", title, description);
    }
}
