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
        string name = string.IsNullOrEmpty(itemName) ? Loc.T("combat.item_fallback_name") : itemName;
        string fullName = isPlayerItem ? name : Loc.T("combat.enemy_item_name", name);

        // Handle critical hits prominently for damage
        if (isCrit && actionType == ActionType.PlayerDamage && amount > 0)
        {
            return Loc.T("combat.crit_hit_damage", fullName, amount);
        }

        string effectText = actionType switch
        {
            ActionType.PlayerDamage => amount > 0 ? Loc.T("combat.damage_amount", amount) : Loc.T("combat.damage"),
            ActionType.PlayerHeal => amount > 0 ? Loc.T("combat.heal_amount", amount) : Loc.T("combat.heal"),
            ActionType.PlayerShieldApply => amount > 0 ? Loc.T("combat.shield_amount", amount) : Loc.T("combat.shield"),
            ActionType.PlayerBurnApply => amount > 0 ? Loc.T("combat.burn_amount", amount) : Loc.T("combat.burn_applied"),
            ActionType.PlayerPoisonApply => amount > 0 ? Loc.T("combat.poison_amount", amount) : Loc.T("combat.poison_applied"),
            ActionType.PlayerRegenApply => amount > 0 ? Loc.T("combat.regen_amount", amount) : Loc.T("combat.regen_applied"),
            ActionType.PlayerGoldSteal => amount > 0 ? Loc.T("combat.stole_gold", amount) : Loc.T("combat.gold_stolen"),
            ActionType.PlayerMaxHealthIncrease => amount > 0 ? Loc.T("combat.max_health_increase", amount) : Loc.T("combat.max_health_increased"),
            ActionType.PlayerMaxHealthDecrease => amount > 0 ? Loc.T("combat.max_health_decrease", amount) : Loc.T("combat.max_health_decreased"),
            ActionType.CardSlow => Loc.T("combat.slowed"),
            ActionType.CardFreeze => isPlayerItem ? Loc.T("combat.freeze_applied") : null, // Enemy freeze uses special "Frozen!" alert
            ActionType.CardHaste => Loc.T("combat.hasted"),
            ActionType.CardCharge => Loc.T("combat.charged"),
            ActionType.CardReload => amount > 0 ? Loc.T("combat.reloaded_ammo", amount) : Loc.T("combat.reloaded"),
            ActionType.CardModifyAttribute => FormatModifyAttributeText(data, amount),
            ActionType.CardRepair => FormatRepairText(data),
            ActionType.CardDestroy => FormatDestroyText(data),
            ActionType.CardDisable => FormatDisableText(data),
            ActionType.CardTransform => Loc.T("combat.transformed"),
            ActionType.CardUpgrade => Loc.T("combat.upgraded"),
            ActionType.CardQuestComplete => Loc.T("combat.quest_complete"),
            ActionType.FlyingStart => Loc.T("combat.started_flying"),
            ActionType.FlyingStop => Loc.T("combat.stopped_flying"),
            ActionType.FlyingToggle => Loc.T("combat.toggled_flying"),
            _ => null
        };

        if (effectText == null)
        {
            // Special case: enemy freeze - announce "Frozen!" instead
            if (actionType == ActionType.CardFreeze && !isPlayerItem)
            {
                TolkWrapper.Speak(Loc.T("combat.frozen_alert"), interrupt: true);
                return null;
            }
            return null;
        }

        return Loc.T("combat.item_effect", fullName, effectText);
    }

    /// <summary>
    /// Formats destroy text showing which item was destroyed.
    /// </summary>
    public static string FormatDestroyText(CombatActionData data)
    {
        if (data == null) return Loc.T("combat.destroyed");

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return Loc.T("combat.destroyed_named", targetName);
        }

        return Loc.T("combat.destroyed");
    }

    /// <summary>
    /// Formats disable text showing which item was disabled.
    /// </summary>
    public static string FormatDisableText(CombatActionData data)
    {
        if (data == null) return Loc.T("combat.disabled");

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return Loc.T("combat.disabled_named", targetName);
        }

        return Loc.T("combat.disabled");
    }

    /// <summary>
    /// Formats repair text showing which item was repaired.
    /// </summary>
    public static string FormatRepairText(CombatActionData data)
    {
        if (data == null) return Loc.T("combat.repaired");

        var targetCard = data.TargetCard;
        if (targetCard != null)
        {
            string targetName = ItemReader.GetCardName(targetCard);
            if (!string.IsNullOrEmpty(targetName))
                return Loc.T("combat.repaired_named", targetName);
        }

        return Loc.T("combat.repaired");
    }

    /// <summary>
    /// Formats the modify attribute text with specific attribute info when available.
    /// </summary>
    public static string FormatModifyAttributeText(CombatActionData data, int fallbackAmount)
    {
        if (data == null)
        {
            return fallbackAmount != 0 ? Loc.T("combat.modified_by", fallbackAmount) : Loc.T("combat.modified");
        }

        var info = GetModifyAttributeInfo(data);

        // Build a descriptive message
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(info.AttrName))
        {
            if (info.Amount > 0)
            {
                parts.Add(Loc.T("combat.attr_increased_by", info.AttrName, Math.Abs(info.Amount)));
            }
            else if (info.Amount < 0)
            {
                parts.Add(Loc.T("combat.attr_decreased_by", info.AttrName, Math.Abs(info.Amount)));
            }
            else
            {
                parts.Add(Loc.T("combat.attr_changed", info.AttrName));
            }
        }
        else if (info.Amount != 0 || fallbackAmount != 0)
        {
            int finalAmount = info.Amount != 0 ? info.Amount : fallbackAmount;
            parts.Add(Loc.T("combat.modified_by", finalAmount));
        }
        else
        {
            parts.Add(Loc.T("combat.modified"));
        }

        if (!string.IsNullOrEmpty(info.TargetName))
        {
            parts.Add(Loc.T("combat.on_target", info.TargetName));
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
    /// Converts attribute type enum names to friendly names. Tries the game's own vocabulary
    /// first; only falls back to the hand-written switch when the game has no word for it.
    /// </summary>
    public static string GetFriendlyAttributeName(string attrType)
    {
        if (string.IsNullOrEmpty(attrType)) return null;

        string gameWord = GameVocabulary.Keyword(attrType);
        if (gameWord != attrType) return gameWord;

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
    /// Calculates the effect amount from event health diff or card attributes.
    /// For damage/heal/shield, prefers the actual HealthBefore/HealthAfter diff because
    /// card attributes only show base values and don't account for skills that amplify
    /// effects (e.g., Glass Cannon doubling weapon damage).
    /// </summary>
    public static int CalculateEffectAmount(CombatActionData data)
    {
        var card = data.SourceCard;
        if (card == null) return 0;

        // For damage, heal, shield, and other health-affecting effects:
        // Prefer HealthBefore/HealthAfter diff (actual effect including skill amplification)
        // over card attributes (base stats only).
        if (data.ActionType == ActionType.PlayerDamage || data.ActionType == ActionType.PlayerHeal ||
            data.ActionType == ActionType.PlayerShieldApply || data.ActionType == ActionType.PlayerGoldSteal ||
            data.ActionType == ActionType.PlayerMaxHealthIncrease || data.ActionType == ActionType.PlayerMaxHealthDecrease)
        {
            if (data.HealthBefore > 0 || data.HealthAfter > 0)
            {
                int healthDiff = (int)Math.Abs(data.HealthBefore - data.HealthAfter);
                if (healthDiff > 0) return healthDiff;
            }

            // Fallback to card attribute if health diff unavailable
            int attrAmount = data.ActionType switch
            {
                ActionType.PlayerDamage => card.GetAttributeValue(ECardAttributeType.DamageAmount) ?? 0,
                ActionType.PlayerHeal => card.GetAttributeValue(ECardAttributeType.HealAmount) ?? 0,
                ActionType.PlayerShieldApply => card.GetAttributeValue(ECardAttributeType.ShieldApplyAmount) ?? 0,
                _ => 0
            };
            return attrAmount;
        }

        // For non-health effects, use card attributes directly
        int amount = data.ActionType switch
        {
            ActionType.PlayerBurnApply => card.GetAttributeValue(ECardAttributeType.BurnApplyAmount) ?? 0,
            ActionType.PlayerPoisonApply => card.GetAttributeValue(ECardAttributeType.PoisonApplyAmount) ?? 0,
            ActionType.PlayerRegenApply => card.GetAttributeValue(ECardAttributeType.RegenApplyAmount) ?? 0,
            ActionType.CardReload => card.GetAttributeValue(ECardAttributeType.ReloadAmount) ?? 1,
            ActionType.CardHaste => card.GetAttributeValue(ECardAttributeType.HasteAmount) ?? 0,
            _ => 0
        };

        return amount;
    }
}
