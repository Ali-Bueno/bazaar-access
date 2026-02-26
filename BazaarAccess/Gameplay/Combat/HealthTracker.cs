using System;
using System.Collections;
using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.CombatSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Gameplay.Combat;

/// <summary>
/// Tracks player and enemy health during combat, provides health queries (1/2 keys),
/// periodic health announcements (batched mode), and low/critical threshold warnings.
/// </summary>
public static class HealthTracker
{
    // Configuration
    private const float HealthInterval = 5f;        // Seconds between health announcements
    private const float LowHealthThreshold = 0.25f; // 25% = low health warning
    private const float CritHealthThreshold = 0.10f; // 10% = critical health warning

    // Health state
    private static int _lastPlayerHealth;
    private static int _lastPlayerMaxHealth;
    private static int _lastEnemyHealth;
    private static int _lastEnemyMaxHealth;

    // Health threshold tracking (to avoid repeating warnings)
    private static bool _announcedPlayerLow;
    private static bool _announcedPlayerCrit;
    private static bool _announcedEnemyLow;
    private static bool _announcedEnemyCrit;

    // Coroutine for periodic health announcements
    private static Coroutine _healthCoroutine;

    // Enemy name reference (set by CombatDescriber)
    private static string _enemyName;

    // Whether combat is active (set by CombatDescriber)
    private static bool _active;

    /// <summary>
    /// Initializes health tracking for a new combat.
    /// </summary>
    public static void Start(string enemyName)
    {
        _active = true;
        _enemyName = enemyName;

        _lastPlayerHealth = 0;
        _lastPlayerMaxHealth = 0;
        _lastEnemyHealth = 0;
        _lastEnemyMaxHealth = 0;

        _announcedPlayerLow = false;
        _announcedPlayerCrit = false;
        _announcedEnemyLow = false;
        _announcedEnemyCrit = false;

        CaptureHealthState();
    }

    /// <summary>
    /// Stops health tracking.
    /// </summary>
    public static void Stop()
    {
        _active = false;
        _enemyName = null;
        StopCoroutine();
    }

    /// <summary>
    /// Starts the periodic health announcement coroutine (batched mode only).
    /// </summary>
    public static void StartPeriodicAnnouncements()
    {
        CoroutineHelper.StartSafe(ref _healthCoroutine, HealthAnnouncementLoop());
    }

    /// <summary>
    /// Stops the periodic health announcement coroutine.
    /// </summary>
    public static void StopCoroutine()
    {
        CoroutineHelper.StopSafe(ref _healthCoroutine);
    }

    /// <summary>
    /// Captures current health state from game data.
    /// </summary>
    public static void CaptureHealthState()
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
    /// Handler for health changes - checks for threshold warnings.
    /// </summary>
    public static void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
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
    /// Checks if health thresholds have been crossed and announces warnings.
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
    /// Note: The game may report Health as (actual health + shield), so we subtract shield to get actual health.
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
                int reportedHealth = player.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
                int shield = player.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;
                // Game may include shield in health, so subtract to get actual health
                int actualHealth = reportedHealth - shield;
                if (actualHealth < 0) actualHealth = reportedHealth; // Fallback if assumption is wrong

                if (shield > 0)
                    parts.Add($"You: {actualHealth} health, {shield} shield");
                else
                    parts.Add($"You: {reportedHealth} health");

                _lastPlayerHealth = actualHealth;
            }

            if (opponent != null)
            {
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int reportedEnemyHealth);
                opponent.Attributes.TryGetValue(EPlayerAttributeType.Shield, out int enemyShield);
                // Game may include shield in health, so subtract to get actual health
                int actualEnemyHealth = reportedEnemyHealth - enemyShield;
                if (actualEnemyHealth < 0) actualEnemyHealth = reportedEnemyHealth; // Fallback if assumption is wrong

                if (enemyShield > 0)
                    parts.Add($"{_enemyName}: {actualEnemyHealth} health, {enemyShield} shield");
                else
                    parts.Add($"{_enemyName}: {reportedEnemyHealth} health");

                _lastEnemyHealth = actualEnemyHealth;
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

    /// <summary>
    /// Gets player health as a string (for 1 key).
    /// Format: "400" or "400 with 50 shield" if shield is present.
    /// </summary>
    public static string GetPlayerHealth()
    {
        var player = Data.Run?.Player;
        int reportedHealth = player?.GetAttributeValue(EPlayerAttributeType.Health) ?? 0;
        int shield = player?.GetAttributeValue(EPlayerAttributeType.Shield) ?? 0;
        int health = reportedHealth - shield;
        if (health < 0) health = reportedHealth;

        if (shield > 0)
            return $"{health} with {shield} shield";
        return health.ToString();
    }

    /// <summary>
    /// Gets enemy health as a string (for 2 key).
    /// Format: "400" or "400 with 50 shield" if shield is present.
    /// </summary>
    public static string GetEnemyHealth()
    {
        var opponent = Data.Run?.Opponent;
        int reportedHealth = 0;
        int shield = 0;
        opponent?.Attributes.TryGetValue(EPlayerAttributeType.Health, out reportedHealth);
        opponent?.Attributes.TryGetValue(EPlayerAttributeType.Shield, out shield);
        int health = reportedHealth - shield;
        if (health < 0) health = reportedHealth;

        if (shield > 0)
            return $"{health} with {shield} shield";
        return health.ToString();
    }
}
