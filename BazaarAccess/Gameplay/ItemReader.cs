using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameClient.Domain.Tooltips;
using BazaarGameShared.Domain.Cards.Enchantments;
using BazaarGameShared.Domain.Cards.Item;
using BazaarGameShared.Domain.Cards.Quests;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Tooltips;
using BazaarGameShared.Domain.Values;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Localization;
using TheBazaar.Utilities;

// Para limpiar tags HTML de textos

namespace BazaarAccess.Gameplay;

/// <summary>
/// Lee información de items/cartas para accesibilidad.
/// </summary>
public static class ItemReader
{
    // Regex para tokens como {DamageAmount}, {Cooldown}, etc.
    private static readonly Regex TokenRegex = new Regex(
        @"\{(\w+)(?::(\w+))?\}",
        RegexOptions.Compiled);

    // Regex para detectar valores en milisegundos que deberían ser segundos
    // Patrones: "for 1000 second", "1000 second(s)", etc.
    private static readonly Regex MillisecondsInTextRegex = new Regex(
        @"(\d{3,})\s*(second|sec)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Mapeo de nombres de token a tipos de atributo
    private static readonly Dictionary<string, ECardAttributeType> TokenToAttribute = new Dictionary<string, ECardAttributeType>(StringComparer.OrdinalIgnoreCase)
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

    // Atributos que están en milisegundos y necesitan conversión a segundos
    private static readonly HashSet<ECardAttributeType> MillisecondAttributes = new HashSet<ECardAttributeType>
    {
        ECardAttributeType.Cooldown,
        ECardAttributeType.CooldownMax,
        ECardAttributeType.HasteAmount,
        ECardAttributeType.SlowAmount,
        ECardAttributeType.FreezeAmount,
        ECardAttributeType.ChargeAmount
    };

    /// <summary>
    /// Obtiene el texto localizado de un TLocalizableText, limpiando tags HTML.
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
            // Fallback al texto por defecto
        }

        if (string.IsNullOrEmpty(result))
        {
            result = text.Text ?? string.Empty;
        }

        // Limpiar tags HTML
        return TextHelper.CleanText(result);
    }

    /// <summary>
    /// Obtiene el texto localizado con los tokens resueltos usando los valores de la carta.
    /// Usa el sistema de tooltips del juego para resolver tokens como {ability.0}, {DamageAmount}, etc.
    /// </summary>
    public static string GetLocalizedTextWithValues(TLocalizableText text, Card card)
    {
        string localizedText = GetLocalizedText(text);
        if (string.IsNullOrEmpty(localizedText) || card == null)
            return localizedText;

        string resolved = null;

        // Primero intentar usar el sistema de tooltips del juego para resolver tokens de abilities
        try
        {
            var run = Data.Run;
            if (run != null)
            {
                var valueContext = new ValueContext(run, card, null);
                var tooltipContext = new TooltipContext
                {
                    Instance = card,
                    Template = card.Template,
                    ValueContext = valueContext
                };

                var builder = TooltipBuilder.Create(tooltipContext, localizedText);
                resolved = builder.Render(true);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"TooltipBuilder failed, falling back to regex: {ex.Message}");
        }

        // Si el TooltipBuilder no funcionó, usar regex
        if (string.IsNullOrEmpty(resolved))
        {
            resolved = ResolveTokens(localizedText, card);
        }

        // Post-procesar para convertir milisegundos a segundos
        // El TooltipBuilder devuelve valores crudos sin aplicar transformers
        resolved = ConvertMillisecondsInText(resolved);

        return resolved;
    }

    /// <summary>
    /// Convierte valores en milisegundos a segundos en el texto.
    /// Detecta patrones como "1000 second" y los convierte a "1 second".
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
                // Convertir a segundos
                float seconds = milliseconds / 1000f;

                // Formatear: si es entero, mostrar sin decimales
                string formattedSeconds = seconds == (int)seconds
                    ? ((int)seconds).ToString()
                    : seconds.ToString("F1");

                return $"{formattedSeconds} {unit}";
            }

            return match.Value;
        });
    }

    /// <summary>
    /// Resuelve los tokens {X} en el texto con los valores reales de los atributos de la carta.
    /// Este es el fallback cuando el TooltipBuilder del juego no está disponible.
    /// </summary>
    private static string ResolveTokens(string text, Card card)
    {
        if (string.IsNullOrEmpty(text) || card == null)
            return text;

        return TokenRegex.Replace(text, match =>
        {
            string tokenName = match.Groups[1].Value;
            string format = match.Groups[2].Success ? match.Groups[2].Value : null;

            // Buscar el atributo correspondiente
            if (TokenToAttribute.TryGetValue(tokenName, out var attrType))
            {
                var value = card.GetAttributeValue(attrType);
                if (value.HasValue)
                {
                    float displayValue = value.Value;

                    // Convertir milisegundos a segundos si es necesario
                    if (MillisecondAttributes.Contains(attrType))
                    {
                        displayValue = value.Value / 1000f;
                        return displayValue.ToString("F1") + "s";
                    }

                    return displayValue.ToString();
                }
            }

            // Si no encontramos el atributo, devolver el token original
            return match.Value;
        });
    }

    /// <summary>
    /// Obtiene el nombre localizado de una carta.
    /// </summary>
    public static string GetCardName(Card card)
    {
        if (card == null) return "Empty";

        var template = card.Template;
        string baseName = string.Empty;

        if (template?.Localization?.Title != null)
        {
            baseName = GetLocalizedText(template.Localization.Title);
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = template?.InternalName ?? "Unknown";
        }

        // Check if the card is enchanted and prepend enchantment name
        if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
        {
            string enchantName = GetEnchantmentName(itemCard.Enchantment.Value);
            if (!string.IsNullOrEmpty(enchantName))
            {
                return $"{enchantName} {baseName}";
            }
        }

        return baseName;
    }

    /// <summary>
    /// Gets the localized name of an enchantment type.
    /// </summary>
    public static string GetEnchantmentName(EEnchantmentType enchantment)
    {
        try
        {
            // Try to get localized name from the game's localization system
            var locText = new LocalizableText(enchantment.ToString());
            string localized = locText.GetLocalizedText();
            if (!string.IsNullOrEmpty(localized))
                return localized;

            // Fallback to enum name
            return enchantment.ToString();
        }
        catch
        {
            return enchantment.ToString();
        }
    }

    /// <summary>
    /// Obtiene el tier de una carta como string.
    /// </summary>
    public static string GetTierName(Card card)
    {
        if (card == null) return string.Empty;

        return card.Tier switch
        {
            ETier.Bronze => "Bronze",
            ETier.Silver => "Silver",
            ETier.Gold => "Gold",
            ETier.Diamond => "Diamond",
            ETier.Legendary => "Legendary",
            _ => card.Tier.ToString()
        };
    }

    /// <summary>
    /// Gets the size name of a card (small/medium/large).
    /// </summary>
    public static string GetSizeName(Card card)
    {
        var template = card?.Template;
        if (template == null) return "";

        return template.Size switch
        {
            ECardSize.Small => "small",
            ECardSize.Medium => "medium",
            ECardSize.Large => "large",
            _ => ""
        };
    }

    /// <summary>
    /// Obtiene el precio de compra de un item.
    /// </summary>
    public static int GetBuyPrice(Card card)
    {
        if (card == null) return 0;
        return card.GetAttributeValue(ECardAttributeType.BuyPrice) ?? 0;
    }

    /// <summary>
    /// Obtiene el precio de venta de un item.
    /// </summary>
    public static int GetSellPrice(Card card)
    {
        if (card == null) return 0;
        return card.GetAttributeValue(ECardAttributeType.SellPrice) ?? 0;
    }

    /// <summary>
    /// Tags que son relevantes para el usuario (tipos de item).
    /// Excluye tags técnicos como Unsellable, Unstashable, etc.
    /// </summary>
    private static readonly HashSet<ECardTag> RelevantTags = new HashSet<ECardTag>
    {
        ECardTag.Weapon,
        ECardTag.Property,
        ECardTag.Food,
        ECardTag.Potion,
        ECardTag.Tool,
        ECardTag.Vehicle,
        ECardTag.Aquatic,
        ECardTag.Friend,
        ECardTag.Core,
        ECardTag.Ray,
        ECardTag.Dinosaur,
        ECardTag.Apparel,
        ECardTag.Toy,
        ECardTag.Tech,
        ECardTag.Dragon,
        ECardTag.Ingredient,
        ECardTag.Relic,
        ECardTag.Reagent,
        ECardTag.Map,
        ECardTag.Key,
        ECardTag.Drone,
        ECardTag.Loot
    };

    /// <summary>
    /// Obtiene los tags/tipos de una carta (ej: "Aquatic, Friend").
    /// Solo devuelve tags relevantes para el usuario.
    /// </summary>
    public static string GetTags(Card card)
    {
        if (card == null || card.Tags == null || card.Tags.Count == 0)
            return string.Empty;

        var relevantTags = card.Tags
            .Where(t => RelevantTags.Contains(t))
            .Select(t => t.ToString())
            .ToList();

        return string.Join(", ", relevantTags);
    }

    /// <summary>
    /// Checks if a card is a quest item (has Quest hidden tag).
    /// </summary>
    public static bool IsQuestItem(Card card)
    {
        if (card == null) return false;
        return card.HiddenTags != null && card.HiddenTags.Contains(EHiddenTag.Quest);
    }

    /// <summary>
    /// Gets quest condition lines for a quest item, showing requirements and progress.
    /// Returns empty list if the card is not a quest item or has no quest data.
    /// </summary>
    public static List<string> GetQuestLines(Card card)
    {
        var lines = new List<string>();
        if (card == null || !IsQuestItem(card)) return lines;

        try
        {
            var template = card.Template as TCardItem;
            if (template?.Quests == null || template.Quests.Count == 0) return lines;

            foreach (var questGroup in template.Quests)
            {
                if (questGroup?.Entries == null) continue;

                foreach (var entry in questGroup.Entries)
                {
                    if (entry == null) continue;

                    // Get quest description from localization tooltips
                    string questDesc = null;
                    if (entry.Localization?.Tooltips != null)
                    {
                        foreach (var tooltip in entry.Localization.Tooltips)
                        {
                            if (tooltip?.Content != null)
                            {
                                string text = GetLocalizedTextWithValues(tooltip.Content, card);
                                if (!string.IsNullOrEmpty(text))
                                {
                                    questDesc = text;
                                    break;
                                }
                            }
                        }
                    }

                    // Get progress: current value and target
                    int current = card.GetAttributeValue(entry.AttributeType) ?? 0;
                    int target = entry.Target;
                    bool isComplete = current >= target;

                    if (!string.IsNullOrEmpty(questDesc))
                    {
                        string status = isComplete ? "Complete" : $"{current}/{target}";
                        lines.Add($"Quest: {questDesc} ({status})");
                    }
                    else
                    {
                        // Fallback if no localization available
                        string status = isComplete ? "Complete" : $"{current}/{target}";
                        lines.Add($"Quest: {status}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetQuestLines error: {ex.Message}");
        }

        return lines;
    }

    /// <summary>
    /// Gets a compact quest progress summary for short descriptions.
    /// Returns null if not a quest item.
    /// </summary>
    public static string GetQuestProgress(Card card)
    {
        if (card == null || !IsQuestItem(card)) return null;

        try
        {
            var template = card.Template as TCardItem;
            if (template?.Quests == null || template.Quests.Count == 0) return null;

            foreach (var questGroup in template.Quests)
            {
                if (questGroup?.Entries == null) continue;
                foreach (var entry in questGroup.Entries)
                {
                    if (entry == null) continue;
                    int current = card.GetAttributeValue(entry.AttributeType) ?? 0;
                    int target = entry.Target;
                    bool isComplete = current >= target;
                    return isComplete ? "Quest complete" : $"Quest {current}/{target}";
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetQuestProgress error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Obtiene el estado de temperatura de un item (Heated/Chilled para Jules).
    /// </summary>
    public static string GetTemperatureState(Card card)
    {
        if (card == null) return string.Empty;

        bool isHeated = card.GetAttributeValue(ECardAttributeType.Heated) > 0;
        bool isChilled = card.GetAttributeValue(ECardAttributeType.Chilled) > 0;

        if (isHeated && isChilled)
            return "Heated and Chilled";
        if (isHeated)
            return "Heated";
        if (isChilled)
            return "Chilled";

        return string.Empty;
    }

    /// <summary>
    /// Verifica si un item está Heated (caliente).
    /// </summary>
    public static bool IsHeated(Card card)
    {
        if (card == null) return false;
        return card.GetAttributeValue(ECardAttributeType.Heated) > 0;
    }

    /// <summary>
    /// Verifica si un item está Chilled (frío).
    /// </summary>
    public static bool IsChilled(Card card)
    {
        if (card == null) return false;
        return card.GetAttributeValue(ECardAttributeType.Chilled) > 0;
    }

    /// <summary>
    /// Obtiene un resumen corto del item para navegación rápida.
    /// Formato: "Nombre, Tier" o "Nombre, Tier, Heated/Chilled"
    /// </summary>
    public static string GetShortDescription(Card card)
    {
        if (card == null) return "Empty slot";

        string name = GetCardName(card);
        string tier = GetTierName(card);
        string tempState = GetTemperatureState(card);
        bool isQuest = IsQuestItem(card);

        var parts = new List<string> { name, tier };

        if (isQuest)
        {
            string questProgress = GetQuestProgress(card);
            parts.Add(questProgress ?? "Quest");
        }

        if (!string.IsNullOrEmpty(tempState))
            parts.Add(tempState);

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Obtiene información detallada del item.
    /// Incluye todos los stats y la descripción.
    /// </summary>
    public static string GetDetailedDescription(Card card)
    {
        if (card == null) return "Empty slot";

        var sb = new StringBuilder();

        // Nombre y tier
        sb.Append(GetCardName(card));
        sb.Append(", ");
        sb.Append(GetTierName(card));

        // Estado de temperatura (Heated/Chilled para Jules)
        string tempState = GetTemperatureState(card);
        if (!string.IsNullOrEmpty(tempState))
        {
            sb.Append($", {tempState}");
        }

        // Tamaño
        var template = card.Template;
        if (template != null)
        {
            sb.Append($", Size {(int)template.Size}");
        }

        // Cooldown (convertir de ms a segundos)
        // Try Cooldown first, fallback to CooldownMax (base cooldown shown in UI)
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (!cooldown.HasValue || cooldown.Value <= 0)
        {
            cooldown = card.GetAttributeValue(ECardAttributeType.CooldownMax);
        }
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            float seconds = cooldown.Value / 1000f;
            sb.Append($", Cooldown {seconds:F1}s");
        }

        // Stats de combate
        AppendStatIfPresent(sb, card, ECardAttributeType.Ammo, "Ammo");
        AppendStatIfPresent(sb, card, ECardAttributeType.AmmoMax, "Max Ammo");
        AppendStatIfPresent(sb, card, ECardAttributeType.DamageAmount, "Damage");
        AppendStatIfPresent(sb, card, ECardAttributeType.HealAmount, "Heal");
        AppendStatIfPresent(sb, card, ECardAttributeType.ShieldApplyAmount, "Shield");
        AppendStatIfPresent(sb, card, ECardAttributeType.PoisonApplyAmount, "Poison");
        AppendStatIfPresent(sb, card, ECardAttributeType.BurnApplyAmount, "Burn");
        AppendStatIfPresent(sb, card, ECardAttributeType.RegenApplyAmount, "Regen");

        // Stats de velocidad
        AppendStatIfPresent(sb, card, ECardAttributeType.HasteAmount, "Haste");
        AppendStatIfPresent(sb, card, ECardAttributeType.SlowAmount, "Slow");
        AppendStatIfPresent(sb, card, ECardAttributeType.FreezeAmount, "Freeze");
        AppendStatIfPresent(sb, card, ECardAttributeType.ChargeAmount, "Charge");

        // Otros stats
        AppendStatIfPresent(sb, card, ECardAttributeType.CritChance, "Crit%");
        AppendStatIfPresent(sb, card, ECardAttributeType.Lifesteal, "Lifesteal");
        AppendStatIfPresent(sb, card, ECardAttributeType.Multicast, "Multicast");
        AppendStatIfPresent(sb, card, ECardAttributeType.RepairTargets, "Repair Targets");

        // Descripción del item y tooltips de habilidades
        string fullDesc = GetFullDescription(card);
        if (!string.IsNullOrEmpty(fullDesc))
        {
            sb.Append(". ");
            sb.Append(fullDesc);
        }

        // Flavor text
        string flavor = GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            sb.Append(". ");
            sb.Append(flavor);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Obtiene información de precio para compra.
    /// </summary>
    public static string GetBuyInfo(Card card)
    {
        if (card == null) return string.Empty;

        string name = GetCardName(card);
        int price = GetBuyPrice(card);

        return $"{name}, {price} gold";
    }

    /// <summary>
    /// Obtiene información de precio para venta.
    /// </summary>
    public static string GetSellInfo(Card card)
    {
        if (card == null) return string.Empty;

        string name = GetCardName(card);
        int price = GetSellPrice(card);

        return $"{name}, sells for {price} gold";
    }

    private static void AppendStatIfPresent(StringBuilder sb, Card card, ECardAttributeType type, string label)
    {
        var value = card.GetAttributeValue(type);
        if (value.HasValue && value.Value > 0)
        {
            sb.Append($", {label} {value.Value}");
        }
    }

    /// <summary>
    /// Obtiene información básica de un encuentro.
    /// Para PvP, muestra el nombre del jugador oponente si está disponible.
    /// </summary>
    public static string GetEncounterInfo(Card card)
    {
        if (card == null) return "Empty";

        string name = GetCardName(card);
        string type = GetEncounterTypeName(card.Type);
        string tier = GetTierName(card);

        // Para PvP encounters, incluir el nombre del oponente y rango si están disponibles
        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                // Use Hero enum for correct hero name instead of card template (which may be wrong)
                string heroName = GetPvpOpponentHeroName(pvpOpponent) ?? name;
                string rank = GetPvpOpponentRank(pvpOpponent);
                if (!string.IsNullOrEmpty(rank))
                {
                    return $"{pvpOpponent.Name}, {heroName}, {type}, {rank}";
                }
                return $"{pvpOpponent.Name}, {heroName}, {type}, {tier}";
            }
        }

        return $"{name}, {type}, {tier}";
    }

    /// <summary>
    /// Obtiene información detallada de un encuentro.
    /// Para PvP, incluye nombre, nivel, victorias y prestigio del oponente.
    /// </summary>
    public static string GetEncounterDetailedInfo(Card card)
    {
        if (card == null) return "Empty";

        var sb = new StringBuilder();

        // Para PvP, incluir info detallada del oponente
        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                sb.Append(pvpOpponent.Name);
                sb.Append(", ");
                // Use Hero enum for correct hero name
                string heroName = GetPvpOpponentHeroName(pvpOpponent) ?? GetCardName(card);
                sb.Append(heroName);

                // Incluir rango si está disponible
                string rank = GetPvpOpponentRank(pvpOpponent);
                if (!string.IsNullOrEmpty(rank))
                {
                    sb.Append(", ");
                    sb.Append(rank);
                }

                sb.Append(", Level ");
                sb.Append(pvpOpponent.Level);
                sb.Append(", ");
                sb.Append(pvpOpponent.Victories);
                sb.Append(" wins, ");
                sb.Append(pvpOpponent.Prestige);
                sb.Append(" prestige");
                return sb.ToString();
            }
        }

        // Fallback para otros tipos de encuentros
        sb.Append(GetCardName(card));
        sb.Append(", ");
        sb.Append(GetEncounterTypeName(card.Type));

        // Descripción del encuentro (si existe)
        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
        {
            sb.Append(". ");
            sb.Append(desc);
        }

        // Flavor text (historia/narrativa)
        string flavor = GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            sb.Append(". ");
            sb.Append(flavor);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Obtiene el nombre del tipo de encuentro.
    /// </summary>
    private static string GetEncounterTypeName(ECardType type)
    {
        return type switch
        {
            ECardType.CombatEncounter => "Combat",
            ECardType.EventEncounter => "Event",
            ECardType.PedestalEncounter => "Upgrade",
            ECardType.EncounterStep => "Path",
            ECardType.PvpEncounter => "PvP",
            _ => "Encounter"
        };
    }

    /// <summary>
    /// Obtiene el texto de sabor (FlavorText) de una carta.
    /// </summary>
    public static string GetFlavorText(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        if (template?.Localization?.FlavorText != null)
        {
            return GetLocalizedText(template.Localization.FlavorText);
        }

        return string.Empty;
    }

    /// <summary>
    /// Obtiene la descripción de una carta con valores resueltos.
    /// </summary>
    public static string GetDescription(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        if (template?.Localization?.Description != null)
        {
            return GetLocalizedTextWithValues(template.Localization.Description, card);
        }

        return string.Empty;
    }

    /// <summary>
    /// Obtiene los tooltips de habilidades activas y pasivas de una carta con valores resueltos.
    /// </summary>
    public static string GetAbilityTooltips(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        var sb = new StringBuilder();

        // Get base tooltips from template
        var tooltips = template?.Localization?.Tooltips;
        if (tooltips != null && tooltips.Count > 0)
        {
            foreach (var tooltip in tooltips)
            {
                if (tooltip?.Content != null)
                {
                    string text = GetLocalizedTextWithValues(tooltip.Content, card);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(text);
                    }
                }
            }
        }

        // Get enchantment tooltips if the card is enchanted
        if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
        {
            var enchantmentTooltips = GetEnchantmentTooltips(itemCard);
            if (!string.IsNullOrEmpty(enchantmentTooltips))
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(enchantmentTooltips);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the tooltips from an item's enchantment.
    /// </summary>
    private static string GetEnchantmentTooltips(ItemCard itemCard)
    {
        if (!itemCard.Enchantment.HasValue) return string.Empty;

        try
        {
            var template = itemCard.Template as TCardItem;
            if (template == null) return string.Empty;

            // Try to get enchantment template
            if (!template.TryGetEnchantmentTemplate(itemCard.Enchantment.Value, out TEnchantment enchantmentTemplate))
                return string.Empty;

            if (enchantmentTemplate?.Localization?.Tooltips == null)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var tooltip in enchantmentTemplate.Localization.Tooltips)
            {
                if (tooltip?.Content != null)
                {
                    string text = GetLocalizedTextWithValues(tooltip.Content, itemCard);
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (sb.Length > 0) sb.Append(". ");
                        sb.Append(text);
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetEnchantmentTooltips error: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtiene la descripción completa de una carta incluyendo tooltips de habilidades.
    /// </summary>
    public static string GetFullDescription(Card card)
    {
        if (card == null) return string.Empty;

        var parts = new List<string>();

        // Descripción básica
        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
            parts.Add(desc);

        // Tooltips de habilidades
        string abilities = GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities))
            parts.Add(abilities);

        return string.Join(". ", parts);
    }

    /// <summary>
    /// Obtiene las líneas de detalle separadas para navegación Ctrl+Up/Down.
    /// Sin prefijos innecesarios para una lectura más limpia.
    /// </summary>
    public static List<string> GetDetailLines(Card card)
    {
        var lines = new List<string>();
        if (card == null) return lines;

        // Para encuentros PvP, devolver info detallada del oponente
        if (card.Type == ECardType.PvpEncounter)
        {
            return GetPvpEncounterDetailLines(card);
        }

        // Nombre
        lines.Add(GetCardName(card));

        // Tier
        lines.Add(GetTierName(card));

        // Quest conditions and progress
        if (IsQuestItem(card))
        {
            var questLines = GetQuestLines(card);
            if (questLines.Count > 0)
            {
                lines.AddRange(questLines);
            }
            else
            {
                lines.Add("Quest item");
            }
        }

        // Tags/Tipos (Aquatic, Friend, Weapon, etc.)
        string tags = GetTags(card);
        if (!string.IsNullOrEmpty(tags))
        {
            lines.Add(tags);
        }

        // Estado de temperatura (Heated/Chilled para Jules)
        string tempState = GetTemperatureState(card);
        if (!string.IsNullOrEmpty(tempState))
        {
            lines.Add($"State: {tempState}");
        }

        // Enchantment status
        if (card is ItemCard enchantedItem && enchantedItem.Enchantment.HasValue)
        {
            string enchantName = GetEnchantmentName(enchantedItem.Enchantment.Value);
            lines.Add($"Enchanted: {enchantName}");
        }

        // Tamaño con nombre descriptivo
        var template = card.Template;
        if (template != null)
        {
            int size = (int)template.Size;
            string sizeName = template.Size switch
            {
                ECardSize.Small => "Small",
                ECardSize.Medium => "Medium",
                ECardSize.Large => "Large",
                _ => ""
            };
            lines.Add($"Size: {size} slots ({sizeName})");
        }

        // Precio de compra
        int buyPrice = GetBuyPrice(card);
        if (buyPrice > 0)
        {
            lines.Add($"Buy {buyPrice} gold");
        }

        // Precio de venta
        int sellPrice = GetSellPrice(card);
        if (sellPrice > 0)
        {
            lines.Add($"Sell {sellPrice} gold");
        }

        // Cooldown
        // Try Cooldown first, fallback to CooldownMax (base cooldown shown in UI)
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (!cooldown.HasValue || cooldown.Value <= 0)
        {
            cooldown = card.GetAttributeValue(ECardAttributeType.CooldownMax);
        }
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            float seconds = cooldown.Value / 1000f;
            lines.Add($"Cooldown {seconds:F1} seconds");
        }

        // Stats de combate
        AddStatLine(lines, card, ECardAttributeType.Ammo, "Ammo");
        AddStatLine(lines, card, ECardAttributeType.AmmoMax, "Max Ammo");
        AddStatLine(lines, card, ECardAttributeType.DamageAmount, "Damage");
        AddStatLine(lines, card, ECardAttributeType.HealAmount, "Heal");
        AddStatLine(lines, card, ECardAttributeType.ShieldApplyAmount, "Shield");
        AddStatLine(lines, card, ECardAttributeType.PoisonApplyAmount, "Poison");
        AddStatLine(lines, card, ECardAttributeType.BurnApplyAmount, "Burn");
        AddStatLine(lines, card, ECardAttributeType.RegenApplyAmount, "Regeneration");

        // Stats de velocidad
        AddStatLine(lines, card, ECardAttributeType.HasteAmount, "Haste");
        AddStatLine(lines, card, ECardAttributeType.SlowAmount, "Slow");
        AddStatLine(lines, card, ECardAttributeType.FreezeAmount, "Freeze");
        AddStatLine(lines, card, ECardAttributeType.ChargeAmount, "Charge");

        // Otros stats
        AddStatLine(lines, card, ECardAttributeType.CritChance, "Crit Chance");
        AddStatLine(lines, card, ECardAttributeType.Lifesteal, "Lifesteal");
        AddStatLine(lines, card, ECardAttributeType.Multicast, "Multicast");
        AddStatLine(lines, card, ECardAttributeType.RepairTargets, "Repair Targets");

        // Descripción básica (sin prefijo)
        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add(desc);
        }

        // Tooltips de habilidades (sin prefijo)
        string abilities = GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities))
        {
            lines.Add(abilities);
        }

        // Flavor text (sin prefijo)
        string flavor = GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            lines.Add(flavor);
        }

        return lines;
    }

    /// <summary>
    /// Gets detail lines optimized for enemy board reading.
    /// Prioritizes combat-relevant info: name, cooldown, stats, effects first.
    /// Metadata like tier, size, price comes at the end.
    /// </summary>
    public static List<string> GetEnemyDetailLines(Card card)
    {
        var lines = new List<string>();
        if (card == null) return lines;

        // Name first
        lines.Add(GetCardName(card));

        // Description and abilities first - what the item does
        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add(desc);
        }

        string abilities = GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities))
        {
            lines.Add(abilities);
        }

        // Cooldown
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (!cooldown.HasValue || cooldown.Value <= 0)
        {
            cooldown = card.GetAttributeValue(ECardAttributeType.CooldownMax);
        }
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            float seconds = cooldown.Value / 1000f;
            lines.Add($"Cooldown {seconds:F1} seconds");
        }

        // Combat stats
        AddStatLine(lines, card, ECardAttributeType.DamageAmount, "Damage");
        AddStatLine(lines, card, ECardAttributeType.HealAmount, "Heal");
        AddStatLine(lines, card, ECardAttributeType.ShieldApplyAmount, "Shield");
        AddStatLine(lines, card, ECardAttributeType.PoisonApplyAmount, "Poison");
        AddStatLine(lines, card, ECardAttributeType.BurnApplyAmount, "Burn");
        AddStatLine(lines, card, ECardAttributeType.RegenApplyAmount, "Regeneration");

        // Speed stats
        AddStatLine(lines, card, ECardAttributeType.HasteAmount, "Haste");
        AddStatLine(lines, card, ECardAttributeType.SlowAmount, "Slow");
        AddStatLine(lines, card, ECardAttributeType.FreezeAmount, "Freeze");
        AddStatLine(lines, card, ECardAttributeType.ChargeAmount, "Charge");

        // Other combat stats
        AddStatLine(lines, card, ECardAttributeType.Ammo, "Ammo");
        AddStatLine(lines, card, ECardAttributeType.AmmoMax, "Max Ammo");
        AddStatLine(lines, card, ECardAttributeType.CritChance, "Crit Chance");
        AddStatLine(lines, card, ECardAttributeType.Lifesteal, "Lifesteal");
        AddStatLine(lines, card, ECardAttributeType.Multicast, "Multicast");
        AddStatLine(lines, card, ECardAttributeType.RepairTargets, "Repair Targets");

        // Metadata at the end (less important for enemy analysis)
        lines.Add(GetTierName(card));

        string tags = GetTags(card);
        if (!string.IsNullOrEmpty(tags))
        {
            lines.Add(tags);
        }

        var template = card.Template;
        if (template != null)
        {
            int size = (int)template.Size;
            string sizeName = template.Size switch
            {
                ECardSize.Small => "Small",
                ECardSize.Medium => "Medium",
                ECardSize.Large => "Large",
                _ => ""
            };
            lines.Add($"Size: {size} slots ({sizeName})");
        }

        return lines;
    }

    /// <summary>
    /// Gets a compact combat-focused description for enemy item navigation.
    /// Format: "Name, Cooldown Xs, Damage X" (key stats only)
    /// </summary>
    public static string GetEnemyCompactDescription(Card card)
    {
        if (card == null) return "Empty slot";

        var parts = new List<string>();
        parts.Add(GetCardName(card));

        // Cooldown
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (!cooldown.HasValue || cooldown.Value <= 0)
        {
            cooldown = card.GetAttributeValue(ECardAttributeType.CooldownMax);
        }
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            float seconds = cooldown.Value / 1000f;
            parts.Add($"{seconds:F1}s");
        }

        // Key combat stat (just one primary stat for quick reading)
        var damage = card.GetAttributeValue(ECardAttributeType.DamageAmount);
        if (damage.HasValue && damage.Value > 0)
        {
            parts.Add($"{damage.Value} damage");
        }
        else
        {
            var heal = card.GetAttributeValue(ECardAttributeType.HealAmount);
            if (heal.HasValue && heal.Value > 0)
            {
                parts.Add($"{heal.Value} heal");
            }
            else
            {
                var shield = card.GetAttributeValue(ECardAttributeType.ShieldApplyAmount);
                if (shield.HasValue && shield.Value > 0)
                {
                    parts.Add($"{shield.Value} shield");
                }
            }
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Añade una línea de stat si tiene valor.
    /// </summary>
    private static void AddStatLine(List<string> lines, Card card, ECardAttributeType type, string label)
    {
        var value = card.GetAttributeValue(type);
        if (value.HasValue && value.Value != 0)
        {
            lines.Add($"{label}: {value.Value}");
        }
    }

    /// <summary>
    /// Obtiene las descripciones de las propiedades/tags de una carta.
    /// Usa el diccionario de leyendas del juego para obtener explicaciones.
    /// </summary>
    public static List<string> GetTagDescriptions(Card card)
    {
        var descriptions = new List<string>();
        if (card == null || card.Tags == null || card.Tags.Count == 0)
            return descriptions;

        foreach (var tag in card.Tags)
        {
            // Solo incluir tags relevantes para el usuario
            if (!RelevantTags.Contains(tag))
                continue;

            string tagName = tag.ToString();

            // Buscar en el diccionario de leyendas del juego
            if (Data.TooltipLegendStringDictionary.TryGetValue(tagName, out var symbol))
            {
                if (symbol != null && !string.IsNullOrEmpty(symbol.Keyword))
                {
                    // Obtener el texto localizado del keyword (descripción)
                    string description = new LocalizableText(symbol.Keyword).GetLocalizedText();
                    if (!string.IsNullOrEmpty(description))
                    {
                        // Limpiar HTML tags
                        description = TextHelper.CleanText(description);
                        descriptions.Add($"{tagName}: {description}");
                    }
                    else
                    {
                        descriptions.Add($"{tagName}: No description available");
                    }
                }
                else
                {
                    descriptions.Add($"{tagName}: No description available");
                }
            }
            else
            {
                // Si no hay descripción en el diccionario, mostrar solo el nombre
                descriptions.Add($"{tagName}: No description available");
            }
        }

        return descriptions;
    }

    /// <summary>
    /// Obtiene las descripciones de keywords mencionados en los tooltips de la carta.
    /// Incluye efectos como Burn, Poison, Haste, etc.
    /// </summary>
    public static List<string> GetKeywordDescriptions(Card card)
    {
        var descriptions = new List<string>();
        if (card == null) return descriptions;

        // Obtener el texto completo del tooltip para buscar keywords
        string fullDesc = GetFullDescription(card);
        if (string.IsNullOrEmpty(fullDesc)) return descriptions;

        // Lista de keywords que el juego soporta (de Data.BuildTooltipLegend)
        var keywordsToCheck = new[]
        {
            // Efectos de daño/curación
            "Damage", "Healing", "Shield", "Burn", "Poison", "Regen", "Joy",
            // Efectos de velocidad
            "Slow", "Haste", "Freeze", "Charge",
            // Efectos especiales
            "Crit Chance", "Multicast", "Lifesteal", "Flying",
            // Mecánicas
            "Ammo", "Cooldown", "Income", "Upgrade",
            // Estados
            "Heated", "Chilled",
            // Otros
            "Enchant", "Transform", "Unsellable"
        };

        var addedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywordsToCheck)
        {
            // Verificar si el keyword aparece en la descripción
            if (fullDesc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !addedKeywords.Contains(keyword))
            {
                if (Data.TooltipLegendStringDictionary.TryGetValue(keyword, out var symbol))
                {
                    if (symbol != null && !string.IsNullOrEmpty(symbol.Keyword))
                    {
                        string description = new LocalizableText(symbol.Keyword).GetLocalizedText();
                        if (!string.IsNullOrEmpty(description))
                        {
                            description = TextHelper.CleanText(description);
                            descriptions.Add($"{keyword}: {description}");
                            addedKeywords.Add(keyword);
                        }
                    }
                }
            }
        }

        return descriptions;
    }

    /// <summary>
    /// Obtiene todas las descripciones de propiedades (tags + keywords) de una carta.
    /// </summary>
    public static List<string> GetAllPropertyDescriptions(Card card)
    {
        var allDescriptions = new List<string>();

        // Primero los tags (tipos de item)
        var tagDescriptions = GetTagDescriptions(card);
        allDescriptions.AddRange(tagDescriptions);

        // Luego los keywords mencionados en los tooltips
        var keywordDescriptions = GetKeywordDescriptions(card);
        allDescriptions.AddRange(keywordDescriptions);

        return allDescriptions;
    }

    /// <summary>
    /// Obtiene las líneas de detalle para un encuentro PvP.
    /// Se usa con flechas arriba/abajo para leer info adicional del oponente.
    /// </summary>
    private static List<string> GetPvpEncounterDetailLines(Card card)
    {
        var lines = new List<string>();

        var pvpOpponent = Data.SimPvpOpponent;
        if (pvpOpponent == null)
        {
            lines.Add(GetCardName(card));
            lines.Add("PvP Encounter");
            return lines;
        }

        try
        {
            var type = pvpOpponent.GetType();

            // Nombre del jugador
            string name = GetPvpProperty<string>(pvpOpponent, type, "Name") ?? "Unknown";
            lines.Add($"Player: {name}");

            // Héroe - use Hero enum for correct name
            string hero = GetPvpOpponentHeroName(pvpOpponent) ?? GetCardName(card);
            lines.Add($"Hero: {hero}");

            // Rango y división
            string rank = GetPvpProperty<object>(pvpOpponent, type, "Rank")?.ToString();
            string division = GetPvpProperty<object>(pvpOpponent, type, "Division")?.ToString();
            if (!string.IsNullOrEmpty(rank))
            {
                if (!string.IsNullOrEmpty(division) && division != "0")
                {
                    lines.Add($"Rank: {rank} {division}");
                }
                else
                {
                    lines.Add($"Rank: {rank}");
                }
            }

            // Rating
            var rating = GetPvpProperty<int?>(pvpOpponent, type, "Rating");
            if (rating.HasValue && rating.Value > 0)
            {
                lines.Add($"Rating: {rating.Value}");
            }

            // Nivel
            var level = GetPvpProperty<int?>(pvpOpponent, type, "Level");
            if (level.HasValue && level.Value > 0)
            {
                lines.Add($"Level: {level.Value}");
            }

            // Victorias
            var victories = GetPvpProperty<int?>(pvpOpponent, type, "Victories");
            if (victories.HasValue)
            {
                lines.Add($"Wins: {victories.Value}");
            }

            // Prestigio
            var prestige = GetPvpProperty<int?>(pvpOpponent, type, "Prestige");
            if (prestige.HasValue && prestige.Value > 0)
            {
                lines.Add($"Prestige: {prestige.Value}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpEncounterDetailLines error: {ex.Message}");
            lines.Add(GetCardName(card));
            lines.Add("PvP Encounter");
        }

        return lines;
    }

    /// <summary>
    /// Helper para obtener propiedades del oponente PvP usando reflexión.
    /// </summary>
    private static T GetPvpProperty<T>(object pvpOpponent, Type type, string propertyName)
    {
        try
        {
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(pvpOpponent);
                if (value is T typedValue)
                {
                    return typedValue;
                }
                // Intentar conversión para tipos numéricos
                if (typeof(T) == typeof(int?) && value != null)
                {
                    return (T)(object)(int?)Convert.ToInt32(value);
                }
            }
        }
        catch { }
        return default;
    }

    /// <summary>
    /// Gets the hero name of the PvP opponent using the Hero enum property.
    /// Returns the hero name (e.g., "Pygmalien", "Vanessa") or null if unavailable.
    /// </summary>
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
                    // "Common" is the default/unknown value
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

    /// <summary>
    /// Obtiene el rango del oponente PvP.
    /// Devuelve formato: "Bronze 1" o "Gold 3" etc.
    /// </summary>
    public static string GetPvpOpponentRank(object pvpOpponent)
    {
        if (pvpOpponent == null) return null;

        try
        {
            var type = pvpOpponent.GetType();
            string rankName = null;
            string division = null;

            // Obtener Rank (Bronze, Silver, Gold, etc.)
            var rankProp = type.GetProperty("Rank", BindingFlags.Public | BindingFlags.Instance);
            if (rankProp != null)
            {
                var value = rankProp.GetValue(pvpOpponent);
                if (value != null)
                {
                    rankName = value.ToString();
                }
            }

            // Obtener Division (1, 2, 3, etc.)
            var divProp = type.GetProperty("Division", BindingFlags.Public | BindingFlags.Instance);
            if (divProp != null)
            {
                var value = divProp.GetValue(pvpOpponent);
                if (value != null)
                {
                    division = value.ToString();
                }
            }

            // Combinar rango y división
            if (!string.IsNullOrEmpty(rankName))
            {
                if (!string.IsNullOrEmpty(division) && division != "0")
                {
                    return $"{rankName} {division}";
                }
                return rankName;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetPvpOpponentRank error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the player's own ranked rank (e.g., "Silver 3", "Gold 1", "Legendary").
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
