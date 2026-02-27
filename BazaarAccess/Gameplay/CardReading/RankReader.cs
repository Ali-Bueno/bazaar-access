using System;
using System.Reflection;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Reads player rank and game mode information.
/// </summary>
internal static class RankReader
{
    /// <summary>
    /// Gets the player's ranked rank (e.g., "Silver 3", "Gold 1", "Legendary").
    /// Uses reflection to access Data.Rank.CurrentSeasonRank.
    /// </summary>
    public static string GetPlayerRank()
    {
        try
        {
            var dataType = typeof(Data);
            var rankProp = dataType.GetProperty("Rank", BindingFlags.Public | BindingFlags.Static);
            if (rankProp == null) return null;

            var rankObj = rankProp.GetValue(null);
            if (rankObj == null) return null;

            var seasonRankProp = rankObj.GetType().GetProperty("CurrentSeasonRank",
                BindingFlags.Public | BindingFlags.Instance);
            if (seasonRankProp == null) return null;

            var seasonRank = seasonRankProp.GetValue(rankObj);
            if (seasonRank == null) return null;

            var seasonType = seasonRank.GetType();

            // Get ERank (Bronze, Silver, Gold, Diamond, Legendary)
            string rankName = null;
            var rankEnumProp = seasonType.GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance);
            if (rankEnumProp != null)
            {
                var val = rankEnumProp.GetValue(seasonRank);
                if (val != null) rankName = val.ToString();
            }

            if (string.IsNullOrEmpty(rankName)) return null;

            // Legendary has no division
            if (rankName == "Legendary") return "Legendary";

            // Get Division (1-4)
            var divProp = seasonType.GetProperty("Division", BindingFlags.Public | BindingFlags.Instance);
            if (divProp != null)
            {
                var divVal = divProp.GetValue(seasonRank);
                if (divVal != null)
                {
                    string div = divVal.ToString();
                    if (!string.IsNullOrEmpty(div) && div != "0")
                        return $"{rankName} {div}";
                }
            }

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
            return Data.RunConfiguration?.RunType == EPlayMode.Ranked;
        }
        catch
        {
            return false;
        }
    }
}
