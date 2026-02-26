using System.Collections.Generic;
using System.Linq;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;

namespace BazaarAccess.Gameplay.Combat;

/// <summary>
/// Tracks per-card combat statistics (damage, heal, shield, triggers, crits, repairs).
/// Stats persist through wave announcements and are cleared at combat start.
/// Used by the H key recap and post-combat stats screen.
/// </summary>
public static class CardStatsTracker
{
    private static Dictionary<string, CardCombatStats> _playerCardStats = new Dictionary<string, CardCombatStats>();
    private static Dictionary<string, CardCombatStats> _enemyCardStats = new Dictionary<string, CardCombatStats>();

    /// <summary>
    /// Per-card stats accumulated over the entire combat.
    /// </summary>
    public class CardCombatStats
    {
        public int Damage;
        public int Heal;
        public int Shield;
        public int Triggers;
        public int Crits;
        public int Repairs;

        public string Format(string name)
        {
            var parts = new List<string>();
            parts.Add(name);
            if (Damage > 0) parts.Add($"{Damage} damage");
            if (Heal > 0) parts.Add($"{Heal} heal");
            if (Shield > 0) parts.Add($"{Shield} shield");
            if (Repairs > 0) parts.Add($"{Repairs} repairs");
            if (Crits > 0) parts.Add($"{Crits} crits");
            parts.Add($"{Triggers} triggers");
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Clears all tracked stats for a new combat.
    /// </summary>
    public static void Clear()
    {
        _playerCardStats.Clear();
        _enemyCardStats.Clear();
    }

    /// <summary>
    /// Whether there are any per-card stats available (combat has occurred).
    /// </summary>
    public static bool HasCombatStats => _playerCardStats.Count > 0 || _enemyCardStats.Count > 0;

    /// <summary>
    /// Gets or creates a CardCombatStats entry for the given item.
    /// </summary>
    private static CardCombatStats GetOrCreateStats(string itemName, bool isPlayerItem)
    {
        var dict = isPlayerItem ? _playerCardStats : _enemyCardStats;
        if (!dict.TryGetValue(itemName, out var stats))
        {
            stats = new CardCombatStats();
            dict[itemName] = stats;
        }
        return stats;
    }

    /// <summary>
    /// Tracks trigger count for ALL combat events, regardless of action type.
    /// This ensures passive items (Water Wheel, Keychain, etc.) appear in combat stats.
    /// </summary>
    public static void TrackTriggerCount(string itemName, bool isPlayerItem)
    {
        if (string.IsNullOrEmpty(itemName)) return;

        var stats = GetOrCreateStats(itemName, isPlayerItem);
        stats.Triggers++;
    }

    /// <summary>
    /// Tracks detailed per-card stats (damage, heal, etc.) for relevant actions only.
    /// Trigger count is handled separately by TrackTriggerCount.
    /// </summary>
    public static void TrackCardStats(string itemName, bool isPlayerItem, ActionType actionType, int amount, bool isCrit, CombatActionData data)
    {
        if (string.IsNullOrEmpty(itemName)) return;

        var stats = GetOrCreateStats(itemName, isPlayerItem);

        // Don't increment Triggers here - TrackTriggerCount handles it
        if (isCrit) stats.Crits++;

        switch (actionType)
        {
            case ActionType.PlayerDamage:
                stats.Damage += amount;
                break;
            case ActionType.PlayerHeal:
                stats.Heal += amount;
                break;
            case ActionType.PlayerShieldApply:
                stats.Shield += amount;
                break;
            case ActionType.CardRepair:
                stats.Repairs++;
                break;
        }
    }

    /// <summary>
    /// Gets formatted per-card combat stats for the recap screen.
    /// Returns a list of lines: summary first, then player items sorted by damage, then enemy items.
    /// </summary>
    public static List<string> GetCombatStatsLines(int totalDamageDealt, int totalDamageTaken)
    {
        var lines = new List<string>();

        // Summary line
        lines.Add($"Combat stats. You dealt {totalDamageDealt}, took {totalDamageTaken}");

        // Player items sorted by damage (highest first), then by triggers
        if (_playerCardStats.Count > 0)
        {
            lines.Add($"--- Your items: {_playerCardStats.Count} ---");
            var sorted = _playerCardStats.OrderByDescending(kv => kv.Value.Damage)
                                          .ThenByDescending(kv => kv.Value.Triggers);
            foreach (var kv in sorted)
            {
                lines.Add(kv.Value.Format(kv.Key));
            }
        }

        // Enemy items sorted by damage (highest first)
        if (_enemyCardStats.Count > 0)
        {
            lines.Add($"--- Enemy items: {_enemyCardStats.Count} ---");
            var sorted = _enemyCardStats.OrderByDescending(kv => kv.Value.Damage)
                                         .ThenByDescending(kv => kv.Value.Triggers);
            foreach (var kv in sorted)
            {
                lines.Add(kv.Value.Format(kv.Key));
            }
        }

        if (_playerCardStats.Count == 0 && _enemyCardStats.Count == 0)
        {
            lines.Add("No combat data recorded");
        }

        return lines;
    }

    /// <summary>
    /// Determines if a card belongs to the player using the Owner property.
    /// </summary>
    public static bool IsPlayerCard(Card card)
    {
        if (card == null) return false;

        try
        {
            var player = Data.Run?.Player;
            if (player == null) return true; // Default to player if no run data

            // Card.Owner is set to the owning Player instance
            // Player cards have Owner == Data.Run.Player
            // Enemy cards have Owner == Data.Run.Opponent
            // Unowned cards (encounters, etc.) have Owner == null
            if (card.Owner == null) return true; // Unowned cards default to player
            return card.Owner == player;
        }
        catch
        {
            return true;
        }
    }
}
