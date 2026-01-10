using System.Collections.Generic;
using System.Text;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Localization;

// Para limpiar tags HTML de textos

namespace BazaarAccess.Gameplay;

/// <summary>
/// Lee información de items/cartas para accesibilidad.
/// </summary>
public static class ItemReader
{
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
    /// Obtiene el nombre localizado de una carta.
    /// </summary>
    public static string GetCardName(Card card)
    {
        if (card == null) return "Empty";

        var template = card.Template;
        if (template?.Localization?.Title != null)
        {
            string name = GetLocalizedText(template.Localization.Title);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        // Fallback al nombre interno
        return template?.InternalName ?? "Unknown";
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
    /// Obtiene un resumen corto del item para navegación rápida.
    /// Formato: "Nombre, Tier"
    /// </summary>
    public static string GetShortDescription(Card card)
    {
        if (card == null) return "Empty slot";

        string name = GetCardName(card);
        string tier = GetTierName(card);

        return $"{name}, {tier}";
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

        // Tamaño
        var template = card.Template;
        if (template != null)
        {
            sb.Append($", Size {(int)template.Size}");
        }

        // Cooldown (convertir de ms a segundos)
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
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
    /// Para PvP, usa el nombre del jugador si está disponible.
    /// </summary>
    public static string GetEncounterInfo(Card card)
    {
        if (card == null) return "Empty";

        string name;
        string type = GetEncounterTypeName(card.Type);

        // Para PvP, intentar obtener el nombre del jugador real
        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                // Mostrar nombre del jugador + héroe
                string heroName = GetCardName(card);
                name = $"{pvpOpponent.Name} ({heroName})";
            }
            else
            {
                name = GetCardName(card);
            }
        }
        else
        {
            name = GetCardName(card);
        }

        return $"{name}, {type}";
    }

    /// <summary>
    /// Obtiene información detallada de un encuentro.
    /// Para PvP, incluye nombre del jugador, héroe, nivel, victorias, etc.
    /// </summary>
    public static string GetEncounterDetailedInfo(Card card)
    {
        if (card == null) return "Empty";

        var sb = new StringBuilder();

        // Para PvP, mostrar información del jugador
        if (card.Type == ECardType.PvpEncounter)
        {
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null)
            {
                // Nombre del jugador
                if (!string.IsNullOrEmpty(pvpOpponent.Name))
                {
                    sb.Append(pvpOpponent.Name);
                }
                else
                {
                    sb.Append(GetCardName(card));
                }

                // Héroe
                sb.Append(", playing ");
                sb.Append(GetCardName(card));

                // Nivel
                sb.Append($", Level {pvpOpponent.Level}");

                // Victorias
                sb.Append($", {pvpOpponent.Victories} wins");

                // Prestigio
                sb.Append($", {pvpOpponent.Prestige} prestige");

                return sb.ToString();
            }
        }

        // Para otros encuentros, comportamiento normal
        sb.Append(GetCardName(card));
        sb.Append(", ");
        sb.Append(GetEncounterTypeName(card.Type));

        // Descripción del encuentro
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
    /// Obtiene la descripción de una carta.
    /// </summary>
    public static string GetDescription(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        if (template?.Localization?.Description != null)
        {
            return GetLocalizedText(template.Localization.Description);
        }

        return string.Empty;
    }

    /// <summary>
    /// Obtiene los tooltips de habilidades activas y pasivas de una carta.
    /// </summary>
    public static string GetAbilityTooltips(Card card)
    {
        if (card == null) return string.Empty;

        var template = card.Template;
        var tooltips = template?.Localization?.Tooltips;
        if (tooltips == null || tooltips.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        foreach (var tooltip in tooltips)
        {
            if (tooltip?.Content != null)
            {
                string text = GetLocalizedText(tooltip.Content);
                if (!string.IsNullOrEmpty(text))
                {
                    if (sb.Length > 0) sb.Append(". ");
                    sb.Append(text);
                }
            }
        }

        return sb.ToString();
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
    /// </summary>
    public static List<string> GetDetailLines(Card card)
    {
        var lines = new List<string>();
        if (card == null) return lines;

        // Nombre
        lines.Add($"Name: {GetCardName(card)}");

        // Tier
        lines.Add($"Tier: {GetTierName(card)}");

        // Tamaño
        var template = card.Template;
        if (template != null)
        {
            lines.Add($"Size: {(int)template.Size}");
        }

        // Precio de compra
        int buyPrice = GetBuyPrice(card);
        if (buyPrice > 0)
        {
            lines.Add($"Buy price: {buyPrice} gold");
        }

        // Precio de venta
        int sellPrice = GetSellPrice(card);
        if (sellPrice > 0)
        {
            lines.Add($"Sell price: {sellPrice} gold");
        }

        // Cooldown
        var cooldown = card.GetAttributeValue(ECardAttributeType.Cooldown);
        if (cooldown.HasValue && cooldown.Value > 0)
        {
            float seconds = cooldown.Value / 1000f;
            lines.Add($"Cooldown: {seconds:F1} seconds");
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

        // Descripción básica
        string desc = GetDescription(card);
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add($"Description: {desc}");
        }

        // Tooltips de habilidades (Active/Passive abilities)
        string abilities = GetAbilityTooltips(card);
        if (!string.IsNullOrEmpty(abilities))
        {
            lines.Add($"Ability: {abilities}");
        }

        // Flavor text
        string flavor = GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavor))
        {
            lines.Add($"Lore: {flavor}");
        }

        return lines;
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
}
