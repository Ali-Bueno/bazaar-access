using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Core;
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
        public readonly string LabelKey;

        public RecapStatEntry(ECardStats stat, string labelKey)
        {
            Stat = stat;
            LabelKey = labelKey;
        }
    }

    private static readonly FieldInfo LastCombatSimField =
        typeof(BoardManager).GetField("_lastCombatSim", BindingFlags.Instance | BindingFlags.NonPublic);

    // Label text is resolved via Loc.T(LabelKey) at read time (not cached here), so a mid-session
    // language change is picked up immediately.
    private static readonly RecapStatEntry[] OrderedRecapStats =
    {
        new RecapStatEntry(ECardStats.DamageDone, "card.recap.damageDealt"),
        new RecapStatEntry(ECardStats.HealAdded, "card.recap.healApplied"),
        new RecapStatEntry(ECardStats.ShieldAdded, "card.recap.shieldApplied"),
        new RecapStatEntry(ECardStats.BurnAdded, "card.recap.burnApplied"),
        new RecapStatEntry(ECardStats.PoisonAdded, "card.recap.poisonApplied"),
        new RecapStatEntry(ECardStats.RegenAdded, "card.recap.regenApplied"),
        new RecapStatEntry(ECardStats.RageAdded, "card.recap.rageApplied"),
        new RecapStatEntry(ECardStats.HastedCardsCount, "card.recap.hastedItems"),
        new RecapStatEntry(ECardStats.SlowedCardsCount, "card.recap.slowedItems"),
        new RecapStatEntry(ECardStats.FrozenCardsCount, "card.recap.frozenItems"),
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

        var lines = new List<string> { Loc.T("card.recap.header") };
        var stats = GetStats(card);

        lines.Add(Loc.T("card.stat.line", Loc.T("card.recap.uses"), GetStatValue(stats, ECardStats.UseCount)));

        foreach (var entry in OrderedRecapStats)
        {
            int value = GetStatValue(stats, entry.Stat);
            if (value > 0)
            {
                lines.Add(Loc.T("card.stat.line", Loc.T(entry.LabelKey), value));
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
