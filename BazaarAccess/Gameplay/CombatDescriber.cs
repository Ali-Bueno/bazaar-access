using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Combat;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Narrates combat events via screen reader. Supports two modes:
///
/// BATCHED MODE (default):
/// - Accumulates effects into "waves" based on timing
/// - After 1.5s of inactivity, announces a summary: "You: 50 damage (Sword). Enemy: 30 damage"
/// - Includes periodic health announcements every 5 seconds
/// - Best for: Getting the overall flow without constant interruption
/// - Developer: Modify the BATCHED MODE region to change wave behavior
///
/// INDIVIDUAL MODE:
/// - Announces each card trigger immediately as it happens
/// - Format: "[ItemName]: [amount] [effect]" (e.g., "Sword: 10 damage")
/// - Enemy items prefixed: "Enemy Dagger: 5 damage"
/// - Best for: Detailed real-time combat feedback
/// - Developer: Modify the INDIVIDUAL MODE region to change per-card behavior
///
/// Toggle between modes with M key during combat (or via config).
/// Both modes share: health threshold warnings (low/critical), combat totals, H key summary.
///
/// Sub-components:
/// - HealthTracker: Health monitoring, periodic announcements, threshold warnings
/// - EffectFormatter: Combat effect text formatting and relevance filtering
/// </summary>
public static class CombatDescriber
{
    // Configuration
    private const float WaveTimeout = 1.5f;         // Seconds of inactivity to close a wave

    // State
    private static bool _active;
    private static string _enemyName;
    private static Coroutine _waveCoroutine;

    // Wave data for accumulating effects
    private static WaveData _playerWave = new WaveData();
    private static WaveData _enemyWave = new WaveData();

    // Combat totals for H key summary
    private static int _totalPlayerDamageDealt;
    private static int _totalPlayerDamageTaken;

    /// <summary>
    /// Whether to use batched mode (wave summaries + auto health) or individual mode (per-card announcements).
    /// </summary>
    public static bool UseBatchedMode => Plugin.UseBatchedCombatMode?.Value ?? true;

    /// <summary>
    /// Toggles between batched and individual combat announcement modes.
    /// </summary>
    public static void ToggleMode()
    {
        if (Plugin.UseBatchedCombatMode == null) return;

        Plugin.UseBatchedCombatMode.Value = !Plugin.UseBatchedCombatMode.Value;
        string modeName = UseBatchedMode ? Loc.T("combat.mode.batched") : Loc.T("combat.mode.individual");
        TolkWrapper.Speak(modeName, interrupt: true);

        // Handle mid-combat switch
        if (_active)
        {
            if (UseBatchedMode)
            {
                // Start health announcements
                HealthTracker.StartPeriodicAnnouncements();
            }
            else
            {
                // Stop health announcements and flush pending waves
                HealthTracker.StopCoroutine();
                CoroutineHelper.StopSafe(ref _waveCoroutine);
                AnnounceWave();  // Flush any pending wave data
            }
        }

        Plugin.Logger.LogInfo($"Combat mode toggled to: {modeName}");
    }

    /// <summary>
    /// Data accumulated during a wave of combat activity.
    /// </summary>
    private class WaveData
    {
        public int TotalDamage;
        public int TotalHeal;
        public int TotalShield;
        public Dictionary<string, int> DamageByItem = new Dictionary<string, int>();
        public HashSet<string> StatusEffects = new HashSet<string>();
        public bool HadCrit;

        // Track reloads and modifications
        public Dictionary<string, int> ReloadsByItem = new Dictionary<string, int>();
        public int TotalBuffs;
        public int TotalDebuffs;

        // Track repairs
        public int TotalRepairs;
        public List<string> RepairedItems = new List<string>();

        public void Clear()
        {
            TotalDamage = 0;
            TotalHeal = 0;
            TotalShield = 0;
            DamageByItem.Clear();
            StatusEffects.Clear();
            HadCrit = false;
            ReloadsByItem.Clear();
            TotalBuffs = 0;
            TotalDebuffs = 0;
            TotalRepairs = 0;
            RepairedItems.Clear();
        }

        public bool HasActivity => TotalDamage > 0 || TotalHeal > 0 || TotalShield > 0 ||
                                   StatusEffects.Count > 0 || ReloadsByItem.Count > 0 ||
                                   TotalBuffs > 0 || TotalDebuffs > 0 || TotalRepairs > 0;

        public string GetTopItem()
        {
            if (DamageByItem.Count == 0) return null;
            return DamageByItem.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

    // Backward-compatible inner class alias
    /// <summary>
    /// Starts combat narration.
    /// </summary>
    public static void StartDescribing()
    {
        if (_active)
        {
            Plugin.Logger.LogInfo("CombatDescriber: Already active, restarting...");
            StopDescribing();
        }

        _active = true;

        // Initialize wave state
        _playerWave.Clear();
        _enemyWave.Clear();

        // Reset totals
        _totalPlayerDamageDealt = 0;
        _totalPlayerDamageTaken = 0;

        // Get enemy name
        _enemyName = GetEnemyName();

        // Initialize health tracking
        HealthTracker.Start(_enemyName);

        // Start periodic health announcements only in batched mode
        if (UseBatchedMode)
        {
            HealthTracker.StartPeriodicAnnouncements();
        }
        // Health threshold warnings (low/critical) are always active

        Plugin.Logger.LogInfo($"CombatDescriber: Started, enemy = {_enemyName}, mode = {(UseBatchedMode ? "batched" : "individual")}");
    }

    /// <summary>
    /// Stops combat narration.
    /// </summary>
    public static void StopDescribing()
    {
        if (!_active) return;
        _active = false;

        // Stop coroutines
        HealthTracker.Stop();
        CoroutineHelper.StopSafe(ref _waveCoroutine);

        // Clear state
        _enemyName = null;
        _playerWave.Clear();
        _enemyWave.Clear();

        Plugin.Logger.LogInfo("CombatDescriber: Stopped");
    }

    /// <summary>
    /// Gets the current combat summary for the H key.
    /// </summary>
    public static string GetCombatSummary()
    {
        if (!_active) return Loc.T("combat.summary.not_in_combat");

        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            int reportedPlayerHealth = player?.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
            int playerShield = player?.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;
            // Game may include shield in health, so subtract to get actual health
            int playerHealth = reportedPlayerHealth - playerShield;
            if (playerHealth < 0) playerHealth = reportedPlayerHealth;

            int reportedEnemyHealth = 0;
            int enemyShield = 0;
            opponent?.Attributes.TryGetValue(EPlayerAttributeType.Health, out reportedEnemyHealth);
            opponent?.Attributes.TryGetValue(EPlayerAttributeType.Shield, out enemyShield);
            int enemyHealth = reportedEnemyHealth - enemyShield;
            if (enemyHealth < 0) enemyHealth = reportedEnemyHealth;

            var parts = new List<string>();

            // Damage totals
            parts.Add(Loc.T("combat.summary.dealt_took", _totalPlayerDamageDealt, _totalPlayerDamageTaken));

            // Health comparison
            string playerHealthStr = playerShield > 0 ? $"{playerHealth}+{playerShield}" : $"{playerHealth}";
            string enemyHealthStr = enemyShield > 0 ? $"{enemyHealth}+{enemyShield}" : $"{enemyHealth}";
            parts.Add(Loc.T("combat.summary.health_vs", playerHealthStr, enemyHealthStr));

            return string.Join(". ", parts);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"GetCombatSummary error: {ex.Message}");
            return Loc.T("combat.summary.unavailable");
        }
    }

    /// <summary>
    /// Gets player health as a string (for 1 key).
    /// Delegates to HealthTracker.
    /// </summary>
    public static string GetPlayerHealth() => HealthTracker.GetPlayerHealth();

    /// <summary>
    /// Gets enemy health as a string (for 2 key).
    /// Delegates to HealthTracker.
    /// </summary>
    public static string GetEnemyHealth() => HealthTracker.GetEnemyHealth();

    /// <summary>
    /// Gets total damage dealt as a number string (for 3 key).
    /// </summary>
    public static string GetDamageDealt()
    {
        return _totalPlayerDamageDealt.ToString();
    }

    /// <summary>
    /// Gets total damage taken as a number string (for 4 key).
    /// </summary>
    public static string GetDamageTaken()
    {
        return _totalPlayerDamageTaken.ToString();
    }

    /// <summary>
    /// Gets the enemy name. Always returns "Enemy" for simplicity during combat.
    /// </summary>
    private static string GetEnemyName()
    {
        return Loc.T("combat.enemy_name");
    }

    /// <summary>
    /// Handler for combat effect events. Dispatches to the appropriate mode handler.
    /// </summary>
    internal static void OnEffectTriggered(EffectTriggeredEvent evt)
    {
        if (!_active) return;

        // Verify we're in combat
        var currentState = Data.CurrentState?.StateName;
        if (currentState != ERunState.Combat && currentState != ERunState.PVPCombat)
        {
            return;
        }

        try
        {
            var data = evt?.Data;
            if (data == null) return;

            var sourceCard = data.SourceCard;
            if (sourceCard == null) return;

            // Determine owner
            bool isPlayerItem = IsPlayerCard(sourceCard);
            string itemName = ItemReader.GetCardName(sourceCard);

            if (!EffectFormatter.IsRelevantAction(data.ActionType)) return;

            // Calculate details for relevant actions
            int amount = EffectFormatter.CalculateEffectAmount(data);
            bool isCrit = data.IsCrit;

            // Dispatch to the appropriate mode handler
            if (UseBatchedMode)
                HandleBatchedEffect(itemName, isPlayerItem, data, amount, isCrit);
            else
                HandleIndividualEffect(itemName, isPlayerItem, data, amount, isCrit);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnEffectTriggered error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handler for health changes - delegates to HealthTracker.
    /// </summary>
    internal static void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        HealthTracker.OnPlayerHealthChanged(evt);
    }

    #region ===== BATCHED MODE =====
    // All batched mode specific code here.
    // Developer can modify this section without affecting individual mode.
    // Batched mode accumulates effects into waves and announces summaries after 1.5s of inactivity.

    /// <summary>
    /// Handles an effect in batched mode by accumulating it into wave data.
    /// Modify this method to change how effects are grouped and summarized.
    /// </summary>
    private static void HandleBatchedEffect(string itemName, bool isPlayerItem, CombatActionData data, int amount, bool isCrit)
    {
        WaveData wave = isPlayerItem ? _playerWave : _enemyWave;

        switch (data.ActionType)
        {
            case ActionType.PlayerDamage:
                wave.TotalDamage += amount;
                if (!string.IsNullOrEmpty(itemName))
                {
                    if (wave.DamageByItem.ContainsKey(itemName))
                        wave.DamageByItem[itemName] += amount;
                    else
                        wave.DamageByItem[itemName] = amount;
                }
                if (isPlayerItem) _totalPlayerDamageDealt += amount;
                else _totalPlayerDamageTaken += amount;
                break;

            case ActionType.PlayerHeal:
                wave.TotalHeal += amount;
                break;

            case ActionType.PlayerShieldApply:
                wave.TotalShield += amount;
                break;

            case ActionType.PlayerBurnApply:
                wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.BurnApplyAmount) ?? "burn");
                break;

            case ActionType.PlayerPoisonApply:
                wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.PoisonApplyAmount) ?? "poison");
                break;

            case ActionType.CardSlow:
                wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.SlowAmount) ?? "slow");
                break;

            case ActionType.CardFreeze:
                if (!isPlayerItem)
                {
                    // Enemy freeze - special "Frozen!" alert
                    TolkWrapper.Speak(Loc.T("combat.frozen_alert"), interrupt: true);
                }
                else
                {
                    wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.FreezeAmount) ?? "freeze");
                }
                break;

            case ActionType.CardReload:
                // Accumulate reloads into wave
                if (!string.IsNullOrEmpty(itemName))
                {
                    if (wave.ReloadsByItem.ContainsKey(itemName))
                        wave.ReloadsByItem[itemName] += amount > 0 ? amount : 1;
                    else
                        wave.ReloadsByItem[itemName] = amount > 0 ? amount : 1;
                }
                break;

            case ActionType.CardModifyAttribute:
                // Accumulate buffs/debuffs into wave (don't announce each one)
                if (amount > 0)
                    wave.TotalBuffs++;
                else if (amount < 0)
                    wave.TotalDebuffs++;
                else
                    wave.TotalBuffs++; // Count unknown modifications as buffs
                break;

            case ActionType.CardRepair:
                wave.TotalRepairs++;
                var repairedCard = data.TargetCard;
                if (repairedCard != null)
                {
                    string repairedName = ItemReader.GetCardName(repairedCard);
                    if (!string.IsNullOrEmpty(repairedName))
                        wave.RepairedItems.Add(repairedName);
                }
                break;

            case ActionType.PlayerRegenApply:
                wave.TotalHeal += amount;
                break;

            case ActionType.PlayerGoldSteal:
                wave.StatusEffects.Add(amount > 0 ? Loc.T("combat.stole_gold", amount) : Loc.T("combat.gold_stolen"));
                break;

            case ActionType.PlayerMaxHealthIncrease:
                wave.TotalBuffs++;
                break;

            case ActionType.PlayerMaxHealthDecrease:
                wave.TotalDebuffs++;
                break;

            case ActionType.CardHaste:
                wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.HasteAmount) ?? "haste");
                break;

            case ActionType.CardCharge:
                wave.StatusEffects.Add(GameVocabulary.Attribute(ECardAttributeType.ChargeAmount) ?? "charge");
                break;

            case ActionType.CardDestroy:
                var destroyedCard = data.TargetCard;
                if (destroyedCard != null)
                {
                    string destroyedName = ItemReader.GetCardName(destroyedCard);
                    if (!string.IsNullOrEmpty(destroyedName))
                        wave.StatusEffects.Add(Loc.T("combat.destroyed_named", destroyedName));
                    else
                        wave.StatusEffects.Add(Loc.T("combat.destroyed"));
                }
                else
                {
                    wave.StatusEffects.Add(Loc.T("combat.destroyed"));
                }
                break;

            case ActionType.CardDisable:
                var disabledCard = data.TargetCard;
                if (disabledCard != null)
                {
                    string disabledName = ItemReader.GetCardName(disabledCard);
                    if (!string.IsNullOrEmpty(disabledName))
                        wave.StatusEffects.Add(Loc.T("combat.disabled_named", disabledName));
                    else
                        wave.StatusEffects.Add(Loc.T("combat.disabled"));
                }
                else
                {
                    wave.StatusEffects.Add(Loc.T("combat.disabled"));
                }
                break;

            case ActionType.CardTransform:
                wave.StatusEffects.Add(Loc.T("combat.transformed"));
                break;

            case ActionType.CardUpgrade:
                wave.TotalBuffs++;
                break;

            case ActionType.CardQuestComplete:
                wave.StatusEffects.Add(Loc.T("combat.quest_complete"));
                break;

            case ActionType.FlyingStart:
                wave.StatusEffects.Add(Loc.T("combat.flying"));
                break;

            case ActionType.FlyingStop:
                wave.StatusEffects.Add(Loc.T("combat.landed"));
                break;

            case ActionType.FlyingToggle:
                wave.StatusEffects.Add(Loc.T("combat.flying_toggled"));
                break;
        }

        if (isCrit) wave.HadCrit = true;
        RestartWaveTimer();
    }

    #endregion

    #region ===== INDIVIDUAL MODE =====
    // All individual mode specific code here.
    // Developer can modify this section without affecting batched mode.
    // Individual mode announces each card trigger immediately as it happens.

    /// <summary>
    /// Handles an effect in individual mode by announcing it immediately.
    /// Modify this method to change how individual effects are announced.
    /// </summary>
    private static void HandleIndividualEffect(string itemName, bool isPlayerItem, CombatActionData data, int amount, bool isCrit)
    {
        // Track damage totals
        if (data.ActionType == ActionType.PlayerDamage)
        {
            if (isPlayerItem) _totalPlayerDamageDealt += amount;
            else _totalPlayerDamageTaken += amount;
        }

        string announcement = EffectFormatter.FormatEffectAnnouncement(itemName, isPlayerItem, data.ActionType, amount, isCrit, data);
        if (!string.IsNullOrEmpty(announcement))
        {
            TolkWrapper.Speak(announcement, interrupt: false);
        }
    }

    private static bool IsPlayerCard(BazaarGameClient.Domain.Models.Cards.Card card)
    {
        if (card == null) return false;

        try
        {
            var player = Data.Run?.Player;
            if (player == null) return true;
            if (card.Owner == null) return true;
            return card.Owner == player;
        }
        catch
        {
            return true;
        }
    }

    #endregion

    #region ===== BATCHED MODE: WAVE METHODS =====
    // Wave accumulation and announcement methods for batched mode.
    // Modify these to change how waves are timed and summarized.

    /// <summary>
    /// Restarts the wave timeout timer.
    /// </summary>
    private static void RestartWaveTimer()
    {
        CoroutineHelper.StartSafe(ref _waveCoroutine, WaveTimeoutCoroutine());
    }

    /// <summary>
    /// Waits for wave timeout then announces the wave summary.
    /// </summary>
    private static IEnumerator WaveTimeoutCoroutine()
    {
        yield return new WaitForSeconds(WaveTimeout);
        AnnounceWave();
        _waveCoroutine = null;
    }

    /// <summary>
    /// Announces the current wave summary and clears wave data.
    /// </summary>
    private static void AnnounceWave()
    {
        if (!_playerWave.HasActivity && !_enemyWave.HasActivity)
            return;

        var parts = new List<string>();

        // Player side
        if (_playerWave.HasActivity)
        {
            string playerPart = FormatWaveSide(Loc.T("combat.you_label"), _playerWave);
            if (!string.IsNullOrEmpty(playerPart))
                parts.Add(playerPart);
        }

        // Enemy side
        if (_enemyWave.HasActivity)
        {
            string enemyPart = FormatWaveSide(_enemyName, _enemyWave);
            if (!string.IsNullOrEmpty(enemyPart))
                parts.Add(enemyPart);
        }

        if (parts.Count > 0)
        {
            TolkWrapper.Speak(string.Join(". ", parts), interrupt: false);
        }

        // Clear wave data
        _playerWave.Clear();
        _enemyWave.Clear();
    }

    /// <summary>
    /// Formats one side of the wave summary.
    /// Critical hits are announced prominently at the start.
    /// </summary>
    private static string FormatWaveSide(string owner, WaveData wave)
    {
        var elements = new List<string>();

        // Main effect (damage or heal or shield)
        if (wave.TotalDamage > 0)
        {
            string topItem = wave.GetTopItem();
            string damageText;
            if (wave.HadCrit)
            {
                // Prominent critical hit announcement
                damageText = !string.IsNullOrEmpty(topItem)
                    ? Loc.T("combat.wave.crit_damage_item", wave.TotalDamage, topItem)
                    : Loc.T("combat.wave.crit_damage", wave.TotalDamage);
            }
            else
            {
                damageText = !string.IsNullOrEmpty(topItem)
                    ? Loc.T("combat.wave.damage_item", wave.TotalDamage, topItem)
                    : Loc.T("combat.damage_amount", wave.TotalDamage);
            }
            elements.Add(damageText);
        }

        if (wave.TotalHeal > 0)
        {
            elements.Add(Loc.T("combat.heal_amount", wave.TotalHeal));
        }

        if (wave.TotalShield > 0)
        {
            elements.Add(Loc.T("combat.shield_amount", wave.TotalShield));
        }

        // Status effects
        if (wave.StatusEffects.Count > 0)
        {
            elements.AddRange(wave.StatusEffects);
        }

        // Reloads - summarize briefly
        if (wave.ReloadsByItem.Count > 0)
        {
            int totalReloads = wave.ReloadsByItem.Values.Sum();
            if (wave.ReloadsByItem.Count == 1)
            {
                var item = wave.ReloadsByItem.First();
                elements.Add(Loc.T("combat.wave.reload_item", item.Key, item.Value));
            }
            else
            {
                elements.Add(Loc.Plural("combat.wave.reloads", totalReloads, totalReloads));
            }
        }

        // Buffs/debuffs - summarize briefly
        if (wave.TotalBuffs > 0 || wave.TotalDebuffs > 0)
        {
            if (wave.TotalBuffs > 0 && wave.TotalDebuffs > 0)
            {
                elements.Add($"{Loc.Plural("combat.wave.buffs", wave.TotalBuffs, wave.TotalBuffs)}, {Loc.Plural("combat.wave.debuffs", wave.TotalDebuffs, wave.TotalDebuffs)}");
            }
            else if (wave.TotalBuffs > 0)
            {
                elements.Add(Loc.Plural("combat.wave.buffs", wave.TotalBuffs, wave.TotalBuffs));
            }
            else
            {
                elements.Add(Loc.Plural("combat.wave.debuffs", wave.TotalDebuffs, wave.TotalDebuffs));
            }
        }

        // Repairs
        if (wave.TotalRepairs > 0)
        {
            if (wave.RepairedItems.Count == 1)
            {
                elements.Add(Loc.T("combat.repaired_named", wave.RepairedItems[0]));
            }
            else
            {
                elements.Add(Loc.Plural("combat.wave.repairs", wave.TotalRepairs, wave.TotalRepairs));
            }
        }

        if (elements.Count == 0)
            return null;

        string result = Loc.T("combat.wave_side", owner, string.Join(", ", elements));

        return result;
    }

    #endregion
}
