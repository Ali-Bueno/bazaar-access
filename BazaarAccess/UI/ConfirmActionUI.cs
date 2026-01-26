using System;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;

namespace BazaarAccess.UI;

/// <summary>
/// Tipo de acción a confirmar.
/// </summary>
public enum ConfirmActionType
{
    Buy,
    Sell,
    Move,
    Select,
    Upgrade
}

/// <summary>
/// UI de confirmación para acciones de compra/venta/mover/upgrade.
/// </summary>
public class ConfirmActionUI : IAccessibleUI
{
    public string UIName { get; }

    private readonly string _itemName;
    private readonly int _price;
    private readonly string _customMessage;
    private readonly ConfirmActionType _actionType;
    private readonly Action _onConfirm;
    private readonly Action _onCancel;

    private int _selectedOption = 0; // 0 = Confirm, 1 = Cancel
    private bool _isValid = true;

    public ConfirmActionUI(ConfirmActionType actionType, string itemName, int price, Action onConfirm, Action onCancel = null)
        : this(actionType, itemName, price, null, onConfirm, onCancel)
    {
    }

    public ConfirmActionUI(ConfirmActionType actionType, string itemName, int price, string customMessage, Action onConfirm, Action onCancel = null)
    {
        _actionType = actionType;
        _itemName = itemName;
        _price = price;
        _customMessage = customMessage;
        _onConfirm = onConfirm;
        _onCancel = onCancel;

        UIName = actionType switch
        {
            ConfirmActionType.Buy => "Confirm Purchase",
            ConfirmActionType.Sell => "Confirm Sale",
            ConfirmActionType.Move => "Confirm Move",
            ConfirmActionType.Select => "Confirm Selection",
            ConfirmActionType.Upgrade => "Confirm Upgrade",
            _ => "Confirm"
        };
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Left:
            case AccessibleKey.Right:
                // Cambiar entre Confirm y Cancel
                _selectedOption = (_selectedOption + 1) % 2;
                AnnounceCurrentOption();
                break;

            case AccessibleKey.Confirm:
                if (_selectedOption == 0)
                {
                    // Confirmar
                    _onConfirm?.Invoke();
                }
                else
                {
                    // Cancelar
                    _onCancel?.Invoke();
                }
                Close();
                break;

            case AccessibleKey.Back:
                _onCancel?.Invoke();
                Close();
                break;
        }
    }

    public string GetHelp()
    {
        return "Left/Right: switch option. Enter: confirm. Escape: cancel.";
    }

    public void OnFocus()
    {
        // Use custom message if provided
        string question;
        if (!string.IsNullOrEmpty(_customMessage))
        {
            question = _customMessage;
        }
        else
        {
            question = _actionType switch
            {
                ConfirmActionType.Buy => $"Buy {_itemName} for {_price} gold?",
                ConfirmActionType.Sell => $"Sell {_itemName} for {_price} gold?",
                ConfirmActionType.Move => $"Move {_itemName}?",
                ConfirmActionType.Select => $"Select {_itemName}?",
                ConfirmActionType.Upgrade => $"Upgrade {_itemName}?",
                _ => $"Confirm action for {_itemName}?"
            };
        }

        TolkWrapper.Speak(question);
        AnnounceCurrentOption();
    }

    public bool IsValid() => _isValid;

    private void AnnounceCurrentOption()
    {
        string option = _selectedOption == 0 ? "Confirm" : "Cancel";
        int position = _selectedOption + 1;
        TolkWrapper.Speak($"{option}, {position} of 2");
    }

    private void Close()
    {
        _isValid = false;
        AccessibilityMgr.PopUI();
    }
}
