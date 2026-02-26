using System;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarBattleService.Models;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.GameSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Handles combat lifecycle events: start, end, results, health changes.
/// </summary>
public static class CombatEventHandler
{
    // Track if we already announced the combat result to avoid duplicates
    private static bool _combatResultAnnounced = false;

    // Cached combat result data for building consolidated messages
    private static BazaarMatchHistory.EVictoryCondition _pendingCombatResult;
    private static Coroutine _combatResultCoroutine;

    /// <summary>
    /// When combat starts.
    /// </summary>
    public static void OnCombatStarted()
    {
        Plugin.Logger.LogInfo("CombatStarted");
        StateChangePatch._inCombat = true;
        StateChangePatch._combatBoardReady = false;
        _combatResultAnnounced = false; // Reset for the new combat

        // Start combat narration
        CombatDescriber.StartDescribing();

        // Set board ready after a short delay for items to appear
        Plugin.Instance.StartCoroutine(DelayedSetCombatBoardReady());

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(true);
    }

    /// <summary>
    /// When combat ends.
    /// </summary>
    public static void OnCombatEnded()
    {
        Plugin.Logger.LogInfo("CombatEnded");
        StateChangePatch._inCombat = false;
        StateChangePatch._combatBoardReady = false;

        // Stop combat narration
        CombatDescriber.StopDescribing();

        // Reset the combat result flag for the next combat
        _combatResultAnnounced = false;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(false);
    }

    /// <summary>
    /// When combat finishes with a result (fires for BOTH PvE and PvP).
    /// Uses a short delay to consolidate with victory/prestige updates into one message.
    /// </summary>
    public static void OnCombatResult(BazaarMatchHistory.EVictoryCondition result)
    {
        Plugin.Logger.LogInfo($"OnCombatResult: {result}");

        // Avoid duplicate announcements
        if (_combatResultAnnounced)
        {
            Plugin.Logger.LogInfo("OnCombatResult: Already announced, skipping");
            return;
        }
        _combatResultAnnounced = true;

        // Store result and start delayed announcement
        _pendingCombatResult = result;

        // Cancel any existing coroutine
        if (_combatResultCoroutine != null)
        {
            Plugin.Instance.StopCoroutine(_combatResultCoroutine);
        }

        // Wait briefly for victory/prestige events to fire, then announce consolidated message
        _combatResultCoroutine = Plugin.Instance.StartCoroutine(DelayedCombatResultAnnounce());
    }

    /// <summary>
    /// Announces combat result after a short delay to include updated stats.
    /// </summary>
    private static System.Collections.IEnumerator DelayedCombatResultAnnounce()
    {
        yield return new WaitForSeconds(0.15f);

        try
        {
            string message;
            if (_pendingCombatResult == BazaarMatchHistory.EVictoryCondition.Win)
            {
                // Get current victory count from Data.Run.Victories
                uint victories = Data.Run?.Victories ?? 0;
                if (victories > 0)
                {
                    message = $"Victory! {victories} wins";
                }
                else
                {
                    message = "Victory!";
                }
            }
            else
            {
                // Get current prestige
                int prestige = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Prestige) ?? 0;
                message = $"Defeat! {prestige} prestige remaining";
            }

            TolkWrapper.Speak(message);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"DelayedCombatResultAnnounce error: {ex.Message}");
            // Fallback to simple announcement
            TolkWrapper.Speak(_pendingCombatResult == BazaarMatchHistory.EVictoryCondition.Win ? "Victory!" : "Defeat!");
        }

        _combatResultCoroutine = null;
    }

    /// <summary>
    /// When victory count increases (we won PvP combat).
    /// Does NOT speak - consolidated with OnCombatResult.
    /// </summary>
    public static void OnVictoryCountChanged(uint newVictoryCount)
    {
        Plugin.Logger.LogInfo($"VictoryCountChanged: {newVictoryCount}");
        // Do not speak here - OnCombatResult handles the consolidated message
    }

    /// <summary>
    /// When prestige changes (if it decreases, we lost).
    /// Does NOT speak - consolidated with OnCombatResult.
    /// </summary>
    public static void OnPrestigeChanged(GameSimEventPlayerPrestigeChanged evt)
    {
        Plugin.Logger.LogInfo($"PrestigeChanged: Delta={evt.Delta}");
        // Do not speak here - OnCombatResult handles the consolidated message
    }

    private static System.Collections.IEnumerator DelayedSetCombatBoardReady()
    {
        yield return new WaitForSeconds(1.5f);
        if (StateChangePatch._inCombat)
        {
            StateChangePatch._combatBoardReady = true;
            Plugin.Logger.LogInfo("Combat board ready (after delay)");
        }
    }
}
