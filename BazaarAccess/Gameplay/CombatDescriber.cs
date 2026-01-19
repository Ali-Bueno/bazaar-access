using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
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
/// </summary>
public static class CombatDescriber
{
    // Configuration
    private const float WaveTimeout = 1.5f;         // Seconds of inactivity to close a wave
    private const float HealthInterval = 5f;        // Seconds between health announcements
    private const float LowHealthThreshold = 0.25f; // 25% = low health warning
    private const float CritHealthThreshold = 0.10f; // 10% = critical health warning

    // State
    private static bool _active;
    private static float _lastHealthTime;
    private static int _lastPlayerHealth;
    private static int _lastPlayerMaxHealth;
    private static int _lastEnemyHealth;
    private static int _lastEnemyMaxHealth;
    private static string _enemyName;
    private static Coroutine _healthCoroutine;
    private static Coroutine _waveCoroutine;

    // Health threshold tracking (to avoid repeating warnings)
    private static bool _announcedPlayerLow;
    private static bool _announcedPlayerCrit;
    private static bool _announcedEnemyLow;
    private static bool _announcedEnemyCrit;

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
        string modeName = UseBatchedMode ? "Combat viewer set to batched action mode" : "Combat viewer set to Individual action mode";
        TolkWrapper.Speak(modeName, interrupt: true);

        // Handle mid-combat switch
        if (_active)
        {
            if (UseBatchedMode)
            {
                // Start health announcements
                if (_healthCoroutine == null && Plugin.Instance != null)
                    _healthCoroutine = Plugin.Instance.StartCoroutine(HealthAnnouncementLoop());
            }
            else
            {
                // Stop health announcements and flush pending waves
                if (_healthCoroutine != null && Plugin.Instance != null)
                {
                    Plugin.Instance.StopCoroutine(_healthCoroutine);
                    _healthCoroutine = null;
                }
                if (_waveCoroutine != null && Plugin.Instance != null)
                {
                    Plugin.Instance.StopCoroutine(_waveCoroutine);
                    _waveCoroutine = null;
                }
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

        public void Clear()
        {
            TotalDamage = 0;
            TotalHeal = 0;
            TotalShield = 0;
            DamageByItem.Clear();
            StatusEffects.Clear();
            HadCrit = false;
        }

        public bool HasActivity => TotalDamage > 0 || TotalHeal > 0 || TotalShield > 0 || StatusEffects.Count > 0;

        public string GetTopItem()
        {
            if (DamageByItem.Count == 0) return null;
            return DamageByItem.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

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

        // Initialize state
        _lastHealthTime = Time.time;
        _lastPlayerHealth = 0;
        _lastPlayerMaxHealth = 0;
        _lastEnemyHealth = 0;
        _lastEnemyMaxHealth = 0;
        _playerWave.Clear();
        _enemyWave.Clear();

        // Reset totals
        _totalPlayerDamageDealt = 0;
        _totalPlayerDamageTaken = 0;

        // Reset health threshold warnings
        _announcedPlayerLow = false;
        _announcedPlayerCrit = false;
        _announcedEnemyLow = false;
        _announcedEnemyCrit = false;

        // Get enemy name
        _enemyName = GetEnemyName();

        // Capture initial health
        CaptureHealthState();

        // Start periodic health announcements only in batched mode
        if (UseBatchedMode && Plugin.Instance != null)
        {
            _healthCoroutine = Plugin.Instance.StartCoroutine(HealthAnnouncementLoop());
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
        if (_healthCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_healthCoroutine);
            _healthCoroutine = null;
        }
        if (_waveCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_waveCoroutine);
            _waveCoroutine = null;
        }

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
        if (!_active) return "Not in combat.";

        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            int playerHealth = player?.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
            int playerShield = player?.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;
            int enemyHealth = 0;
            int enemyShield = 0;
            opponent?.Attributes.TryGetValue(EPlayerAttributeType.Health, out enemyHealth);
            opponent?.Attributes.TryGetValue(EPlayerAttributeType.Shield, out enemyShield);

            var parts = new List<string>();

            // Damage totals
            parts.Add($"You dealt {_totalPlayerDamageDealt}, took {_totalPlayerDamageTaken}");

            // Health comparison
            string playerHealthStr = playerShield > 0 ? $"{playerHealth}+{playerShield}" : $"{playerHealth}";
            string enemyHealthStr = enemyShield > 0 ? $"{enemyHealth}+{enemyShield}" : $"{enemyHealth}";
            parts.Add($"Health: {playerHealthStr} vs {enemyHealthStr}");

            return string.Join(". ", parts);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"GetCombatSummary error: {ex.Message}");
            return "Summary unavailable.";
        }
    }

    /// <summary>
    /// Gets player health as a number string (for 1 key).
    /// </summary>
    public static string GetPlayerHealth()
    {
        var player = Data.Run?.Player;
        int health = player?.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
        return health.ToString();
    }

    /// <summary>
    /// Gets enemy health as a number string (for 2 key).
    /// </summary>
    public static string GetEnemyHealth()
    {
        var opponent = Data.Run?.Opponent;
        int health = 0;
        opponent?.Attributes.TryGetValue(EPlayerAttributeType.Health, out health);
        return health.ToString();
    }

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
    /// Gets the enemy name (PvP or PvE).
    /// </summary>
    private static string GetEnemyName()
    {
        try
        {
            var currentState = Data.CurrentState?.StateName;
            bool isPvpCombat = currentState == ERunState.PVPCombat;

            if (isPvpCombat)
            {
                var pvp = Data.SimPvpOpponent;
                if (pvp != null && !string.IsNullOrEmpty(pvp.Name))
                {
                    return pvp.Name;
                }
            }

            return "Enemy";
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetEnemyName error: {ex.Message}");
            return "Enemy";
        }
    }

    /// <summary>
    /// Captures current health state.
    /// </summary>
    private static void CaptureHealthState()
    {
        try
        {
            var player = Data.Run?.Player;
            if (player != null)
            {
                _lastPlayerHealth = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                _lastPlayerMaxHealth = player.GetAttributeValue(EPlayerAttributeType.HealthMax) ?? 100;
            }

            var opponent = Data.Run?.Opponent;
            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out _lastEnemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out _lastEnemyMaxHealth);
                if (_lastEnemyMaxHealth == 0) _lastEnemyMaxHealth = 100;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CaptureHealthState error: {ex.Message}");
        }
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

            if (!IsRelevantAction(data.ActionType)) return;

            var sourceCard = data.SourceCard;
            if (sourceCard == null) return;

            // Determine owner and effect details
            bool isPlayerItem = IsPlayerCard(sourceCard);
            string itemName = ItemReader.GetCardName(sourceCard);
            int amount = CalculateEffectAmount(data);
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
                wave.StatusEffects.Add("burn");
                break;

            case ActionType.PlayerPoisonApply:
                wave.StatusEffects.Add("poison");
                break;

            case ActionType.CardSlow:
                wave.StatusEffects.Add("slow");
                break;

            case ActionType.CardFreeze:
                if (!isPlayerItem)
                {
                    // Enemy freeze - special "Frozen!" alert
                    TolkWrapper.Speak("Frozen!", interrupt: true);
                }
                else
                {
                    wave.StatusEffects.Add("freeze");
                }
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

        string announcement = FormatEffectAnnouncement(itemName, isPlayerItem, data.ActionType, amount, isCrit);
        if (!string.IsNullOrEmpty(announcement))
        {
            TolkWrapper.Speak(announcement, interrupt: false);
        }
    }

    /// <summary>
    /// Formats an immediate effect announcement for individual mode.
    /// Player: "Sword: 10 damage" | Enemy: "Enemy Sword: 10 damage"
    /// </summary>
    private static string FormatEffectAnnouncement(string itemName, bool isPlayerItem, ActionType actionType, int amount, bool isCrit)
    {
        // Prefix with "Enemy" for opponent items
        string prefix = isPlayerItem ? "" : "Enemy ";
        string name = string.IsNullOrEmpty(itemName) ? "Item" : itemName;

        string effectText = actionType switch
        {
            ActionType.PlayerDamage => amount > 0 ? $"{amount} damage" : "damage",
            ActionType.PlayerHeal => amount > 0 ? $"{amount} heal" : "heal",
            ActionType.PlayerShieldApply => amount > 0 ? $"{amount} shield" : "shield",
            ActionType.PlayerBurnApply => "burn",
            ActionType.PlayerPoisonApply => "poison",
            ActionType.CardSlow => "slow",
            ActionType.CardFreeze => isPlayerItem ? "freeze" : null, // Enemy freeze uses special "Frozen!" alert
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

        // Add crit suffix if applicable
        if (isCrit && actionType == ActionType.PlayerDamage)
        {
            effectText += ", crit";
        }

        return $"{prefix}{name}: {effectText}";
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
        if (_waveCoroutine != null && Plugin.Instance != null)
        {
            Plugin.Instance.StopCoroutine(_waveCoroutine);
        }
        if (Plugin.Instance != null)
        {
            _waveCoroutine = Plugin.Instance.StartCoroutine(WaveTimeoutCoroutine());
        }
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
            string playerPart = FormatWaveSide("You", _playerWave);
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
    /// </summary>
    private static string FormatWaveSide(string owner, WaveData wave)
    {
        var elements = new List<string>();

        // Main effect (damage or heal or shield)
        if (wave.TotalDamage > 0)
        {
            string topItem = wave.GetTopItem();
            if (!string.IsNullOrEmpty(topItem))
                elements.Add($"{wave.TotalDamage} damage ({topItem})");
            else
                elements.Add($"{wave.TotalDamage} damage");
        }

        if (wave.TotalHeal > 0)
        {
            elements.Add($"{wave.TotalHeal} heal");
        }

        if (wave.TotalShield > 0)
        {
            elements.Add($"{wave.TotalShield} shield");
        }

        // Status effects
        if (wave.StatusEffects.Count > 0)
        {
            elements.AddRange(wave.StatusEffects);
        }

        if (elements.Count == 0)
            return null;

        string result = $"{owner}: {string.Join(", ", elements)}";

        // Add critical if applicable
        if (wave.HadCrit)
            result += ", critical";

        return result;
    }

    #endregion

    #region ===== SHARED: HEALTH WARNINGS =====
    // Health threshold warnings (low/critical) are active in both modes.

    /// <summary>
    /// Handler for health changes - checks for threshold warnings.
    /// </summary>
    internal static void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        if (!_active) return;

        try
        {
            CheckHealthThresholds();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnPlayerHealthChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if health thresholds have been crossed.
    /// </summary>
    private static void CheckHealthThresholds()
    {
        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            if (player != null)
            {
                int health = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                int maxHealth = player.GetAttributeValue(EPlayerAttributeType.HealthMax) ?? 100;
                float ratio = maxHealth > 0 ? (float)health / maxHealth : 1f;

                if (ratio <= CritHealthThreshold && !_announcedPlayerCrit)
                {
                    TolkWrapper.Speak("Critical health!", interrupt: true);
                    _announcedPlayerCrit = true;
                    _announcedPlayerLow = true;
                }
                else if (ratio <= LowHealthThreshold && !_announcedPlayerLow)
                {
                    TolkWrapper.Speak("Low health!", interrupt: true);
                    _announcedPlayerLow = true;
                }
            }

            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int enemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out int enemyMaxHealth);
                if (enemyMaxHealth == 0) enemyMaxHealth = 100;
                float ratio = (float)enemyHealth / enemyMaxHealth;

                if (ratio <= CritHealthThreshold && !_announcedEnemyCrit)
                {
                    TolkWrapper.Speak("Enemy critical!", interrupt: true);
                    _announcedEnemyCrit = true;
                    _announcedEnemyLow = true;
                }
                else if (ratio <= LowHealthThreshold && !_announcedEnemyLow)
                {
                    TolkWrapper.Speak("Enemy low!", interrupt: true);
                    _announcedEnemyLow = true;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CheckHealthThresholds error: {ex.Message}");
        }
    }

    #endregion

    #region ===== SHARED: UTILITIES =====
    // Utility methods used by both modes.

    /// <summary>
    /// Checks if an action type is relevant for narration.
    /// </summary>
    private static bool IsRelevantAction(ActionType type)
    {
        return type switch
        {
            ActionType.PlayerDamage => true,
            ActionType.PlayerHeal => true,
            ActionType.PlayerShieldApply => true,
            ActionType.PlayerBurnApply => true,
            ActionType.PlayerPoisonApply => true,
            ActionType.CardSlow => true,
            ActionType.CardFreeze => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a card belongs to the player.
    /// </summary>
    private static bool IsPlayerCard(Card card)
    {
        if (card == null) return false;

        try
        {
            var bm = Singleton<BoardManager>.Instance;
            if (bm == null) return true; // Default to player

            // Check player sockets
            if (bm.playerItemSockets != null)
            {
                foreach (var socket in bm.playerItemSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return true;
                }
            }

            if (bm.playerSkillSockets != null)
            {
                foreach (var socket in bm.playerSkillSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return true;
                }
            }

            // Check opponent sockets
            if (bm.opponentItemSockets != null)
            {
                foreach (var socket in bm.opponentItemSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return false;
                }
            }

            if (bm.opponentSkillSockets != null)
            {
                foreach (var socket in bm.opponentSkillSockets)
                {
                    if (socket?.CardController?.CardData == card)
                        return false;
                }
            }

            return true; // Default to player
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Calculates the effect amount from card attributes.
    /// </summary>
    private static int CalculateEffectAmount(CombatActionData data)
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
            _ => 0
        };

        // Fallback to health diff for damage/heal if attribute not found
        if (amount == 0 && (data.ActionType == ActionType.PlayerDamage || data.ActionType == ActionType.PlayerHeal))
        {
            if (data.HealthBefore > 0 || data.HealthAfter > 0)
            {
                amount = (int)Math.Abs(data.HealthBefore - data.HealthAfter);
            }
        }

        return amount;
    }

    #endregion

    #region ===== BATCHED MODE: PERIODIC HEALTH =====
    // Periodic health announcements are only active in batched mode.
    // Modify these methods to change timing or format of health updates.

    /// <summary>
    /// Periodic health announcement loop (batched mode only).
    /// </summary>
    private static IEnumerator HealthAnnouncementLoop()
    {
        // First announcement after 2 seconds
        yield return new WaitForSeconds(2f);
        if (_active)
        {
            AnnounceHealth();
        }

        while (_active)
        {
            yield return new WaitForSeconds(HealthInterval);
            if (_active)
            {
                AnnounceHealth();
            }
        }
    }

    /// <summary>
    /// Announces current health status.
    /// </summary>
    private static void AnnounceHealth()
    {
        try
        {
            var player = Data.Run?.Player;
            var opponent = Data.Run?.Opponent;

            var parts = new List<string>();

            if (player != null)
            {
                int health = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                int shield = player.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;

                if (shield > 0)
                    parts.Add($"You: {health} health, {shield} shield");
                else
                    parts.Add($"You: {health} health");

                _lastPlayerHealth = health;
            }

            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int enemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Shield, out int enemyShield);

                if (enemyShield > 0)
                    parts.Add($"{_enemyName}: {enemyHealth} health, {enemyShield} shield");
                else
                    parts.Add($"{_enemyName}: {enemyHealth} health");

                _lastEnemyHealth = enemyHealth;
            }

            if (parts.Count > 0)
            {
                TolkWrapper.Speak(string.Join(". ", parts), interrupt: false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"AnnounceHealth error: {ex.Message}");
        }
    }

    #endregion
}
