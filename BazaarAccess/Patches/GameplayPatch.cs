using System.Collections;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using HarmonyLib;
using TheBazaar;
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
