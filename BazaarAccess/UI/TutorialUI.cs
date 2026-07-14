using System;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using BazaarAccess.Patches;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para los diálogos del tutorial (FTUE).
/// Permite leer el texto y navegar/continuar con el teclado.
/// </summary>
public class TutorialUI : IAccessibleUI
{
    public string UIName => "Tutorial";

    private readonly Transform _root;
    private readonly string _text;
    private readonly bool _isFullScreen;
    private readonly MonoBehaviour _nextButton;
    private readonly MonoBehaviour _prevButton;

    private int _currentOption = 0; // 0 = Continue/Next, 1 = Previous
    private bool _hasPrevButton = false;

    public TutorialUI(Transform root, string text, bool isFullScreen,
        MonoBehaviour nextButton = null, MonoBehaviour prevButton = null)
    {
        _root = root;
        _text = text;
        _isFullScreen = isFullScreen;
        _nextButton = nextButton;
        _prevButton = prevButton;
        _hasPrevButton = prevButton != null && IsButtonActive(prevButton);
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Confirm:
                // Solo Enter continúa el tutorial (Space puede abrir el stash del juego)
                if (_isFullScreen && _hasPrevButton && _currentOption == 1)
                {
                    ClickButton(_prevButton);
                    TolkWrapper.Speak(Loc.T("ui.previous"));
                }
                else
                {
                    ContinueTutorial();
                }
                break;

            // No usar Escape/Back - abre el menú de opciones del juego
            // Usar Period/Comma para releer mensajes del buffer

            case AccessibleKey.Help:
                if (_isFullScreen && _hasPrevButton)
                {
                    TolkWrapper.Speak(Loc.T("ui.tutorial2.help_full_nav"));
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("ui.tutorial2.help_full_basic"));
                }
                break;

            // Leer mensajes del buffer con . y ,
            case AccessibleKey.NextMessage:
                MessageBuffer.ReadNewest();
                break;

            case AccessibleKey.PrevMessage:
                MessageBuffer.ReadPrevious();
                break;

            // IMPORTANTE: Pasar todas las demás teclas al GameplayScreen
            // El tutorial del juego NO bloquea la interacción, así que tampoco debemos bloquearla
            default:
                PassToGameplayScreen(key);
                break;
        }
    }

    /// <summary>
    /// Pasa una tecla al GameplayScreen para que el usuario pueda navegar durante el tutorial.
    /// </summary>
    private void PassToGameplayScreen(AccessibleKey key)
    {
        var gameplayScreen = GameplayPatch.GetGameplayScreen();
        if (gameplayScreen != null && gameplayScreen.IsValid())
        {
            gameplayScreen.HandleInput(key);
        }
    }

    public string GetHelp()
    {
        if (_isFullScreen && _hasPrevButton)
        {
            return Loc.T("ui.tutorial2.help_short_nav");
        }
        return Loc.T("ui.tutorial2.help_short_basic");
    }

    public void OnFocus()
    {
        // Añadir el texto del tutorial al buffer para poder releerlo con . y ,
        MessageBuffer.Add(Loc.T("ui.tutorial2.buffer_prefix", _text));

        // Anunciar el texto del tutorial
        string announcement = _text;

        if (_isFullScreen && _hasPrevButton)
        {
            announcement += ". " + Loc.T("ui.tutorial2.nav_hint");
        }
        else
        {
            announcement += ". " + Loc.T("ui.continue_prompt");
        }

        TolkWrapper.Speak(announcement);
    }

    public bool IsValid()
    {
        return _root != null;
    }

    /// <summary>
    /// Continúa al siguiente paso del tutorial.
    /// </summary>
    private void ContinueTutorial()
    {
        try
        {
            // Intentar hacer click en el botón Next si existe
            if (_nextButton != null)
            {
                ClickButton(_nextButton);
                return;
            }

            // Si no hay botón, intentar completar el NodeSequence directamente
            // Buscar el NodeSequenceComponent y llamar a Completed()
            var nodeComponent = FindNodeSequenceComponent();
            if (nodeComponent != null)
            {
                var nodeSequenceInterface = AccessTools.TypeByName("TheBazaar.SequenceFramework.INodeSequence");
                if (nodeSequenceInterface != null)
                {
                    var completedMethod = nodeSequenceInterface.GetMethod("Completed");
                    completedMethod?.Invoke(nodeComponent, null);
                    Plugin.Logger.LogInfo("Tutorial: Called Completed() on NodeSequence");
                }
            }
            else
            {
                // Fallback: simular click en cualquier lugar
                Plugin.Logger.LogInfo("Tutorial: No NodeSequence found, attempting click simulation");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ContinueTutorial error: {ex.Message}");
        }
    }

    /// <summary>
    /// Busca el NodeSequenceComponent en el árbol de objetos.
    /// </summary>
    private object FindNodeSequenceComponent()
    {
        try
        {
            // El BasePointerDialogController tiene un campo _nodeSequenceComponent
            var baseType = AccessTools.TypeByName("TheBazaar.BasePointerDialogController");
            if (baseType == null) return null;

            var field = AccessTools.Field(baseType, "_nodeSequenceComponent");
            if (field == null) return null;

            // Obtener el componente del root
            var dialogController = _root.GetComponent(baseType);
            if (dialogController != null)
            {
                return field.GetValue(dialogController);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"FindNodeSequenceComponent error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Hace click en un botón BazaarButtonController.
    /// </summary>
    private void ClickButton(MonoBehaviour button)
    {
        if (button == null) return;

        try
        {
            // Intentar invocar onClick
            var buttonType = button.GetType();
            var onClickField = AccessTools.Field(buttonType, "onClick");
            if (onClickField != null)
            {
                var onClick = onClickField.GetValue(button);
                var invokeMethod = onClick?.GetType().GetMethod("Invoke", Type.EmptyTypes);
                invokeMethod?.Invoke(onClick, null);
                return;
            }

            // Fallback: buscar un Button de Unity
            var unityButton = button.GetComponent<Button>();
            if (unityButton != null)
            {
                unityButton.onClick.Invoke();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ClickButton error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifica si un botón está activo y visible.
    /// </summary>
    private bool IsButtonActive(MonoBehaviour button)
    {
        if (button == null) return false;

        try
        {
            return button.gameObject.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }
}
