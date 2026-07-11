using System;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;

namespace BazaarAccess.Gameplay;

internal enum ShopItemActionOption
{
    Details,
    Buy,
    Cancel
}

internal sealed class ShopItemMenuHandler
{
    private readonly Action<ItemCard> _onBuy;
    private readonly Action<Card> _onShowDetails;

    private bool _isActive;
    private ItemCard _itemCard;
    private int _currentIndex;

    private static readonly ShopItemActionOption[] _options =
    {
        ShopItemActionOption.Details,
        ShopItemActionOption.Buy,
        ShopItemActionOption.Cancel
    };

    public bool IsActive => _isActive;

    public ShopItemMenuHandler(Action<ItemCard> onBuy, Action<Card> onShowDetails)
    {
        _onBuy = onBuy;
        _onShowDetails = onShowDetails;
    }

    public void Enter(ItemCard itemCard)
    {
        if (itemCard == null)
            return;

        _itemCard = itemCard;
        _currentIndex = 0;
        _isActive = true;

        string name = ItemReader.GetCardName(itemCard);
        TolkWrapper.Speak($"{name}. {GetOptionText(_options[0])}. {_options.Length} actions. Backspace to cancel.");
    }

    public void Exit(bool announce = true)
    {
        if (!_isActive)
            return;

        _isActive = false;
        _itemCard = null;

        if (announce)
            TolkWrapper.Speak("Exited");
    }

    public void HandleInput(AccessibleKey key)
    {
        if (!_isActive || _itemCard == null)
        {
            Exit(announce: false);
            return;
        }

        switch (key)
        {
            case AccessibleKey.Up:
                Navigate(-1);
                break;

            case AccessibleKey.Down:
                Navigate(1);
                break;

            case AccessibleKey.Confirm:
                ExecuteCurrentOption();
                break;

            case AccessibleKey.Back:
                Exit();
                break;
        }
    }

    private void Navigate(int direction)
    {
        _currentIndex = (_currentIndex + direction + _options.Length) % _options.Length;
        TolkWrapper.Speak($"{GetOptionText(_options[_currentIndex])}, {_currentIndex + 1} of {_options.Length}");
    }

    private void ExecuteCurrentOption()
    {
        var option = _options[_currentIndex];
        var itemCard = _itemCard;

        _isActive = false;
        _itemCard = null;

        switch (option)
        {
            case ShopItemActionOption.Details:
                _onShowDetails?.Invoke(itemCard);
                break;

            case ShopItemActionOption.Buy:
                _onBuy?.Invoke(itemCard);
                break;

            case ShopItemActionOption.Cancel:
                TolkWrapper.Speak("Exited");
                break;
        }
    }

    private string GetOptionText(ShopItemActionOption option)
    {
        switch (option)
        {
            case ShopItemActionOption.Details:
                return "Details";

            case ShopItemActionOption.Buy:
                int price = ItemReader.GetBuyPrice(_itemCard);
                return price > 0 ? $"Buy for {price} gold" : "Buy";

            case ShopItemActionOption.Cancel:
                return "Cancel";

            default:
                return option.ToString();
        }
    }
}
