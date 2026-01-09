using System.Collections.Generic;
using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Interface para pantallas accesibles (vistas principales del juego).
/// </summary>
public interface IAccessibleScreen
{
    string ScreenName { get; }
    void HandleInput(AccessibleKey key);
    string GetHelp();
    void OnFocus();
    bool IsValid();
}

/// <summary>
/// Interface para UIs accesibles (popups/diálogos).
/// </summary>
public interface IAccessibleUI
{
    string UIName { get; }
    void HandleInput(AccessibleKey key);
    string GetHelp();
    void OnFocus();
    bool IsValid();
}

/// <summary>
/// Gestor central de accesibilidad.
/// Maneja el focus entre pantallas y UIs, y distribuye el input.
/// </summary>
public static class AccessibilityMgr
{
    // Stack de UIs (popups/diálogos)
    private static readonly Stack<IAccessibleUI> _uiStack = new Stack<IAccessibleUI>();

    // Pantalla actual (vista base)
    private static IAccessibleScreen _currentScreen;

    // --- Gestión de pantallas ---

    /// <summary>
    /// Establece la pantalla actual. Limpia el stack de UIs.
    /// </summary>
    public static void SetScreen(IAccessibleScreen screen)
    {
        // Limpiar UIs anteriores
        _uiStack.Clear();

        _currentScreen = screen;

        if (_currentScreen != null)
        {
            Plugin.Logger.LogInfo($"Screen: {_currentScreen.ScreenName}");
            _currentScreen.OnFocus();
        }
    }

    /// <summary>
    /// Obtiene la pantalla actual.
    /// </summary>
    public static IAccessibleScreen GetCurrentScreen() => _currentScreen;

    // --- Gestión de UIs ---

    /// <summary>
    /// Muestra una UI (push al stack).
    /// </summary>
    public static void ShowUI(IAccessibleUI ui)
    {
        if (ui == null) return;

        _uiStack.Push(ui);
        Plugin.Logger.LogInfo($"UI Push: {ui.UIName} (stack: {_uiStack.Count})");
        ui.OnFocus();
    }

    /// <summary>
    /// Oculta una UI (pop del stack).
    /// </summary>
    public static void HideUI(IAccessibleUI ui)
    {
        if (_uiStack.Count == 0) return;

        // Buscar y remover la UI del stack
        var tempStack = new Stack<IAccessibleUI>();
        bool found = false;

        while (_uiStack.Count > 0)
        {
            var top = _uiStack.Pop();
            if (top == ui)
            {
                found = true;
                break;
            }
            tempStack.Push(top);
        }

        // Restaurar las UIs que no eran la buscada
        while (tempStack.Count > 0)
        {
            _uiStack.Push(tempStack.Pop());
        }

        if (found)
        {
            Plugin.Logger.LogInfo($"UI Pop: {ui.UIName} (stack: {_uiStack.Count})");

            // Dar focus a la siguiente UI o a la pantalla
            if (_uiStack.Count > 0)
            {
                _uiStack.Peek().OnFocus();
            }
            else if (_currentScreen != null)
            {
                _currentScreen.OnFocus();
            }
        }
    }

    /// <summary>
    /// Cierra la UI superior del stack.
    /// </summary>
    public static void PopUI()
    {
        if (_uiStack.Count == 0)
        {
            Plugin.Logger.LogInfo("PopUI: stack vacío");
            return;
        }

        var ui = _uiStack.Pop();
        Plugin.Logger.LogInfo($"UI Pop: {ui.UIName} (stack: {_uiStack.Count})");

        // Dar focus a la siguiente UI o a la pantalla
        if (_uiStack.Count > 0)
        {
            _uiStack.Peek().OnFocus();
        }
        else if (_currentScreen != null)
        {
            _currentScreen.OnFocus();
        }
    }

    /// <summary>
    /// Obtiene la UI con focus actual (top del stack).
    /// </summary>
    public static IAccessibleUI GetFocusedUI()
    {
        return _uiStack.Count > 0 ? _uiStack.Peek() : null;
    }

    // --- Input ---

    /// <summary>
    /// Maneja el input del teclado. Distribuye al componente con focus.
    /// </summary>
    public static void HandleInput(AccessibleKey key)
    {
        if (key == AccessibleKey.None) return;

        // Tecla de ayuda global
        if (key == AccessibleKey.Help)
        {
            ReadHelp();
            return;
        }

        // Prioridad: UI > Screen
        var focusedUI = GetFocusedUI();
        if (focusedUI != null)
        {
            if (focusedUI.IsValid())
            {
                focusedUI.HandleInput(key);
            }
            else
            {
                // UI inválida, removerla
                PopUI();
            }
            return;
        }

        // No hay UI, enviar a la pantalla
        if (_currentScreen != null)
        {
            if (_currentScreen.IsValid())
            {
                _currentScreen.HandleInput(key);
            }
            else
            {
                _currentScreen = null;
            }
        }
    }

    /// <summary>
    /// Lee la ayuda del componente con focus.
    /// </summary>
    private static void ReadHelp()
    {
        string help = null;

        var focusedUI = GetFocusedUI();
        if (focusedUI != null)
        {
            help = focusedUI.GetHelp();
        }
        else if (_currentScreen != null)
        {
            help = _currentScreen.GetHelp();
        }

        if (!string.IsNullOrEmpty(help))
        {
            TolkWrapper.Speak(help);
        }
    }

    /// <summary>
    /// Limpia todo el estado.
    /// </summary>
    public static void Clear()
    {
        _uiStack.Clear();
        _currentScreen = null;
    }
}
