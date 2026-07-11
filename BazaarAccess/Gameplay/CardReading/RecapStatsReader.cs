using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;
using TheBazaar.AppFramework;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads the native post-combat recap stats shown by the game for recap cards.
/// </summary>
internal static class RecapStatsReader
{
    private struct RecapStatEntry
    {
        public readonly ECardStats Stat;
        public readonly string Label;

        public RecapStatEntry(ECardStats stat, string label)
        {
            Stat = stat;
            Label = label;
        }
    }

    private static readonly FieldInfo LastCombatSimField =
        typeof(BoardManager).GetField("_lastCombatSim", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly RecapStatEntry[] OrderedRecapStats =
    {
        new RecapStatEntry(ECardStats.DamageDone, "Damage dealt"),
        new RecapStatEntry(ECardStats.HealAdded, "Heal applied"),
        new RecapStatEntry(ECardStats.ShieldAdded, "Shield applied"),
        new RecapStatEntry(ECardStats.BurnAdded, "Burn applied"),
        new RecapStatEntry(ECardStats.PoisonAdded, "Poison applied"),
        new RecapStatEntry(ECardStats.RegenAdded, "Regeneration applied"),
        new RecapStatEntry(ECardStats.RageAdded, "Rage applied"),
        new RecapStatEntry(ECardStats.HastedCardsCount, "Hasted items"),
        new RecapStatEntry(ECardStats.SlowedCardsCount, "Slowed items"),
        new RecapStatEntry(ECardStats.FrozenCardsCount, "Frozen items"),
    };

    public static bool IsRecapViewActive()
    {
        try
        {
            return Singleton<BoardManager>.Instance?.IsRecapViewOpen == true;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> GetRecapLines(Card card)
    {
        if (card == null || !IsRecapViewActive())
        {
            return new List<string>();
        }

        var lines = new List<string> { "Recap" };
        var stats = GetStats(card);

        lines.Add($"Uses: {GetStatValue(stats, ECardStats.UseCount)}");

        foreach (var entry in OrderedRecapStats)
        {
            int value = GetStatValue(stats, entry.Stat);
            if (value > 0)
            {
                lines.Add($"{entry.Label}: {value}");
            }
        }

        return lines;
    }

    private static Dictionary<ECardStats, int> GetStats(Card card)
    {
        var boardManager = Singleton<BoardManager>.Instance;
        var combatSim = LastCombatSimField?.GetValue(boardManager) as CombatSim;

        if (combatSim?.CardStats == null)
        {
            return new Dictionary<ECardStats, int>();
        }

        return combatSim.CardStats.TryGetValue(card.InstanceId.ToString(), out var stats)
            ? stats
            : new Dictionary<ECardStats, int>();
    }

    private static int GetStatValue(Dictionary<ECardStats, int> stats, ECardStats stat)
    {
        return stats != null && stats.TryGetValue(stat, out int value) ? value : 0;
    }
}
