using System;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.TempoNet.Enums;
using TheBazaar;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads player rank and game mode information.
/// </summary>
internal static class RankReader
{
    /// <summary>
    /// Gets the player's ranked rank (e.g., "Silver 3", "Gold 1", "Legendary").
    /// Uses the current client rank cache populated by the game.
    /// </summary>
    public static string GetPlayerRank()
    {
        try
        {
            if (!ClientCache.Rank.HasData) return null;

            var rank = ClientCache.Rank.Value;
            string rankName = rank.Rank.ToString();
            if (string.IsNullOrEmpty(rankName)) return null;

            if (rank.Rank == ERank.Legendary) return rankName;
            if (rank.Division > 0) return $"{rankName} {rank.Division}";
            return rankName;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetPlayerRank error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Checks if the current game mode is ranked.
    /// </summary>
    public static bool IsRankedMode()
    {
        try
        {
            return ClientCache.RunConfig.HasData &&
                   ClientCache.RunConfig.Value.RunType == EPlayMode.Ranked;
        }
        catch
        {
            return false;
        }
    }
}
