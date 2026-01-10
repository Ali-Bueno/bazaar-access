using System.Collections;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using HarmonyLib;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Detecta entrada al gameplay.
/// </summary>
[HarmonyPatch(typeof(BoardManager), "OnAwake")]
public static class GameplayPatch
{
    private static GameplayScreen _gameplayScreen;
    private static bool _stateSubscribed = false;

    [HarmonyPostfix]
    public static void Postfix(BoardManager __instance)
    {
        Plugin.Logger.LogInfo("BoardManager.OnAwake - Entering gameplay");

        // Suscribir a cambios de estado (solo una vez)
        if (!_stateSubscribed)
        {
            StateChangePatch.Subscribe();
            _stateSubscribed = true;
        }

        // Crear la pantalla de gameplay
        _gameplayScreen = new GameplayScreen();
        AccessibilityMgr.SetScreen(_gameplayScreen);

        // Iniciar refresh con delay para dar tiempo al juego a cargar
        Plugin.Instance.StartCoroutine(DelayedInitialize());
    }

    private static IEnumerator DelayedInitialize()
    {
        // Esperar inicial para que el juego arranque
        yield return new WaitForSeconds(1.5f);
        if (_gameplayScreen == null) yield break;

        // Primer refresh
        _gameplayScreen.RefreshNavigator();
        Plugin.Logger.LogInfo($"DelayedInitialize: First refresh, hasContent={_gameplayScreen.HasContent()}");

        // Si hay contenido inmediatamente, anunciar
        if (_gameplayScreen.HasContent())
        {
            _gameplayScreen.ForceAnnounceState();
            Plugin.Logger.LogInfo("DelayedInitialize: Content found on first check");
            yield break;
        }

        // Esperar un poco más y hacer más refreshes
        yield return new WaitForSeconds(0.5f);
        if (_gameplayScreen == null) yield break;
        _gameplayScreen.RefreshNavigator();

        yield return new WaitForSeconds(0.5f);
        if (_gameplayScreen == null) yield break;
        _gameplayScreen.RefreshNavigator();

        yield return new WaitForSeconds(0.5f);
        if (_gameplayScreen == null) yield break;
        _gameplayScreen.RefreshNavigator();

        // Anunciar estado final - siempre anunciar después de esperar
        Plugin.Logger.LogInfo($"DelayedInitialize: Final check, hasContent={_gameplayScreen.HasContent()}");
        _gameplayScreen.ForceAnnounceState();
        Plugin.Logger.LogInfo("DelayedInitialize: Announced state");
    }

    /// <summary>
    /// Obtiene la pantalla de gameplay actual.
    /// </summary>
    public static GameplayScreen GetGameplayScreen() => _gameplayScreen;
}

// NOTA: El evento StorageToggled se maneja en StateChangePatch.cs via Events.StorageToggled
// No usar un patch aquí para evitar duplicados

/// <summary>
/// Detecta cuando entramos en ReplayState (post-combat).
/// </summary>
[HarmonyPatch]
public static class ReplayStateEnterPatch
{
    private static System.Reflection.MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType != null)
            {
                _targetMethod = replayStateType.GetMethod("OnEnter",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("ReplayStateEnterPatch: Found ReplayState.OnEnter");
                    return true;
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ReplayStateEnterPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static System.Reflection.MethodBase TargetMethod() => _targetMethod;

    [HarmonyPostfix]
    public static void Postfix()
    {
        Plugin.Logger.LogInfo("ReplayStateEnterPatch: Entered ReplayState!");
        Plugin.Instance.StartCoroutine(DelayedReplayStateEnter());
    }

    private static IEnumerator DelayedReplayStateEnter()
    {
        yield return new WaitForSeconds(0.3f);

        var screen = GameplayPatch.GetGameplayScreen();
        if (screen != null)
        {
            // OnReplayStateChanged ya anuncia el mensaje, no duplicar
            screen.OnReplayStateChanged(true);
        }
    }
}

/// <summary>
/// Detecta cuando el usuario presiona Exit en ReplayState.
/// </summary>
[HarmonyPatch]
public static class ReplayStateExitPatch
{
    private static System.Reflection.MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType != null)
            {
                _targetMethod = replayStateType.GetMethod("Exit",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("ReplayStateExitPatch: Found ReplayState.Exit");
                    return true;
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ReplayStateExitPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static System.Reflection.MethodBase TargetMethod() => _targetMethod;

    [HarmonyPostfix]
    public static void Postfix()
    {
        Plugin.Logger.LogInfo("ReplayStateExitPatch: Exit called - continuing from combat");

        var screen = GameplayPatch.GetGameplayScreen();
        if (screen != null)
        {
            screen.OnReplayStateChanged(false);
        }
    }
}
