using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads encounter and PvP opponent data from cards.
/// </summary>
internal static class EncounterReader
{
    public static string GetEncounterInfo(Card card)
    {
        if (card == null) return "Empty";

        string name = CardProperties.GetCardName(card);
        string type = GetEncounterTypeName(card.Type);
        string tier = CardProperties.GetTierName(card);

        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                string heroName = GetPvpOpponentHeroName(pvpOpponent) ?? name;
                string rank = GetPvpOpponentRank(pvpOpponent);
                if (!string.IsNullOrEmpty(rank))
                {
                    return $"{pvpOpponent.Name}, {heroName}, {type}, {rank}";
                }
                return $"{pvpOpponent.Name}, {heroName}, {type}, {tier}";
            }
        }

        return $"{name}, {type}, {tier}";
    }

    public static string GetEncounterDetailedInfo(Card card)
    {
        if (card == null) return "Empty";

        var sb = new StringBuilder();

        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                sb.Append(pvpOpponent.Name);
                sb.Append(", ");
                string heroName = GetPvpOpponentHeroName(pvpOpponent) ?? CardProperties.GetCardName(card);
                sb.Append(heroName);

                string rank = GetPvpOpponentRank(pvpOpponent);
                if (!string.IsNullOrEmpty(rank))
                {
                    sb.Append(", ");
                    sb.Append(rank);
                }

                sb.Append(", Level ");
                sb.Append(pvpOpponent.Level);
                sb.Append(", ");
                sb.Append(pvpOpponent.Victories);
                sb.Append(" wins, ");
                sb.Append(pvpOpponent.Prestige);
                sb.Append(" prestige");
                return sb.ToString();
            }
        }

        // Fallback for non-PvP encounters
        sb.Append(CardProperties.GetCardName(card));
        sb.Append(", ");
        sb.Append(GetEncounterTypeName(card.Type));

        string desc = CardProperties.GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
        {
            sb.Append(". ");
            sb.Append(desc);
        }

        string flavor = CardProperties.GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            sb.Append(". ");
            sb.Append(flavor);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets PvP encounter detail lines for arrow-key navigation.
    /// </summary>
    internal static List<string> GetPvpEncounterDetailLines(Card card)
    {
        var lines = new List<string>();

        var pvpOpponent = Data.SimPvpOpponent;
        if (pvpOpponent == null)
        {
            lines.Add(CardProperties.GetCardName(card));
            lines.Add("PvP Encounter");
            return lines;
        }

        try
        {
            var type = pvpOpponent.GetType();

            string name = GetPvpProperty<string>(pvpOpponent, type, "Name") ?? "Unknown";
            lines.Add($"Player: {name}");

            string hero = GetPvpOpponentHeroName(pvpOpponent) ?? CardProperties.GetCardName(card);
            lines.Add($"Hero: {hero}");

            string rankName = GetPvpProperty<object>(pvpOpponent, type, "Rank")?.ToString();
            string division = GetPvpProperty<object>(pvpOpponent, type, "Division")?.ToString();
            if (!string.IsNullOrEmpty(rankName))
            {
                lines.Add(!string.IsNullOrEmpty(division) && division != "0"
                    ? $"Rank: {rankName} {division}"
                    : $"Rank: {rankName}");
            }

            var rating = GetPvpProperty<int?>(pvpOpponent, type, "Rating");
            if (rating.HasValue && rating.Value > 0)
                lines.Add($"Rating: {rating.Value}");

            var level = GetPvpProperty<int?>(pvpOpponent, type, "Level");
            if (level.HasValue && level.Value > 0)
                lines.Add($"Level: {level.Value}");

            var victories = GetPvpProperty<int?>(pvpOpponent, type, "Victories");
            if (victories.HasValue)
                lines.Add($"Wins: {victories.Value}");

            var prestige = GetPvpProperty<int?>(pvpOpponent, type, "Prestige");
            if (prestige.HasValue && prestige.Value > 0)
                lines.Add($"Prestige: {prestige.Value}");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpEncounterDetailLines error: {ex.Message}");
            lines.Add(CardProperties.GetCardName(card));
            lines.Add("PvP Encounter");
        }

        return lines;
    }

    public static string GetPvpOpponentHeroName(object pvpOpponent)
    {
        if (pvpOpponent == null) return null;

        try
        {
            var type = pvpOpponent.GetType();
            var heroProp = type.GetProperty("Hero", BindingFlags.Public | BindingFlags.Instance);
            if (heroProp != null)
            {
                var value = heroProp.GetValue(pvpOpponent);
                if (value != null)
                {
                    string heroName = value.ToString();
                    if (!string.IsNullOrEmpty(heroName) && heroName != "Common")
                        return heroName;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpOpponentHeroName error: {ex.Message}");
        }

        return null;
    }

    public static string GetPvpOpponentRank(object pvpOpponent)
    {
        if (pvpOpponent == null) return null;

        try
        {
            var type = pvpOpponent.GetType();
            string rankName = null;
            string division = null;

            var rankProp = type.GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance);
            if (rankProp != null)
            {
                var value = rankProp.GetValue(pvpOpponent);
                if (value != null) rankName = value.ToString();
            }

            var divProp = type.GetProperty("Division", BindingFlags.Public | BindingFlags.Instance);
            if (divProp != null)
            {
                var value = divProp.GetValue(pvpOpponent);
                if (value != null) division = value.ToString();
            }

            if (!string.IsNullOrEmpty(rankName))
            {
                if (!string.IsNullOrEmpty(division) && division != "0")
                    return $"{rankName} {division}";
                return rankName;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpOpponentRank error: {ex.Message}");
        }

        return null;
    }

    private static string GetEncounterTypeName(ECardType type)
    {
        return type switch
        {
            ECardType.CombatEncounter => "Combat",
            ECardType.EventEncounter => "Event",
            ECardType.PedestalEncounter => "Upgrade",
            ECardType.EncounterStep => "Path",
            ECardType.PvpEncounter => "PvP",
            _ => "Encounter"
        };
    }

    private static T GetPvpProperty<T>(object pvpOpponent, Type type, string propertyName)
    {
        try
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(pvpOpponent);
                if (value is T typedValue)
                    return typedValue;

                if (typeof(T) == typeof(int?) && value != null)
                    return (T)(object)(int?)Convert.ToInt32(value);
            }
        }
        catch { }
        return default;
    }
}
