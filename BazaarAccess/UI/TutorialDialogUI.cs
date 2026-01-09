using System;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para diálogos de tutorial (SequenceDialogController, FullScreenPopupDialogController).
/// Permite presionar Enter para continuar.
/// </summary>
public class TutorialDialogUI : IAccessibleUI
{
    private readonly Transform _root;
    private readonly string _text;
    private readonly Action _onContinue;
    private readonly Action _onNext;
    private readonly Action _onPrevious;
    private readonly bool _hasNavigation;
    private bool _isValid = true;
    private int _selectedOption = 0; // 0 = Continue, 1 = Previous (si está disponible)

    public string UIName => "Tutorial";

    public TutorialDialogUI(Transform root, string text, Action onContinue, Action onNext = null, Action onPrevious = null)
    {
        _root = root;
        _text = TextHelper.CleanText(text);
        _onContinue = onContinue;
        _onNext = onNext;
        _onPrevious = onPrevious;
        _hasNavigation = onNext != null || onPrevious != null;

        // Añadir mensaje al buffer
        if (!string.IsNullOrEmpty(_text))
        {
            MessageBuffer.Add(_text);
        }
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Confirm:
                if (_hasNavigation && _selectedOption == 1 && _onPrevious != null)
                {
                    _onPrevious();
                }
                else
                {
                    Continue();
                }
                break;

            case AccessibleKey.Right:
            case AccessibleKey.Down:
                if (_hasNavigation && _onNext != null)
                {
                    _onNext();
                    TolkWrapper.Speak("Next");
                }
                else
                {
                    Continue();
                }
                break;

            case AccessibleKey.Left:
            case AccessibleKey.Up:
                if (_hasNavigation)
                {
                    if (_onPrevious != null)
                    {
                        _selectedOption = 1;
                        TolkWrapper.Speak("Previous, 2 of 2");
                    }
                }
                break;

            case AccessibleKey.Back:
                // Releer el mensaje
                if (!string.IsNullOrEmpty(_text))
                {
                    TolkWrapper.Speak(_text);
                }
                break;
        }
    }

    private void Continue()
    {
        try
        {
            _onContinue?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"TutorialDialogUI.Continue error: {ex.Message}");
        }
    }

    public string GetHelp()
    {
        if (_hasNavigation)
        {
            return "Enter or Right: Next. Left: Previous. Escape: Repeat message.";
        }
        return "Enter: Continue. Escape: Repeat message.";
    }

    public void OnFocus()
    {
        if (!string.IsNullOrEmpty(_text))
        {
            TolkWrapper.Speak(_text);
        }

        if (_hasNavigation && _onNext != null)
        {
            TolkWrapper.Speak("Press Enter or Right arrow to continue");
        }
        else
        {
            TolkWrapper.Speak("Press Enter to continue");
        }
    }

    public bool IsValid()
    {
        if (!_isValid) return false;
        try
        {
            return _root != null && _root.gameObject.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    public void Invalidate()
    {
        _isValid = false;
    }
}
