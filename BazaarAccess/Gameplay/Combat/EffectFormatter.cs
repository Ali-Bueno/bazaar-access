using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;

namespace BazaarAccess.Gameplay.Combat;

/// <summary>
/// Formats combat effect announcements for screen reader output.
/// Handles both individual per-card announcements and utility formatting for batched mode.
/// </summary>
public static class EffectFormatter
{
    /// <summary>
    /// Holds information about a modified attribute event.
    /// </summary>
    public struct ModifyAttributeInfo
    {
        public string TargetName;
        public string AttrName;
        public int Amount;
    }

    /// <summary>
    /// Formats an immediate effect announcement for individual mode.
    /// Player: "Sword: 10 damage" | Enemy: "Enemy Sword: 10 damage"
    /// Critical hits: "Critical hit! Sword: 180 damage"
    /// </summary>
    public static string FormatEffectAnnouncement(string itemName, bool isPlayerItem, ActionType actionType, int amount, bool isCrit, CombatActionData data = null)
    {
        // Prefix with "Enemy" for opponent items
        string prefix = isPlayerItem ? "" : "Enemy ";
        string name = string.IsNullOrEmpty(itemName) ? "Item" : itemName;

        // Handle critical hits prominently for damage
        if (isCrit && actionType == ActionType.PlayerDamage && amount > 0)
        {
            return $"Critical hit! {prefix}{name}: {amount} damage";
        }

        string effectText = actionType switch
        {
            ActionType.PlayerDamage => amount > 0 ? $"{amount} damage" : "damage",
            ActionType.PlayerHeal => amount > 0 ? $"{amount} heal" : "heal",
            ActionType.PlayerShieldApply => amount > 0 ? $"{amount} shield" : "shield",
            ActionType.PlayerBurnApply => amount > 0 ? $"{amount} burn" : "burn applied",
            ActionType.PlayerPoisonApply => amount > 0 ? $"{amount} poison" : "poison applied",
            ActionType.PlayerRegenApply => amount > 0 ? $"{amount} regen" : "regen applied",
            ActionType.PlayerGoldSteal => amount > 0 ? $"stole {amount} gold" : "gold stolen",
            ActionType.PlayerMaxHealthIncrease => amount > 0 ? $"max health +{amount}" : "max health increased",
            ActionType.PlayerMaxHealthDecrease => amount > 0 ? $"max health -{amount}" : "max health decreased",
            ActionType.CardSlow => "slowed",
            ActionType.CardFreeze => isPlayerItem ? "freeze applied" : null, // Enemy freeze uses special "Frozen!" alert
            ActionType.CardHaste => "hasted",
            ActionType.CardCharge => "charged",
            ActionType.CardReload => amount > 0 ? $"reloaded {amount} ammo" : "reloaded",
            ActionType.CardModifyAttribute => FormatModifyAttributeText(data, amount),
            ActionType.CardRepair => FormatRepairText(data),
            ActionType.CardDestroy => FormatDestroyText(data),
            ActionType.CardDisable => FormatDisableText(data),
            ActionType.CardTransform => "transformed",
            ActionType.CardUpgrade => "upgraded",
            ActionType.CardQuestComplete => "quest complete",
            ActionType.FlyingStart => "started flying",
            ActionType.FlyingStop => "stopped flying",
            ActionType.FlyingToggle => "toggled flying",
            _ => null
        };

        if (effectText == null)
        {
            // Special case: enemy freeze - announce "Frozen!" instead
            if (actionType == ActionType.CardFreeze && !isPlayerItem)
            {
                TolkWrapper.Speak("Frozen!", interrupt: true);
                return null;
            }
            return null;
        }

        return $"{prefix}{name}: {effectText}";
    }

    /// <summary>
    /// Formats destroy text showing which item was destroyed.
    /// </summary>
    public static string FormatDestroyText(CombatActionData data)
    {
        if (data == null) return "destroyed";

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return $"destroyed {targetName}";
        }

        return "destroyed";
    }

    /// <summary>
    /// Formats disable text showing which item was disabled.
    /// </summary>
    public static string FormatDisableText(CombatActionData data)
    {
        if (data == null) return "disabled";

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return $"disabled {targetName}";
        }

        return "disabled";
    }

    /// <summary>
    /// Formats repair text showing which item was repaired.
    /// </summary>
    public static string FormatRepairText(CombatActionData data)
    {
        if (data == null) return "repaired";

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return $"repaired {targetName}";
        }

        return "repaired";
    }

    /// <summary>
    /// Formats the modify attribute text with specific attribute info when available.
    /// </summary>
    public static string FormatModifyAttributeText(CombatActionData data, int fallbackAmount)
    {
        if (data == null)
        {
            return fallbackAmount != 0 ? $"modified by {fallbackAmount}" : "modified";
        }

        var info = GetModifyAttributeInfo(data);

        // Build a descriptive message
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(info.AttrName))
        {
            string changeDir = info.Amount > 0 ? "increased" : (info.Amount < 0 ? "decreased" : "changed");
            if (info.Amount != 0)
            {
                parts.Add($"{info.AttrName} {changeDir} by {Math.Abs(info.Amount)}");
            }
            else
            {
                parts.Add($"{info.AttrName} {changeDir}");
            }
        }
        else if (info.Amount != 0 || fallbackAmount != 0)
        {
            int finalAmount = info.Amount != 0 ? info.Amount : fallbackAmount;
            parts.Add($"modified by {finalAmount}");
        }
        else
        {
            parts.Add("modified");
        }

        if (!string.IsNullOrEmpty(info.TargetName))
        {
            parts.Add($"on {info.TargetName}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets modified attribute information from CardModifyAttribute events.
    /// </summary>
    public static ModifyAttributeInfo GetModifyAttributeInfo(CombatActionData data)
    {
        var result = new ModifyAttributeInfo();

        try
        {
            // Try to get TargetCard property via reflection
            var targetCardProp = data.GetType().GetProperty("TargetCard");
            Card targetCard = null;
            if (targetCardProp != null)
            {
                targetCard = targetCardProp.GetValue(data) as Card;
            }

            result.TargetName = targetCard != null ? ItemReader.GetCardName(targetCard) : null;

            // Try to get the attribute type from the event
            var attrTypeProp = data.GetType().GetProperty("AttributeType");
            if (attrTypeProp != null)
            {
                var attrValue = attrTypeProp.GetValue(data);
                if (attrValue != null)
                {
                    result.AttrName = GetFriendlyAttributeName(attrValue.ToString());
                }
            }

            // Try to get amount from event data
            var amountProp = data.GetType().GetProperty("Amount");
            if (amountProp != null)
            {
                var amountValue = amountProp.GetValue(data);
                if (amountValue != null)
                {
                    int.TryParse(amountValue.ToString(), out result.Amount);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"GetModifyAttributeInfo error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Converts attribute type enum names to friendly names.
    /// </summary>
    public static string GetFriendlyAttributeName(string attrType)
    {
        if (string.IsNullOrEmpty(attrType)) return null;

        return attrType switch
        {
            "DamageAmount" => "damage",
            "HealAmount" => "heal",
            "ShieldApplyAmount" => "shield",
            "CritChance" => "crit chance",
            "Cooldown" => "cooldown",
            "CooldownMax" => "cooldown",
            "Ammo" => "ammo",
            "AmmoMax" => "max ammo",
            "HasteAmount" => "haste",
            "SlowAmount" => "slow",
            "FreezeAmount" => "freeze",
            "BurnApplyAmount" => "burn",
            "PoisonApplyAmount" => "poison",
            "Lifesteal" => "lifesteal",
            "Multicast" => "multicast",
            "RegenApplyAmount" => "regen",
            "Counter" => "counter",
            "RepairTargets" => "repair targets",
            _ => attrType.ToLower().Replace("amount", "").Trim()
        };
    }

    /// <summary>
    /// Checks if an action type is relevant for narration.
    /// </summary>
    public static bool IsRelevantAction(ActionType type)
    {
        return type switch
        {
            ActionType.PlayerDamage => true,
            ActionType.PlayerHeal => true,
            ActionType.PlayerShieldApply => true,
            ActionType.PlayerBurnApply => true,
            ActionType.PlayerPoisonApply => true,
            ActionType.PlayerRegenApply => true,
            ActionType.PlayerGoldSteal => true,
            ActionType.PlayerMaxHealthIncrease => true,
            ActionType.PlayerMaxHealthDecrease => true,
            ActionType.CardSlow => true,
            ActionType.CardFreeze => true,
            ActionType.CardHaste => true,
            ActionType.CardCharge => true,
            ActionType.CardReload => true,
            ActionType.CardModifyAttribute => true,
            ActionType.CardRepair => true,
            ActionType.CardDestroy => true,
            ActionType.CardDisable => true,
            ActionType.CardTransform => true,
            ActionType.CardUpgrade => true,
            ActionType.CardQuestComplete => true,
            ActionType.FlyingStart => true,
            ActionType.FlyingStop => true,
            ActionType.FlyingToggle => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates the effect amount from card attributes or event data.
    /// </summary>
    public static int CalculateEffectAmount(CombatActionData data)
    {
        var card = data.SourceCard;
        if (card == null) return 0;

        int amount = data.ActionType switch
        {
            ActionType.PlayerDamage => card.GetAttributeValue(ECardAttributeType.DamageAmount) ?? 0,
            ActionType.PlayerHeal => card.GetAttributeValue(ECardAttributeType.HealAmount) ?? 0,
            ActionType.PlayerShieldApply => card.GetAttributeValue(ECardAttributeType.ShieldApplyAmount) ?? 0,
            ActionType.PlayerBurnApply => card.GetAttributeValue(ECardAttributeType.BurnApplyAmount) ?? 0,
            ActionType.PlayerPoisonApply => card.GetAttributeValue(ECardAttributeType.PoisonApplyAmount) ?? 0,
            ActionType.PlayerRegenApply => card.GetAttributeValue(ECardAttributeType.RegenApplyAmount) ?? 0,
            ActionType.CardReload => card.GetAttributeValue(ECardAttributeType.ReloadAmount) ?? 1,
            ActionType.CardHaste => card.GetAttributeValue(ECardAttributeType.HasteAmount) ?? 0,
            _ => 0
        };

        // Fallback to health diff for health-related effects if attribute not found
        if (amount == 0 && (data.ActionType == ActionType.PlayerDamage || data.ActionType == ActionType.PlayerHeal ||
            data.ActionType == ActionType.PlayerGoldSteal || data.ActionType == ActionType.PlayerMaxHealthIncrease ||
            data.ActionType == ActionType.PlayerMaxHealthDecrease))
        {
            if (data.HealthBefore > 0 || data.HealthAfter > 0)
            {
                amount = (int)Math.Abs(data.HealthBefore - data.HealthAfter);
            }
        }

        return amount;
    }
}
