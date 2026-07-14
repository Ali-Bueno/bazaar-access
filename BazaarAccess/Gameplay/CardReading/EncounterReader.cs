using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Combat;
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
        if (card == null) return Loc.T("card.name.empty");

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
                    return Loc.T("card.encounter.pvp.withRank", pvpOpponent.Name, heroName, type, rank);
                }
                return Loc.T("card.encounter.pvp.withTier", pvpOpponent.Name, heroName, type, tier);
            }
        }

        return Loc.T("card.encounter.info", name, type, tier);
    }

    internal static List<string> GetCombatEncounterDetailLines(Card card)
    {
        var lines = new List<string>();
        if (card == null) return lines;

        lines.Add(CardProperties.GetCardName(card));
        lines.Add(CardProperties.GetTierName(card));

        if (TryGetCombatEncounterRewards(card, out uint monsterLevel, out int xpReward, out int goldReward))
        {
            lines.Add(Loc.T("card.encounter.level", monsterLevel));
            lines.Add(Loc.T("card.encounter.xp", xpReward));
            lines.Add(Loc.T("card.encounter.gold", goldReward));
        }

        string desc = CardProperties.GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
            lines.Add(desc);

        string flavor = CardProperties.GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
            lines.Add(flavor);

        return lines;
    }

    public static string GetEncounterDetailedInfo(Card card)
    {
        if (card == null) return Loc.T("card.name.empty");

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

                sb.Append(Loc.T("card.encounter.pvp.stats", pvpOpponent.Level, pvpOpponent.Victories, pvpOpponent.Prestige));
                return sb.ToString();
            }
        }

        // Fallback for non-PvP encounters
        sb.Append(CardProperties.GetCardName(card));
        sb.Append(", ");
        sb.Append(GetEncounterTypeName(card.Type));

        if (TryGetCombatEncounterRewards(card, out uint monsterLevel, out int xpReward, out int goldReward))
        {
            sb.Append(Loc.T("card.encounter.rewards", monsterLevel, xpReward, goldReward));
        }

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
            lines.Add(Loc.T("card.encounter.pvp.label"));
            return lines;
        }

        try
        {
            var type = pvpOpponent.GetType();

            string name = GetPvpProperty<string>(pvpOpponent, type, "Name") ?? Loc.T("card.name.unknown");
            lines.Add(Loc.T("card.encounter.pvp.player", name));

            string hero = GetPvpOpponentHeroName(pvpOpponent) ?? CardProperties.GetCardName(card);
            lines.Add(Loc.T("card.encounter.pvp.hero", hero));

            string rankName = GetPvpProperty<object>(pvpOpponent, type, "Rank")?.ToString();
            string division = GetPvpProperty<object>(pvpOpponent, type, "Division")?.ToString();
            if (!string.IsNullOrEmpty(rankName))
            {
                lines.Add(!string.IsNullOrEmpty(division) && division != "0"
                    ? Loc.T("card.encounter.pvp.rank.division", rankName, division)
                    : Loc.T("card.encounter.pvp.rank", rankName));
            }

            var rating = GetPvpProperty<int?>(pvpOpponent, type, "Rating");
            if (rating.HasValue && rating.Value > 0)
                lines.Add(Loc.T("card.encounter.pvp.rating", rating.Value));

            var level = GetPvpProperty<int?>(pvpOpponent, type, "Level");
            if (level.HasValue && level.Value > 0)
                lines.Add(Loc.T("card.encounter.level", level.Value));

            var victories = GetPvpProperty<int?>(pvpOpponent, type, "Victories");
            if (victories.HasValue)
                lines.Add(Loc.T("card.encounter.pvp.wins", victories.Value));

            var prestige = GetPvpProperty<int?>(pvpOpponent, type, "Prestige");
            if (prestige.HasValue && prestige.Value > 0)
                lines.Add(Loc.T("card.encounter.pvp.prestige", prestige.Value));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpEncounterDetailLines error: {ex.Message}");
            lines.Add(CardProperties.GetCardName(card));
            lines.Add(Loc.T("card.encounter.pvp.label"));
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
            ECardType.CombatEncounter => Loc.T("card.encounter.type.combat"),
            ECardType.EventEncounter => Loc.T("card.encounter.type.event"),
            ECardType.PedestalEncounter => Loc.T("card.encounter.type.upgrade"),
            ECardType.EncounterStep => Loc.T("card.encounter.type.path"),
            ECardType.PvpEncounter => Loc.T("card.encounter.type.pvp"),
            _ => Loc.T("card.encounter.type.default")
        };
    }

    private static bool TryGetCombatEncounterRewards(Card card, out uint monsterLevel, out int xpReward, out int goldReward)
    {
        monsterLevel = 0;
        xpReward = 0;
        goldReward = 0;

        if (card?.Type != ECardType.CombatEncounter)
            return false;

        if (card.Template is not TCardEncounterCombat combatTemplate)
            return false;

        monsterLevel = (combatTemplate.CombatantType as TCombatantMonster)?.Level ?? 0;
        xpReward = combatTemplate.RewardCombatXp;
        goldReward = combatTemplate.RewardCombatGold;
        return true;
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
