using System;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.Utilities;

namespace BazaarAccess.Core;

/// <summary>
/// The game's own words (Shield, Burn, Bronze, Weapon...), read from the game already translated.
///
/// These are deliberately NOT in the mod's translation files: taking them from the game means the
/// mod says exactly the same word the card says, in whatever language the player picked, and that
/// no one has to translate them by hand. See <see cref="Loc"/> for the mod's own sentences.
///
/// Every lookup falls back to the English enum name, so an unknown word degrades to today's
/// behaviour instead of going silent.
/// </summary>
public static class GameVocabulary
{
    /// <summary>Bronze, Silver, Gold, Diamond, Legendary.</summary>
    public static string Tier(ETier tier)
    {
        try
        {
            string name = LocalizableShared.GetLocalizableTier(tier)?.GetLocalizedText();
            if (!string.IsNullOrEmpty(name)) return name;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"Tier name not available for {tier}: {ex.Message}");
        }

        return tier.ToString();
    }

    /// <summary>Weapon, Tool, Friend, Property... the tags shown on the card.</summary>
    public static string Tag(ECardTag tag) => Keyword(tag.ToString());

    /// <summary>
    /// Damage, Shield, Burn, Cooldown... Returns null when the game has no word for this attribute
    /// (internal counters like RepairTargets), so the caller can decide what to say instead.
    /// </summary>
    public static string Attribute(ECardAttributeType attribute)
    {
        string key = attribute.ToString();
        string word = Keyword(key);

        return word == key ? null : word;
    }

    /// <summary>
    /// The keyword table the game uses to draw the coloured words and icons on a card. A miss
    /// echoes the key back, which is how callers detect "the game has no word for this".
    /// </summary>
    public static string Keyword(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        try
        {
            // Null until the tooltip prefab has loaded.
            var typography = Data.TooltipTypography;
            if (typography != null)
            {
                string word = typography.GetKeywordTranslation(key);
                if (!string.IsNullOrEmpty(word)) return word;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"Keyword not available for {key}: {ex.Message}");
        }

        return key;
    }
}
