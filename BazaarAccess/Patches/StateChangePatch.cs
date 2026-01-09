using System;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Runs;
using BazaarGameShared.Infra.Messages.GameSimEvents;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Escucha cambios de estado del gameplay en tiempo real.
/// Usa los eventos nativos del juego para mayor confiabilidad.
/// </summary>
public static class StateChangePatch
{
    private static ERunState _lastState = ERunState.Choice;
    private static bool _initialized = false;
    private static bool _inCombat = false;
    private static Type _eventsType;

    public static bool IsInCombat => _inCombat;

    /// <summary>
    /// Inicializa la suscripción a eventos.
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
                Plugin.Logger.LogError("StateChangePatch: No se encontró TheBazaar.Events");
                return;
            }

            // === Eventos de cambio de estado ===
            SubscribeToEvent("StateChanged", typeof(Action<StateChangedEvent>),
                (Action<StateChangedEvent>)OnStateChanged);

            // === Eventos de transición/animación completada ===
            SubscribeToEventNoParam("BoardTransitionFinished", OnBoardTransitionFinished);
            SubscribeToEventNoParam("NewDayTransitionAnimationFinished", OnNewDayTransitionFinished);

            // === Eventos de combate ===
            SubscribeToEventNoParam("CombatStarted", OnCombatStarted);
            SubscribeToEventNoParam("CombatEnded", OnCombatEnded);

            // === Eventos de compra/venta ===
            SubscribeToEvent("CardPurchasedSimEvent", typeof(Action<GameSimEventCardPurchased>),
                (Action<GameSimEventCardPurchased>)OnCardPurchased);
            SubscribeToEvent("CardSoldSimEvent", typeof(Action<GameSimEventCardSold>),
                (Action<GameSimEventCardSold>)OnCardSold);

            // === Eventos del tablero ===
            SubscribeToEventNoParam("OnBoardChanged", OnBoardChanged);

            // === Eventos de BoardManager (cartas reveladas) ===
            SubscribeToBoardManagerEvent("ItemCardsRevealed", OnItemCardsRevealed);
            SubscribeToBoardManagerEvent("SkillCardsRevealed", OnSkillCardsRevealed);

            // === Eventos de AppState ===
            SubscribeToAppStateEvent("StateExited", OnStateExited);
            SubscribeToAppStateEvent("EncounterEntered", OnEncounterEntered);

            Plugin.Logger.LogInfo("StateChangePatch: Suscrito a eventos del juego");
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
            var eventField = _eventsType.GetField(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventField == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: No se encontró Events.{eventName}");
                return;
            }

            var eventObj = eventField.GetValue(null);
            if (eventObj == null)
            {
                Plugin.Logger.LogWarning($"StateChangePatch: Events.{eventName} es null");
                return;
            }

            var addMethod = eventObj.GetType().GetMethod("AddListener",
                new Type[] { handlerType, typeof(MonoBehaviour) });

            if (addMethod != null)
            {
                addMethod.Invoke(eventObj, new object[] { handler, null });
                Plugin.Logger.LogInfo($"StateChangePatch: Suscrito a Events.{eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error suscribiendo a {eventName}: {ex.Message}");
        }
    }

    private static void SubscribeToEventNoParam(string eventName, Action handler)
    {
        try
        {
            var eventField = _eventsType.GetField(eventName, BindingFlags.Public | BindingFlags.Static);
            if (eventField == null) return;

            var eventObj = eventField.GetValue(null);
            if (eventObj == null) return;

            var addMethod = eventObj.GetType().GetMethod("AddListener",
                new Type[] { typeof(Action), typeof(MonoBehaviour) });

            if (addMethod != null)
            {
                addMethod.Invoke(eventObj, new object[] { handler, null });
                Plugin.Logger.LogInfo($"StateChangePatch: Suscrito a Events.{eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error suscribiendo a {eventName}: {ex.Message}");
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
                Plugin.Logger.LogInfo($"StateChangePatch: Suscrito a BoardManager.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: No se encontró BoardManager.{eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error suscribiendo a BoardManager.{eventName}: {ex.Message}");
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
                Plugin.Logger.LogInfo($"StateChangePatch: Suscrito a AppState.{eventName}");
            }
            else
            {
                Plugin.Logger.LogWarning($"StateChangePatch: No se encontró AppState.{eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"StateChangePatch: Error suscribiendo a AppState.{eventName}: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Evento principal de cambio de estado.
    /// </summary>
    private static void OnStateChanged(StateChangedEvent evt)
    {
        try
        {
            var newState = GetCurrentRunState();
            Plugin.Logger.LogInfo($"OnStateChanged: {_lastState} -> {newState}");

            bool stateActuallyChanged = newState != _lastState;
            _lastState = newState;

            if (AccessibilityMgr.GetFocusedUI() == null)
            {
                var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
                screen?.OnStateChanged(newState, stateActuallyChanged);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"OnStateChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cuando termina una transición del tablero (animaciones completas).
    /// </summary>
    private static void OnBoardTransitionFinished()
    {
        Plugin.Logger.LogInfo("BoardTransitionFinished - UI ready");
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Cuando termina la animación de nuevo día.
    /// </summary>
    private static void OnNewDayTransitionFinished()
    {
        Plugin.Logger.LogInfo("NewDayTransitionAnimationFinished - UI ready");
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Cuando las cartas de items son reveladas (después de animación).
    /// </summary>
    private static void OnItemCardsRevealed()
    {
        Plugin.Logger.LogInfo("ItemCardsRevealed - Cards ready");
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Cuando las cartas de skills son reveladas.
    /// </summary>
    private static void OnSkillCardsRevealed()
    {
        Plugin.Logger.LogInfo("SkillCardsRevealed - Skills ready");
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Cuando se sale de un estado (antes de entrar al siguiente).
    /// </summary>
    private static void OnStateExited()
    {
        Plugin.Logger.LogInfo("AppState.StateExited");
        // No anunciar aquí, esperar a que el nuevo estado esté listo
    }

    /// <summary>
    /// Cuando se entra en un encuentro.
    /// </summary>
    private static void OnEncounterEntered()
    {
        Plugin.Logger.LogInfo("AppState.EncounterEntered");
        TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Cuando empieza el combate.
    /// </summary>
    private static void OnCombatStarted()
    {
        Plugin.Logger.LogInfo("CombatStarted");
        _inCombat = true;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(true);
    }

    /// <summary>
    /// Cuando termina el combate.
    /// </summary>
    private static void OnCombatEnded()
    {
        Plugin.Logger.LogInfo("CombatEnded");
        _inCombat = false;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.OnCombatStateChanged(false);
    }

    private static void OnCardPurchased(GameSimEventCardPurchased evt)
    {
        Plugin.Logger.LogInfo($"Card purchased: {evt.InstanceId}");
        TriggerRefresh();
    }

    private static void OnCardSold(GameSimEventCardSold evt)
    {
        Plugin.Logger.LogInfo($"Card sold: {evt.InstanceId}");
        TriggerRefresh();
    }

    private static void OnBoardChanged()
    {
        Plugin.Logger.LogInfo("Board changed");
        TriggerRefresh();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Dispara un refresh de la pantalla de gameplay (sin anunciar).
    /// </summary>
    public static void TriggerRefresh()
    {
        if (AccessibilityMgr.GetFocusedUI() != null) return;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        screen?.RefreshNavigator();
    }

    /// <summary>
    /// Dispara un refresh y anuncia el estado actual.
    /// </summary>
    public static void TriggerRefreshAndAnnounce()
    {
        if (AccessibilityMgr.GetFocusedUI() != null) return;

        var screen = AccessibilityMgr.GetCurrentScreen() as GameplayScreen;
        if (screen != null)
        {
            screen.RefreshNavigator();
            screen.ForceAnnounceState();
        }
    }

    public static ERunState GetCurrentRunState()
    {
        try
        {
            return Data.CurrentState?.StateName ?? ERunState.Choice;
        }
        catch
        {
            return ERunState.Choice;
        }
    }

    public static string GetStateDescription(ERunState state)
    {
        return state switch
        {
            ERunState.Choice => "Shop",
            ERunState.Encounter => "Choose encounter",
            ERunState.Combat => "Combat",
            ERunState.PVPCombat => "PvP Combat",
            ERunState.Loot => "Loot",
            ERunState.LevelUp => "Level up",
            ERunState.Pedestal => "Upgrade station",
            ERunState.EndRunVictory => "Victory!",
            ERunState.EndRunDefeat => "Defeat",
            ERunState.NewRun => "Starting run",
            ERunState.Shutdown => "Game ending",
            _ => state.ToString()
        };
    }

    #endregion
}
