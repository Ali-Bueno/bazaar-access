using BazaarAccess.Core;
using BazaarGameShared.Domain.Core.Types;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Single source of truth for tier progression and naming.
/// </summary>
public static class TierHelper
{
    /// <summary>
    /// Gets the next tier in the progression sequence.
    /// Returns the same tier if already at max (Legendary).
    /// </summary>
    public static ETier GetNextTier(ETier current)
    {
        return current switch
        {
            ETier.Bronze => ETier.Silver,
            ETier.Silver => ETier.Gold,
            ETier.Gold => ETier.Diamond,
            ETier.Diamond => ETier.Legendary,
            _ => current
        };
    }

    /// <summary>
    /// Gets a display-friendly name for a tier. Delegates to ItemReader/CardProperties so
    /// there is a single source of truth (the game's own tier word via GameVocabulary).
    /// </summary>
    public static string GetName(ETier tier)
    {
        return ItemReader.GetTierName(tier);
    }

    /// <summary>
    /// Gets the display name of the next tier.
    /// Returns "max" if already at max tier.
    /// </summary>
    public static string GetNextName(ETier current)
    {
        var next = GetNextTier(current);
        return next == current ? Loc.T("action.tier.max") : GetName(next);
    }

    /// <summary>
    /// Checks if an item can be upgraded (not at max tier).
    /// </summary>
    public static bool CanUpgrade(ETier current)
    {
        return current != ETier.Legendary;
    }
}
