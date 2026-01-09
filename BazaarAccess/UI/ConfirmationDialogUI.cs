using System.Collections.Generic;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para diálogos de confirmación genéricos (rendirse, cerrar, etc.).
/// </summary>
public class ConfirmationDialogUI : IAccessibleUI
{
    public string UIName => "Confirmation";

    private readonly Transform _root;
    private readonly string _message;
    private readonly List<Button> _buttons = new List<Button>();
    private int _currentIndex = 0;

    public ConfirmationDialogUI(Transform root, string message)
    {
        _root = root;
        _message = message;
        FindButtons();
    }

    private void FindButtons()
    {
        _buttons.Clear();

        if (_root == null) return;

        var allButtons = _root.GetComponentsInChildren<Button>(true);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                _buttons.Add(btn);
            }
        }

        Plugin.Logger.LogInfo($"ConfirmationDialogUI: Found {_buttons.Count} buttons");
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Left:
            case AccessibleKey.Up:
                NavigatePrevious();
                break;

            case AccessibleKey.Right:
            case AccessibleKey.Down:
                NavigateNext();
                break;

            case AccessibleKey.Confirm:
                ClickCurrentButton();
                break;

            case AccessibleKey.Back:
                // Intentar hacer click en Cancel/No/Close
                ClickCancelButton();
                break;
        }
    }

    private void NavigateNext()
    {
        if (_buttons.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _buttons.Count;
        AnnounceCurrentButton();
    }

    private void NavigatePrevious()
    {
        if (_buttons.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _buttons.Count) % _buttons.Count;
        AnnounceCurrentButton();
    }

    private void AnnounceCurrentButton()
    {
        if (_currentIndex < 0 || _currentIndex >= _buttons.Count) return;

        var btn = _buttons[_currentIndex];
        string text = GetButtonText(btn);
        TolkWrapper.Speak($"{text}, {_currentIndex + 1} of {_buttons.Count}");
    }

    private string GetButtonText(Button btn)
    {
        // Buscar TMP_Text en el botón
        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
        {
            return tmp.text.Trim();
        }

        // Buscar Text legacy
        var text = btn.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (text != null && !string.IsNullOrWhiteSpace(text.text))
        {
            return text.text.Trim();
        }

        // Usar nombre del GameObject
        return btn.gameObject.name;
    }

    private void ClickCurrentButton()
    {
        if (_currentIndex < 0 || _currentIndex >= _buttons.Count)
        {
            TolkWrapper.Speak("No button selected");
            return;
        }

        var btn = _buttons[_currentIndex];
        string text = GetButtonText(btn);
        Plugin.Logger.LogInfo($"ConfirmationDialogUI: Clicking '{text}'");
        btn.onClick?.Invoke();
    }

    private void ClickCancelButton()
    {
        // Buscar botón de cancelar/no/cerrar
        foreach (var btn in _buttons)
        {
            string text = GetButtonText(btn).ToLowerInvariant();
            if (text.Contains("cancel") || text.Contains("no") || text.Contains("close") ||
                text.Contains("back") || text.Contains("exit"))
            {
                Plugin.Logger.LogInfo($"ConfirmationDialogUI: Clicking cancel button '{text}'");
                btn.onClick?.Invoke();
                return;
            }
        }

        // Si no hay botón de cancelar, hacer click en el último (suele ser cancelar)
        if (_buttons.Count > 0)
        {
            var lastBtn = _buttons[_buttons.Count - 1];
            Plugin.Logger.LogInfo($"ConfirmationDialogUI: Clicking last button as cancel");
            lastBtn.onClick?.Invoke();
        }
    }

    public string GetHelp()
    {
        return "Left/Right: Navigate buttons. Enter: Select. Escape: Cancel.";
    }

    public void OnFocus()
    {
        // Leer el mensaje y los botones disponibles
        TolkWrapper.Speak(_message);

        if (_buttons.Count > 0)
        {
            // Seleccionar el primer botón y anunciarlo
            Plugin.Instance.StartCoroutine(DelayedAnnounce());
        }
        else
        {
            TolkWrapper.Speak("No buttons found");
        }
    }

    private System.Collections.IEnumerator DelayedAnnounce()
    {
        yield return new WaitForSeconds(0.3f);
        AnnounceCurrentButton();
    }

    public bool IsValid()
    {
        return _root != null && _root.gameObject.activeInHierarchy;
    }
}
