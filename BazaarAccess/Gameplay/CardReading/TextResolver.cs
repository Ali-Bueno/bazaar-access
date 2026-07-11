using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Combat;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Cards.Enchantments;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Quests;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Effect.AuraActions;
using BazaarGameShared.Domain.Tooltips;
using BazaarGameShared.Domain.Values;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Localization;
using TheBazaar.Utilities;

namespace BazaarAccess.Gameplay.CardReading;

/// <summary>
/// Resolves localized text and token values from card data.
/// </summary>
internal static class TextResolver
{
    // Regex for tokens like {DamageAmount}, {Cooldown}, etc.
    private static readonly Regex TokenRegex = new Regex(
        @"\{(\w+)(?::(\w+))?\}",
        RegexOptions.Compiled);

    // Regex to detect millisecond values that should be seconds
    private static readonly Regex MillisecondsInTextRegex = new Regex(
        @"(\d{3,})\s*(second|sec)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Token name to attribute type mapping
    internal static readonly Dictionary<string, ECardAttributeType> TokenToAttribute = new Dictionary<string, ECardAttributeType>(StringComparer.OrdinalIgnoreCase)
    {
        { "DamageAmount", ECardAttributeType.DamageAmount },
        { "Damage", ECardAttributeType.DamageAmount },
        { "HealAmount", ECardAttributeType.HealAmount },
        { "Heal", ECardAttributeType.HealAmount },
        { "ShieldApplyAmount", ECardAttributeType.ShieldApplyAmount },
        { "Shield", ECardAttributeType.ShieldApplyAmount },
        { "PoisonApplyAmount", ECardAttributeType.PoisonApplyAmount },
        { "Poison", ECardAttributeType.PoisonApplyAmount },
        { "BurnApplyAmount", ECardAttributeType.BurnApplyAmount },
        { "Burn", ECardAttributeType.BurnApplyAmount },
        { "Cooldown", ECardAttributeType.Cooldown },
        { "CooldownMax", ECardAttributeType.CooldownMax },
        { "Ammo", ECardAttributeType.Ammo },
        { "AmmoMax", ECardAttributeType.AmmoMax },
        { "HasteAmount", ECardAttributeType.HasteAmount },
        { "Haste", ECardAttributeType.HasteAmount },
        { "SlowAmount", ECardAttributeType.SlowAmount },
        { "Slow", ECardAttributeType.SlowAmount },
        { "FreezeAmount", ECardAttributeType.FreezeAmount },
        { "Freeze", ECardAttributeType.FreezeAmount },
        { "ChargeAmount", ECardAttributeType.ChargeAmount },
        { "Charge", ECardAttributeType.ChargeAmount },
        { "CritChance", ECardAttributeType.CritChance },
        { "Crit", ECardAttributeType.CritChance },
        { "Lifesteal", ECardAttributeType.Lifesteal },
        { "Multicast", ECardAttributeType.Multicast },
        { "RegenApplyAmount", ECardAttributeType.RegenApplyAmount },
        { "Regen", ECardAttributeType.RegenApplyAmount },
        { "JoyApplyAmount", ECardAttributeType.JoyApplyAmount },
        { "Joy", ECardAttributeType.JoyApplyAmount },
        { "Counter", ECardAttributeType.Counter },
        { "BuyPrice", ECardAttributeType.BuyPrice },
        { "SellPrice", ECardAttributeType.SellPrice },
        { "ReloadAmount", ECardAttributeType.ReloadAmount },
        { "RepairTargets", ECardAttributeType.RepairTargets },
        { "Repair", ECardAttributeType.RepairTargets },
    };

    // Attributes stored in milliseconds that need conversion to seconds
    internal static readonly HashSet<ECardAttributeType> MillisecondAttributes = new HashSet<ECardAttributeType>
    {
        ECardAttributeType.Cooldown,
        ECardAttributeType.CooldownMax,
        ECardAttributeType.HasteAmount,
        ECardAttributeType.SlowAmount,
        ECardAttributeType.FreezeAmount,
        ECardAttributeType.ChargeAmount
    };

    /// <summary>
    /// Gets localized text from a TLocalizableText, cleaning HTML tags.
    /// </summary>
    public static string GetLocalizedText(TLocalizableText text)
    {
        if (text == null) return string.Empty;

        string result = string.Empty;

        try
        {
            var locService = Services.Get<LocalizationService>();
            if (locService != null && locService.TryGetText(text, out var translation))
            {
                result = translation;
            }
        }
        catch
        {
            // Fallback to default text
        }

        if (string.IsNullOrEmpty(result))
        {
            result = text.Text ?? string.Empty;
        }

        return TextHelper.CleanText(result);
    }

    /// <summary>
    /// Gets localized text with tokens resolved using card attribute values.
    /// Uses the game's tooltip system first, falls back to regex replacement.
    /// </summary>
    public static string GetLocalizedTextWithValues(TLocalizableText text, Card card)
    {
        string localizedText = GetLocalizedText(text);
        if (string.IsNullOrEmpty(localizedText) || card == null)
            return localizedText;

        string resolved = null;

        // Try the game's tooltip builder first for ability tokens
        try
        {
            var run = Data.Run;
            if (run != null)
            {
                var valueContext = new ValueContext(run, card, null);
                var tooltipContext = new TooltipContext(card, card.Template, valueContext);

                var builder = TooltipBuilder.Create(tooltipContext, localizedText);
                resolved = RenderWithAttributeNames(builder);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"TooltipBuilder failed, falling back to regex: {ex.Message}");
        }

        // Fallback to regex token resolution
        if (string.IsNullOrEmpty(resolved))
        {
            resolved = ResolveTokens(localizedText, card);
        }

        // Post-process to convert milliseconds to seconds
        resolved = ConvertMillisecondsInText(resolved);

        return resolved;
    }

    /// <summary>
    /// Renders a tooltip, appending the attribute name after aura value tokens. Aura tooltips
    /// (e.g. "Your items have +{aura.0}") show the attribute only as an icon, so a screen reader
    /// would otherwise hear "+1" with no unit. Skipped when the next text already spells it out.
    /// </summary>
    private static string RenderWithAttributeNames(TooltipBuilder builder)
    {
        var components = builder.Components;
        var sb = new StringBuilder();

        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];
            component.Render(sb);

            // Only aura tokens hide their attribute behind an icon; ability/attribute
            // tokens already have the attribute word spelled out in the template text.
            if (component is TooltipComponentAura aura && aura.Resolve().HasValue)
            {
                string keyword = GetAuraAttributeName(aura);
                if (!string.IsNullOrEmpty(keyword) && !NextTextStartsWith(components, i, keyword))
                {
                    sb.Append(' ');
                    sb.Append(keyword);
                }
            }
        }

        return sb.ToString();
    }

    // The buffed attribute lives on the aura's action (item or player modify), not on the value
    // token. Returns null when the aura doesn't modify an attribute.
    private static string GetAuraAttributeName(TooltipComponentAura aura)
    {
        var action = aura.Aura?.Action;

        if (action is TAuraActionCardModifyAttribute cardModify)
            return EffectFormatter.GetFriendlyAttributeName(cardModify.AttributeType.ToString());

        if (action is TAuraActionPlayerModifyAttribute playerModify)
            return GetPlayerAttributeName(playerModify.AttributeType);

        return null;
    }

    private static string GetPlayerAttributeName(EPlayerAttributeType attribute)
    {
        return attribute switch
        {
            EPlayerAttributeType.Health => "health",
            EPlayerAttributeType.HealthMax => "max health",
            EPlayerAttributeType.HealthRegen => "health regen",
            EPlayerAttributeType.HealAmount => "heal",
            EPlayerAttributeType.Gold => "gold",
            EPlayerAttributeType.Income => "income",
            EPlayerAttributeType.CritChance => "crit chance",
            EPlayerAttributeType.Shield => "shield",
            EPlayerAttributeType.Burn => "burn",
            EPlayerAttributeType.Poison => "poison",
            EPlayerAttributeType.Rage => "rage",
            EPlayerAttributeType.RageMax => "max rage",
            EPlayerAttributeType.Experience => "experience",
            EPlayerAttributeType.Level => "level",
            EPlayerAttributeType.Prestige => "prestige",
            _ => attribute.ToString().ToLower()
        };
    }

    // True if the text right after index i already begins with the attribute keyword.
    private static bool NextTextStartsWith(List<ITooltipComponent> components, int i, string keyword)
    {
        if (i + 1 >= components.Count) return false;
        if (!(components[i + 1] is TooltipComponentText textComponent)) return false;

        string next = textComponent.Content;
        if (string.IsNullOrEmpty(next)) return false;

        next = next.TrimStart(' ', '+', '-', ',', '.', ':', ';');
        return next.StartsWith(keyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts millisecond values to seconds in text.
    /// Detects patterns like "1000 second" and converts to "1 second".
    /// </summary>
    private static string ConvertMillisecondsInText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return MillisecondsInTextRegex.Replace(text, match =>
        {
            string numberStr = match.Groups[1].Value;
            string unit = match.Groups[2].Value;

            if (int.TryParse(numberStr, out int milliseconds) && milliseconds >= 100)
            {
                float seconds = milliseconds / 1000f;
                string formattedSeconds = seconds == (int)seconds
                    ? ((int)seconds).ToString()
                    : seconds.ToString("F1");

                return $"{formattedSeconds} {unit}";
            }

            return match.Value;
        });
    }

    /// <summary>
    /// Resolves {X} tokens in text with actual card attribute values.
    /// Fallback when the game's TooltipBuilder is unavailable.
    /// </summary>
    private static string ResolveTokens(string text, Card card)
    {
        if (string.IsNullOrEmpty(text) || card == null)
            return text;

        return TokenRegex.Replace(text, match =>
        {
            string tokenName = match.Groups[1].Value;

            if (TokenToAttribute.TryGetValue(tokenName, out var attrType))
            {
                var value = card.GetAttributeValue(attrType);
                if (value.HasValue)
                {
                    if (MillisecondAttributes.Contains(attrType))
                    {
                        float displayValue = value.Value / 1000f;
                        return displayValue.ToString("F1") + "s";
                    }

                    return value.Value.ToString();
                }
            }

            return match.Value;
        });
    }
}
