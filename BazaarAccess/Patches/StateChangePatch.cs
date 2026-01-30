using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarBattleService.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Infra.Messages.GameSimEvents;
using BazaarGameShared.Domain.Cards.Enchantments;
using TheBazaar;
using UnityEngine;

// For combat describer events
using EffectTriggeredEvent = TheBazaar.EffectTriggeredEvent;
using PlayerHealthChangedEvent = TheBazaar.PlayerHealthChangedEvent;

namespace BazaarAccess.Patches;

/// <summary>
/// Listens to gameplay state changes in real time.
/// Uses native game events for reliability.
/// </summary>
public static class StateChangePatch
{
    private static ERunState _lastState = ERunState.Choice;
    private static bool _initialized = false;
    private static bool _inCombat = false;
    private static bool _inReplayState = false;
    private static bool _combatBoardReady = false;
    private static Type _eventsType;
    private static Type _replayStateType;

    // Track if we already announced the combat result to avoid duplicates
    private static bool _combatResultAnnounced = false;

    // Last valid state (not fallback) - used when Data.CurrentState is temporarily unavailable
    private static ERunState _lastValidState = ERunState.Choice;
    private static bool _hasValidState = false;

    // Track day/hour for announcing changes
    private static int _lastDay = 0;
    private static int _lastHour = 0;

    // Throttle to avoid announcement spam
    private static Coroutine _announceCoroutine = null;
    private static float _lastAnnounceTime = 0f;
    private const float ANNOUNCE_DEBOUNCE_DELAY = 0.4f; // Seconds to wait before announcing
    private const float ANNOUNCE_THROTTLE_WINDOW = 1.0f; // Minimum window between announcements

    public static bool IsInCombat => _inCombat;
    public static bool IsInReplayState => _inReplayState;
    public static bool IsCombatBoardReady => _combatBoardReady;

    /// <summary>
    /// Initializes event subscriptions.
    /// </summary>
    public static void Subscribe()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _eventsType = typeof(AppState).Assembly.GetType("TheBazaar.Events");
            if (_eventsType == null)
            {
                Plugin.Logger.LogError("StateChangePatch: TheBazaar.Events not found");
                return;
            }

            _replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (_replayStateType == null)
            {
                Plugin.Logger.LogWarning("StateChangePatch: TheBazaar.ReplayState not found");
            }

            // === State change events ===
            SubscribeToEvent("StateChanged", typeof(Action<StateChangedEvent>),
                (Action<StateChangedEvent>)OnStateChanged);

            // === Transition/animation completed events ===
            SubscribeToEventNoParam("BoardTransitionFinished", OnBoardTransitionFinished);
            SubscribeToEventNoParam("NewDayTransitionAnimationFinished", OnNewDayTransitionFinished);

            // === Combat events ===
            SubscribeToEventNoParam("CombatStarted", OnCombatStarted);
            SubscribeToEventNoParam("CombatEnded", OnCombatEnded);

            // === Victory/defeat events ===
            // OnCombatPvEFinish fires for ALL combats (PvE and PvP) with the result
            SubscribeToGameServiceManagerEvent();

            // VictoryCountChanged only fires for PvP wins (victory counter)
            SubscribeToEvent("VictoryCountChanged", typeof(Action<uint>),
                (Action<uint>)OnVictoryCountChanged);
            // PrestigeChanged fires when we lose prestige (defeat)
            SubscribeToEvent("PlayerPrestigeChangedSimEvent", typeof(Action<GameSimEventPlayerPrestigeChanged>),
                (Action<GameSimEventPlayerPrestigeChanged>)OnPrestigeChanged);

            // === Combat narration events ===
            SubscribeToEvent("EffectTriggered", typeof(Action<EffectTriggeredEvent>),
                (Action<EffectTriggeredEvent>)CombatDescriber.OnEffectTriggered);
            SubscribeToEvent("PlayerHealthChanged", typeof(Action<PlayerHealthChangedEvent>),
                (Action<PlayerHealthChangedEvent>)CombatDescriber.OnPlayerHealthChanged);

            // === Buy/sell events ===
            SubscribeToEvent("CardPurchasedSimEvent", typeof(Action<GameSimEventCardPurchased>),
                (Action<GameSimEventCardPurchased>)OnCardPurchased);
            SubscribeToEvent("CardSoldSimEvent", typeof(Action<GameSimEventCardSold>),
                (Action<GameSimEventCardSold>)OnCardSold);

            // === Skill equipped event (fires when a skill is added to the player) ===
            SubscribeToEvent("PlayerSkillEquippedSimEvent", typeof(Action<GameSimEventPlayerSkillEquipped>),
                (Action<GameSimEventPlayerSkillEquipped>)OnSkillEquipped);

            // === Card events (disposed = removed from selection after buy) ===
            SubscribeToEvent("CardDisposedSimEvent", typeof(Action<List<Card>>),
                (Action<List<Card>>)OnCardDisposed);

            // === Card selection event (fires immediately when card is clicked) ===
            SubscribeToEventNoParam("CardSelected", OnCardSelected);

            // === Item purchase/selection event (AppState event, fires for all items including loot) ===
            AppState.ItemPurchased += OnItemPurchased;

            // === Board events ===
            SubscribeToEventNoParam("OnBoardChanged", OnBoardChanged);

            // === Stash events ===
            SubscribeToEvent("StorageToggled", typeof(Action<bool>),
                (Action<bool>)OnStorageToggled);

            // === Replay events ===
            SubscribeToEventNoParam("ReplayEnded", OnReplayEnded);

            // === Error events ===
            SubscribeToEvent("NotEnoughSpace", typeof(Action<Card>),
                (Action<Card>)OnNotEnoughSpace);
            SubscribeToEvent("CantAffordCard", typeof(Action<Card>),
                (Action<Card>)OnCantAffordCard);
            SubscribeToEvent("UnsellableItemSaleAttempt", typeof(Action<Card>),
                (Action<Card>)OnUnsellableItemAttempt);

            // === BoardManager events (cards revealed) ===
            SubscribeToBoardManagerEvent("ItemCardsRevealed", OnItemCardsRevealed);
            SubscribeToBoardManagerEvent("SkillCardsRevealed", OnSkillCardsRevealed);

            // === AppState events ===
            SubscribeToAppStateEvent("StateExited", OnStateExited);
            SubscribeToAppStateEvent("EncounterEntered", OnEncounterEntered);

            // === Card modification events (enchant/upgrade) ===
            SubscribeToEvent("CardEnchantedSimEvent", typeof(Action<GameSimEventCardEnchanted>),
                (Action<GameSimEventCardEnchanted>)OnCardEnchanted);
            SubscribeToEvent("CardUpgradedSimEvent", typeof(Action<GameSimEventCardUpgraded>),
                (Action<GameSimEventCardUpgraded>)OnCardUpgraded);

            Plugin.Logger.LogInfo("StateChangePatch: Subscribed to game events");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"StateChangePatch.Subscribe error: {ex.Message}");
        }
    }

    #region Event Subscription Helpers

    private static void SubscribeToEvent(string eventName, Type handlerType, Delegate handler)
    {
        try
        {
            // Search in public and non-public fields (Events is internal)
            var eventField = _eventsType.GetField(eventName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (eventField == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: Events.{eventName} not found");
                return;
            }

            var eventObj = eventField.GetValue(null);
            if (eventObj == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: Events.{eventName} is null");
                return;
            }

            // Find AddListener with the specific handler type
            var addMethod = eventObj.GetType().GetMethod("AddListener",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { handlerType, typeof(MonoBehaviour) },
                null);

            if (addMethod != null)
            {
                addMethod.Invoke(eventObj, new object[] { handler, null });
                Plugin.Logger.LogInfo($"StateChangePatch: Subscribed to Events.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: AddListener not found for Events.{eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error subscribing to {eventName}: {ex.Message}");
        }
    }

    private static void SubscribeToEventNoParam(string eventName, Action handler)
    {
        try
        {
            var eventField = _eventsType.GetField(eventName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (eventField == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: Events.{eventName} not found (NoParam)");
                return;
            }

            var eventObj = eventField.GetValue(null);
            if (eventObj == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: Events.{eventName} is null (NoParam)");
                return;
            }

            var addMethod = eventObj.GetType().GetMethod("AddListener",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(Action), typeof(MonoBehaviour) },
                null);

            if (addMethod != null)
            {
                addMethod.Invoke(eventObj, new object[] { handler, null });
                Plugin.Logger.LogInfo($"StateChangePatch: Subscribed to Events.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: AddListener not found for Events.{eventName} (NoParam)");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error subscribing to {eventName}: {ex.Message}");
        }
    }

    private static void SubscribeToBoardManagerEvent(string eventName, Action handler)
    {
        try
        {
            var eventInfo = typeof(BoardManager).GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo != null)
            {
                eventInfo.AddEventHandler(null, handler);
                Plugin.Logger.LogInfo($"StateChangePatch: Subscribed to BoardManager.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: BoardManager.{eventName} not found");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error subscribing to BoardManager.{eventName}: {ex.Message}");
        }
    }

    private static void SubscribeToAppStateEvent(string eventName, Action handler)
    {
        try
        {
            var eventInfo = typeof(AppState).GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventInfo != null)
            {
                eventInfo.AddEventHandler(null, handler);
                Plugin.Logger.LogInfo($"StateChangePatch: Subscribed to AppState.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: AppState.{eventName} not found");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error subscribing to AppState.{eventName}: {ex.Message}");
        }
    }

    private static void SubscribeToGameServiceManagerEvent()
    {
        try
        {
            var gsm = Singleton<GameServiceManager>.Instance;
            if (gsm != null)
            {
                gsm.OnCombatPvEFinish += OnCombatResult;
                Plugin.Logger.LogInfo("StateChangePatch: Subscribed to GameServiceManager.OnCombatPvEFinish");
            }
            else
            {
                // GameServiceManager may not be ready yet, try with coroutine
                Plugin.Instance.StartCoroutine(DelayedSubscribeToGameServiceManager());
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error subscribing to GameServiceManager: {ex.Message}");
        }
    }

    private static System.Collections.IEnumerator DelayedSubscribeToGameServiceManager()
    {
        yield return new WaitForSeconds(1f);
        try
        {
            var gsm = Singleton<GameServiceManager>.Instance;
            if (gsm != null)
            {
                gsm.OnCombatPvEFinish += OnCombatResult;
                Plugin.Logger.LogInfo("StateChangePatch: Subscribed to GameServiceManager.OnCombatPvEFinish (delayed)");
            }
            else
            {
                Plugin.Logger.LogWarning("StateChangePatch: GameServiceManager still not available");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error in delayed subscription: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Main state change event.
    /// </summary>
    private static void OnStateChanged(StateChangedEvent evt)
    {
        try
        {
            var newState = GetCurrentRunState();
            Plugin.Logger.LogInfo($"OnStateChanged: {_lastState} -> {newState}");

            bool stateActuallyChanged = newState != _lastState;
            _lastState = newState;

            // Reset day/hour tracking on new run
            if (newState == ERunState.NewRun)
            {
                _lastDay = 0;
                _lastHour = 0;
            }

            // Check for day/hour changes after a delay (game data updates after state change)
            Plugin.Instance.StartCoroutine(DelayedCheckDayHourChanges());

            // Detect if we enter/exit ReplayState
            bool wasInReplayState = _inReplayState;
            _inReplayState = _replayStateType != null &&
                             _replayStateType.IsInstanceOfType(AppState.CurrentState);

            if (_inReplayState && !wasInReplayState)
            {
                Plugin.Logger.LogInfo("Entered ReplayState (post-combat)");
            }
            else if (!_inReplayState && wasInReplayState)
            {
                Plugin.Logger.LogInfo("Exited ReplayState - triggering delayed refresh");
                // Cuando salimos del ReplayState, necesitamos refrescar la UI después de un delay
                Plugin.Instance.StartCoroutine(DelayedRefreshAfterExitReplayState());
            }

            if (AccessibilityMgr.GetFocusedUI() == null)
            {
                var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
                screen?.OnStateChanged(newState, stateActuallyChanged);

                // Notify about ReplayState change
                if (_inReplayState != wasInReplayState)
                {
                    screen?.OnReplayStateChanged(_inReplayState);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnStateChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// When a board transition finishes (animations complete).
    /// This is the MAIN event for announcing - others only refresh.
    /// </summary>
    private static void OnBoardTransitionFinished()
    {
        Plugin.Logger.LogInfo("BoardTransitionFinished - UI ready");

        CheckAndAnnounceDayHourChanges();
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// When new day animation finishes.
    /// </summary>
    private static void OnNewDayTransitionFinished()
    {
        Plugin.Logger.LogInfo("NewDayTransitionAnimationFinished - UI ready");
        CheckAndAnnounceDayHourChanges();
        // Only refresh, BoardTransitionFinished will announce
        TriggerRefresh();
    }

    /// <summary>
    /// When item cards are revealed (after animation).
    /// </summary>
    private static void OnItemCardsRevealed()
    {
        Plugin.Logger.LogInfo("ItemCardsRevealed - Cards ready");
        // Only refresh, BoardTransitionFinished will announce
        TriggerRefresh();
    }

    /// <summary>
    /// When skill cards are revealed.
    /// </summary>
    private static void OnSkillCardsRevealed()
    {
        Plugin.Logger.LogInfo("SkillCardsRevealed - Skills ready");
        // Only refresh, BoardTransitionFinished will announce
        TriggerRefresh();
    }

    /// <summary>
    /// When exiting a state (before entering the next one).
    /// </summary>
    private static void OnStateExited()
    {
        Plugin.Logger.LogInfo("AppState.StateExited");
        // Don't announce here, wait for the new state to be ready
    }

    /// <summary>
    /// When entering an encounter.
    /// </summary>
    private static void OnEncounterEntered()
    {
        Plugin.Logger.LogInfo("AppState.EncounterEntered");
        // Only refresh, the next BoardTransitionFinished will announce
        TriggerRefresh();
    }

    /// <summary>
    /// When combat starts.
    /// </summary>
    private static void OnCombatStarted()
    {
        Plugin.Logger.LogInfo("CombatStarted");
        _inCombat = true;
        _combatBoardReady = false;
        _combatResultAnnounced = false; // Reset for the new combat

        // Start combat narration
        CombatDescriber.StartDescribing();

        // Set board ready after a short delay for items to appear
        Plugin.Instance.StartCoroutine(DelayedSetCombatBoardReady());

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(true);
    }

    private static System.Collections.IEnumerator DelayedSetCombatBoardReady()
    {
        yield return new WaitForSeconds(1.5f);
        if (_inCombat)
        {
            _combatBoardReady = true;
            Plugin.Logger.LogInfo("Combat board ready (after delay)");
        }
    }

    /// <summary>
    /// When combat ends.
    /// </summary>
    private static void OnCombatEnded()
    {
        Plugin.Logger.LogInfo("CombatEnded");
        _inCombat = false;
        _combatBoardReady = false;

        // Stop combat narration
        CombatDescriber.StopDescribing();

        // Reset the combat result flag for the next combat
        _combatResultAnnounced = false;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(false);
    }

    // Cached combat result data for building consolidated messages
    private static BazaarMatchHistory.EVictoryCondition _pendingCombatResult;
    private static Coroutine _combatResultCoroutine;

    /// <summary>
    /// When combat finishes with a result (fires for BOTH PvE and PvP).
    /// Uses a short delay to consolidate with victory/prestige updates into one message.
    /// </summary>
    private static void OnCombatResult(BazaarMatchHistory.EVictoryCondition result)
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
        yield return new UnityEngine.WaitForSeconds(0.15f);

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
    private static void OnVictoryCountChanged(uint newVictoryCount)
    {
        Plugin.Logger.LogInfo($"VictoryCountChanged: {newVictoryCount}");
        // Do not speak here - OnCombatResult handles the consolidated message
    }

    /// <summary>
    /// When prestige changes (if it decreases, we lost).
    /// Does NOT speak - consolidated with OnCombatResult.
    /// </summary>
    private static void OnPrestigeChanged(GameSimEventPlayerPrestigeChanged evt)
    {
        Plugin.Logger.LogInfo($"PrestigeChanged: Delta={evt.Delta}");
        // Do not speak here - OnCombatResult handles the consolidated message
    }

    private static void OnCardPurchased(GameSimEventCardPurchased evt)
    {
        Plugin.Logger.LogInfo($"Card purchased: {evt.InstanceId}");
        // Only refresh - ActionHelper already announced "Bought X"
        TriggerRefresh();
    }

    private static void OnCardSold(GameSimEventCardSold evt)
    {
        Plugin.Logger.LogInfo($"Card sold: {evt.InstanceId}");
        // Only refresh - ActionHelper already announced "Sold X"
        TriggerRefresh();
    }

    private static void OnSkillEquipped(GameSimEventPlayerSkillEquipped evt)
    {
        // Only refresh if skill is for the player, not the opponent
        if (evt.Owner == ECombatantId.Player)
        {
            Plugin.Logger.LogInfo($"Skill equipped: {evt.InstanceId}");
            // Small delay to ensure Player.Skills has been updated
            Plugin.Instance.StartCoroutine(DelayedRefreshAfterSkillEquipped());
        }
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterSkillEquipped()
    {
        // Short delay - Player.Skills should already be updated
        yield return new UnityEngine.WaitForSeconds(0.1f);
        TriggerRefresh();
    }

    private static void OnCardDisposed(List<Card> cards)
    {
        Plugin.Logger.LogInfo($"Cards disposed: {cards?.Count ?? 0}");
        // Only refresh - selection/transition already announced
        TriggerRefresh();
    }

    private static void OnCardSelected()
    {
        Plugin.Logger.LogInfo("Card selected - triggering delayed refresh");
        // Use coroutine to wait for the game to process the selection
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterSelection());
    }

    private static void OnItemPurchased(Card card)
    {
        string cardName = card?.ToString() ?? "unknown";
        Plugin.Logger.LogInfo($"Item purchased/selected: {cardName} - triggering delayed refresh");
        // Use coroutine to wait for the game to process the selection
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterSelection());
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterSelection()
    {
        // Wait a bit for the game to process the selection
        yield return new UnityEngine.WaitForSeconds(0.3f);
        // Only refresh, game events will handle announcement with debounce
        TriggerRefresh();
    }

    private static void OnBoardChanged()
    {
        Plugin.Logger.LogInfo("Board changed");
        TriggerRefresh();
    }

    private static void OnStorageToggled(bool isOpen)
    {
        Plugin.Logger.LogInfo($"Storage toggled: {(isOpen ? "open" : "closed")}");
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnStorageToggled(isOpen);
    }

    /// <summary>
    /// When a replay ends (player watched the combat replay).
    /// </summary>
    private static void OnReplayEnded()
    {
        Plugin.Logger.LogInfo("ReplayEnded - Replay finished, refreshing UI");
        // After replay, UI may be outdated
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterReplay());
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterReplay()
    {
        // Wait for animation to finish
        yield return new UnityEngine.WaitForSeconds(0.5f);
        TriggerRefresh();
    }

    private static System.Collections.IEnumerator DelayedRefreshAfterExitReplayState()
    {
        // Multiple refreshes to capture late changes after ReplayState
        // Don't announce here - game events will do it with debounce
        yield return new UnityEngine.WaitForSeconds(0.3f);
        TriggerRefresh();

        yield return new UnityEngine.WaitForSeconds(0.5f);
        TriggerRefresh();

        yield return new UnityEngine.WaitForSeconds(0.5f);
        TriggerRefresh();
    }

    /// <summary>
    /// When there's no space for an item.
    /// </summary>
    private static void OnNotEnoughSpace(Card card)
    {
        string name = card != null ? Gameplay.ItemReader.GetCardName(card) : "item";
        Plugin.Logger.LogInfo($"NotEnoughSpace: {name}");
        TolkWrapper.Speak($"No space for {name}");
    }

    /// <summary>
    /// When there's not enough gold to buy.
    /// </summary>
    private static void OnCantAffordCard(Card card)
    {
        string name = card != null ? Gameplay.ItemReader.GetCardName(card) : "item";
        int price = card != null ? Gameplay.ItemReader.GetBuyPrice(card) : 0;
        Plugin.Logger.LogInfo($"CantAffordCard: {name} costs {price}");
        TolkWrapper.Speak($"Cannot afford {name}");
    }

    /// <summary>
    /// When the game rejects a sale because the item is unsellable.
    /// </summary>
    private static void OnUnsellableItemAttempt(Card card)
    {
        string name = card != null ? Gameplay.ItemReader.GetCardName(card) : "item";
        Plugin.Logger.LogInfo($"UnsellableItemAttempt: {name}");
        TolkWrapper.Speak($"{name} cannot be sold");
    }

    /// <summary>
    /// When a card is enchanted.
    /// Clears the detail cache so updated stats are read correctly.
    /// </summary>
    private static void OnCardEnchanted(GameSimEventCardEnchanted evt)
    {
        Plugin.Logger.LogInfo($"Card enchanted: {evt.InstanceId}, type={evt.EnchantmentType}");

        // Clear the detail cache in GameplayNavigator
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.ClearDetailCache();

        // Refresh to pick up new attributes
        TriggerRefresh();
    }

    /// <summary>
    /// When a card is upgraded (tier increased).
    /// Clears the detail cache so updated stats are read correctly.
    /// </summary>
    private static void OnCardUpgraded(GameSimEventCardUpgraded evt)
    {
        Plugin.Logger.LogInfo($"Card upgraded: {evt.InstanceId}, newTier={evt.NewTier}");

        // Clear the detail cache in GameplayNavigator
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.ClearDetailCache();

        // Refresh to pick up new attributes
        TriggerRefresh();
    }

    /// <summary>
    /// Checks if day or hour changed and announces it to the screen reader.
    /// </summary>
    private static void CheckAndAnnounceDayHourChanges()
    {
        try
        {
            var run = Data.Run;
            if (run == null) return;

            int currentDay = (int)run.Day;
            int currentHour = (int)run.Hour;

            // Announce day change (takes priority over hour)
            if (currentDay > 0 && currentDay != _lastDay)
            {
                TolkWrapper.Speak($"Day {currentDay}");
                _lastDay = currentDay;
                _lastHour = currentHour;
            }
            // Announce hour change (only if day didn't change)
            else if (currentHour > 0 && currentHour != _lastHour)
            {
                TolkWrapper.Speak($"Hour {currentHour}");
                _lastHour = currentHour;
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CheckAndAnnounceDayHourChanges error: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for game data to update, then checks for day/hour changes.
    /// </summary>
    private static System.Collections.IEnumerator DelayedCheckDayHourChanges()
    {
        // Wait for game to update Data.Run.Hour/Day
        yield return new UnityEngine.WaitForSeconds(0.5f);
        CheckAndAnnounceDayHourChanges();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Triggers a refresh of the gameplay screen (without announcing).
    /// </summary>
    public static void TriggerRefresh()
    {
        if (AccessibilityMgr.GetFocusedUI() != null) return;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.RefreshNavigator();
    }

    /// <summary>
    /// Triggers a refresh and announces current state with debounce + throttle.
    /// Multiple calls in a short period are grouped into a single announcement.
    /// Also, no more than one announcement per throttle window is allowed.
    /// </summary>
    public static void TriggerRefreshAndAnnounce()
    {
        if (AccessibilityMgr.GetFocusedUI() != null) return;

        // Always refresh immediately
        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.RefreshNavigator();

        // Throttle: if there was a recent announcement, ignore
        float timeSinceLastAnnounce = UnityEngine.Time.time - _lastAnnounceTime;
        if (timeSinceLastAnnounce < ANNOUNCE_THROTTLE_WINDOW)
        {
            Plugin.Logger.LogInfo($"TriggerRefreshAndAnnounce: Throttled, {timeSinceLastAnnounce:F2}s since last announce");
            return;
        }

        // Debounce: if there's already a pending announcement, don't start another
        if (_announceCoroutine != null)
        {
            Plugin.Logger.LogInfo("TriggerRefreshAndAnnounce: Debouncing, waiting for previous announce");
            return;
        }

        // Start coroutine with debounce
        _announceCoroutine = Plugin.Instance.StartCoroutine(DebouncedAnnounce());
    }

    /// <summary>
    /// Coroutine that waits a bit before announcing to group events.
    /// </summary>
    private static System.Collections.IEnumerator DebouncedAnnounce()
    {
        yield return new UnityEngine.WaitForSeconds(ANNOUNCE_DEBOUNCE_DELAY);

        _announceCoroutine = null;

        if (AccessibilityMgr.GetFocusedUI() != null) yield break;

        // Check throttle again before announcing
        float timeSinceLastAnnounce = UnityEngine.Time.time - _lastAnnounceTime;
        if (timeSinceLastAnnounce < ANNOUNCE_THROTTLE_WINDOW)
        {
            Plugin.Logger.LogInfo($"DebouncedAnnounce: Throttled at announce time, {timeSinceLastAnnounce:F2}s since last");
            yield break;
        }

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        if (screen != null)
        {
            // Final refresh before announcing
            screen.RefreshNavigator();
            screen.AnnounceStateImmediate();
            _lastAnnounceTime = UnityEngine.Time.time;
            Plugin.Logger.LogInfo("DebouncedAnnounce: State announced");
        }
    }

    /// <summary>
    /// Triggers a refresh and announces immediately (no debounce or throttle).
    /// Use only when the announcement is critical and should not be grouped.
    /// </summary>
    public static void TriggerRefreshAndAnnounceImmediate()
    {
        if (AccessibilityMgr.GetFocusedUI() != null) return;

        // Cancel any pending announcement
        if (_announceCoroutine != null)
        {
            Plugin.Instance.StopCoroutine(_announceCoroutine);
            _announceCoroutine = null;
        }

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        if (screen != null)
        {
            screen.RefreshNavigator();
            screen.AnnounceStateImmediate();
            _lastAnnounceTime = UnityEngine.Time.time;
        }
    }

    public static ERunState GetCurrentRunState()
    {
        try
        {
            var currentState = Data.CurrentState;
            if (currentState != null)
            {
                var stateName = currentState.StateName;
                // Update last valid state when we have real data
                _lastValidState = stateName;
                _hasValidState = true;
                return stateName;
            }

            // Data.CurrentState is null - use last valid state if we have one
            if (_hasValidState)
            {
                Plugin.Logger.LogInfo($"GetCurrentRunState: Data.CurrentState null, using last valid: {_lastValidState}");
                return _lastValidState;
            }

            // No valid state yet, return fallback
            return ERunState.Choice;
        }
        catch
        {
            // On error, use last valid state if available
            if (_hasValidState)
            {
                return _lastValidState;
            }
            return ERunState.Choice;
        }
    }

    public static string GetStateDescription(ERunState state)
    {
        return state switch
        {
            ERunState.Choice => "Shop",
            ERunState.Encounter => "Encounters",
            ERunState.Combat => "Combat",
            ERunState.PVPCombat => "PvP Combat",
            ERunState.Loot => "Loot",
            ERunState.LevelUp => "Level up",
            ERunState.Pedestal => "Upgrade",
            ERunState.EndRunVictory => "Victory",
            ERunState.EndRunDefeat => "Defeat",
            ERunState.NewRun => "Starting run",
            ERunState.Shutdown => "Game ending",
            _ => state.ToString()
        };
    }

    #endregion
}
