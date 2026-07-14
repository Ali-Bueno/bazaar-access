using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Combat;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Effect;
using BazaarGameShared.Domain.Effect.Actions;
using BazaarGameShared.Domain.Effect.AuraActions;
using TheBazaar;
using TheBazaar.Tooltips;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Renders a tooltip to plain text, restoring the attribute words the game draws as icons.
///
/// The game keeps the literal words of the template ("Deal 50 damage") but renders every value
/// token as icon + number with no word ("this gains [shield icon]5"). A screen reader only hears
/// the number. This mirrors the game's own token -> attribute mapping and appends the attribute
/// word when the sentence around the token doesn't already spell it out.
/// </summary>
internal static class TooltipIconResolver
{
    // A word: starts with a letter, so symbols and bare numbers are ignored.
    private static readonly Regex WordRegex = new Regex(@"\p{L}[\p{L}\p{Nd}]*", RegexOptions.Compiled);

    // Leading letters two words must share to count as the same word ("Quema" ~ "Quemadura").
    private const int RootLength = 5;

    // '.' also separates decimals, so a terminator must not be followed by a digit.
    private static bool IsSentenceEnd(string text, int index)
    {
        char c = text[index];
        if (c == '\n') return true;
        if (c != '.' && c != '!' && c != '?') return false;
        return index + 1 >= text.Length || !char.IsDigit(text[index + 1]);
    }

    /// <summary>
    /// Renders the builder's components, appending the attribute word after icon-only value tokens.
    /// </summary>
    public static string Render(TooltipBuilder builder)
    {
        var sb = new StringBuilder();
        var pending = new List<KeyValuePair<int, string>>();

        foreach (var component in builder.Components)
        {
            if (!(component is ITooltipToken token))
            {
                component.Render(sb);
                continue;
            }

            // The style attribute is the one whose icon the game draws next to the value.
            var style = GetStyleAttribute(component);
            float? value = token.Resolve();

            if (!value.HasValue)
            {
                component.Render(sb);
                continue;
            }

            sb.Append(FormatValue(value.Value, style ?? token.ReferencedAttribute));

            // Time attributes already carry their unit in the text ("Charge this 1 second(s)"),
            // so their keyword would only add noise.
            if (style.HasValue && !style.Value.RequiresConversionToSeconds())
            {
                string word = GetAttributeWord(style.Value);
                if (!string.IsNullOrEmpty(word))
                    pending.Add(new KeyValuePair<int, string>(sb.Length, word));
            }
        }

        return AppendMissingWords(sb.ToString(), pending);
    }

    /// <summary>
    /// Inserts each pending attribute word right after its value, unless the text around that value
    /// already names the attribute.
    /// </summary>
    private static string AppendMissingWords(string text, List<KeyValuePair<int, string>> pending)
    {
        if (pending.Count == 0) return text;

        var sb = new StringBuilder(text);

        // Back to front so earlier insertions don't shift the positions still to be used.
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            int position = pending[i].Key;
            string word = pending[i].Value;

            if (AlreadySaid(text, position, word))
                continue;

            sb.Insert(position, " " + word);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether the value at <paramref name="position"/> already reads as the attribute.
    ///
    /// Only the text touching the value counts, not the whole sentence: a sentence can name the
    /// attribute for an unrelated reason ("Burn items to the right gain 2" - that 2 still needs its
    /// word), while the word that belongs to the value always sits right against it, either after
    /// it ("Deal 50 damage") or immediately before it ("Shield 25", "Burn 4").
    /// </summary>
    private static bool AlreadySaid(string text, int position, string word)
    {
        if (MentionsAllWords(RestOfSentence(text, position), word))
            return true;

        // Same root, because the text often uses the verb where the keyword is the noun
        // ("Quema 4" / "Quemadura", "Burn 4" / "Burn").
        return ShareRoot(WordBefore(text, position), word);
    }

    // From the value to the end of its sentence: "+5" -> "% de Probabilidad de Crítico".
    private static string RestOfSentence(string text, int position)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        int start = Math.Min(position, text.Length);
        int end = text.Length;

        for (int i = start; i < text.Length; i++)
        {
            if (IsSentenceEnd(text, i))
            {
                end = i;
                break;
            }
        }

        return text.Substring(start, end - start);
    }

    // The last word before the value, skipping the value's own digits and symbols: "Shield 25" -> "Shield".
    private static string WordBefore(string text, int position)
    {
        int end = Math.Min(position, text.Length);

        while (end > 0 && !char.IsLetter(text[end - 1]))
        {
            if (IsSentenceEnd(text, end - 1)) return null;
            end--;
        }

        int start = end;
        while (start > 0 && char.IsLetter(text[start - 1]))
            start--;

        return end > start ? text.Substring(start, end - start) : null;
    }

    // The game's keyword can carry symbols and joiners ("% Crit Chance") while the text spells the
    // same thing its own way ("Probabilidad de Crítico"), so compare on words, not on the raw string.
    // Matching at a word start but allowing suffixes lets "Shields" count as "Shield".
    private static bool MentionsAllWords(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;

        var terms = WordRegex.Matches(word);
        if (terms.Count == 0) return false;

        foreach (Match term in terms)
        {
            if (!Regex.IsMatch(text, @"\b" + Regex.Escape(term.Value), RegexOptions.IgnoreCase))
                return false;
        }

        return true;
    }

    private static bool ShareRoot(string candidate, string word)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(word)) return false;

        foreach (Match term in WordRegex.Matches(word))
        {
            if (ShareRootWithTerm(candidate, term.Value)) return true;
        }

        return false;
    }

    private static bool ShareRootWithTerm(string candidate, string term)
    {
        int shortest = Math.Min(candidate.Length, term.Length);

        // Too short to have a meaningful root: only an exact match counts.
        if (shortest < RootLength)
            return string.Equals(candidate, term, StringComparison.OrdinalIgnoreCase);

        return string.Compare(candidate, 0, term, 0, RootLength, StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>
    /// Formats a token value the way the game does: milliseconds shown as seconds, one decimal.
    /// </summary>
    private static string FormatValue(float value, ECardAttributeType? attribute)
    {
        if (attribute.HasValue && attribute.Value.RequiresConversionToSeconds())
            value = TooltipExtensions.MillisecondsToSeconds(value);

        return value.IsDecimal() ? value.GetDecimalValueString() : value.ToString();
    }

    /// <summary>
    /// The word the icon replaced: the game's own keyword for the attribute, already localized, so
    /// the mod says exactly what a sighted player sees on the card.
    /// </summary>
    private static string GetAttributeWord(ECardAttributeType attribute)
    {
        string word = GameVocabulary.Attribute(attribute);

        if (!string.IsNullOrEmpty(word))
            return TrimToWords(TextHelper.CleanText(word));

        // The game has no word for this attribute (or the table isn't loaded yet).
        return FallbackWord(attribute.ToString());
    }

    // Keyword entries are written for display next to the icon and can carry symbols ("% Crit
    // Chance"). Spoken after the value, only the words themselves are wanted.
    private static string TrimToWords(string word)
    {
        if (string.IsNullOrEmpty(word)) return null;

        var terms = WordRegex.Matches(word);
        if (terms.Count == 0) return null;

        int start = terms[0].Index;
        Match last = terms[terms.Count - 1];

        return word.Substring(start, last.Index + last.Length - start);
    }

    private static string FallbackWord(string key)
    {
        return key.StartsWith("Custom_", StringComparison.Ordinal)
            ? null
            : EffectFormatter.GetFriendlyAttributeName(key);
    }

    /// <summary>
    /// Which attribute's icon the game draws for a value token. Mirrors the game's internal
    /// TooltipComponentExtensions.GetStyleAttribute (TheBazaar.Assets.Scripts.Core.Data.Tooltip).
    /// An unmapped token gets no icon, so it needs no word either.
    /// </summary>
    private static ECardAttributeType? GetStyleAttribute(ITooltipComponent component)
    {
        switch (component)
        {
            case TooltipComponentAbility ability:
                if (ability.Accessor == ETooltipAccessorType.Targets) return null;
                return GetActionAttribute(ability.Ability?.Action);

            case TooltipComponentAura aura:
                if (aura.Accessor == ETooltipAccessorType.Targets) return null;
                return GetAuraActionAttribute(aura.Aura?.Action);

            case TooltipComponentAttribute attribute:
                return GetAttributeTokenStyle(attribute.ReferencedAttribute);

            default:
                return null;
        }
    }

    // Target counts are plain numbers, not stat amounts: the game gives them no icon.
    private static ECardAttributeType? GetAttributeTokenStyle(ECardAttributeType? referenced)
    {
        switch (referenced)
        {
            case ECardAttributeType.ChargeTargets:
            case ECardAttributeType.FreezeTargets:
            case ECardAttributeType.HasteTargets:
            case ECardAttributeType.ReloadTargets:
            case ECardAttributeType.SlowTargets:
                return null;
            default:
                return referenced;
        }
    }

    private static ECardAttributeType? GetActionAttribute(ITAction action)
    {
        switch (action)
        {
            case TActionCardCharge _: return ECardAttributeType.ChargeAmount;
            case TActionCardDisable _: return ECardAttributeType.DisableTargets;
            case TActionCardFreeze _: return ECardAttributeType.FreezeAmount;
            case TActionCardHaste _: return ECardAttributeType.HasteAmount;
            case TActionCardReload _: return ECardAttributeType.ReloadAmount;
            case TActionCardSlow _: return ECardAttributeType.SlowAmount;
            case TActionCardModifyAttribute modify: return modify.AttributeType;
            case TActionPlayerBurnApply _: return ECardAttributeType.BurnApplyAmount;
            case TActionPlayerBurnRemove _: return ECardAttributeType.BurnRemoveAmount;
            case TActionPlayerDamage _: return ECardAttributeType.DamageAmount;
            case TActionPlayerHeal _: return ECardAttributeType.HealAmount;
            case TActionPlayerModifyAttribute modify: return ConvertPlayerAttribute(modify.AttributeType);
            case TActionPlayerPoisonApply _: return ECardAttributeType.PoisonApplyAmount;
            case TActionPlayerPoisonRemove _: return ECardAttributeType.PoisonRemoveAmount;
            case TActionPlayerShieldApply _: return ECardAttributeType.ShieldApplyAmount;
            case TActionPlayerShieldRemove _: return ECardAttributeType.ShieldRemoveAmount;
            case TActionPlayerRegenApply _: return ECardAttributeType.RegenApplyAmount;
            case TActionPlayerRageApply _: return ECardAttributeType.RageApplyAmount;
            default: return null;
        }
    }

    private static ECardAttributeType? GetAuraActionAttribute(ITAuraAction action)
    {
        switch (action)
        {
            case TAuraActionCardModifyAttribute modify: return modify.AttributeType;
            case TAuraActionPlayerModifyAttribute modify: return ConvertPlayerAttribute(modify.AttributeType);
            default: return null;
        }
    }

    private static ECardAttributeType? ConvertPlayerAttribute(EPlayerAttributeType attribute)
    {
        switch (attribute)
        {
            case EPlayerAttributeType.Burn: return ECardAttributeType.BurnApplyAmount;
            case EPlayerAttributeType.CritChance: return ECardAttributeType.CritChance;
            case EPlayerAttributeType.DamageCrit: return ECardAttributeType.DamageAmount;
            case EPlayerAttributeType.Joy:
            case EPlayerAttributeType.JoyCrit: return ECardAttributeType.JoyApplyAmount;
            case EPlayerAttributeType.Health:
            case EPlayerAttributeType.HealthMax:
            case EPlayerAttributeType.HealAmount:
            case EPlayerAttributeType.HealCrit: return ECardAttributeType.HealAmount;
            case EPlayerAttributeType.Poison: return ECardAttributeType.PoisonApplyAmount;
            case EPlayerAttributeType.Shield:
            case EPlayerAttributeType.ShieldCrit: return ECardAttributeType.ShieldApplyAmount;
            case EPlayerAttributeType.Gold:
            case EPlayerAttributeType.Income: return ECardAttributeType.SellPrice;
            case EPlayerAttributeType.Tempo: return ECardAttributeType.TempoCost;
            case EPlayerAttributeType.Experience: return ECardAttributeType.Custom_8;
            case EPlayerAttributeType.HealthRegen: return ECardAttributeType.Custom_7;
            case EPlayerAttributeType.Rage:
            case EPlayerAttributeType.RageMax: return ECardAttributeType.RageApplyAmount;
            default: return null;
        }
    }
}
