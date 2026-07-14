using System;
using System.Collections;
using System.Collections.Generic;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using UnityEngine;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Action menu option type.
/// </summary>
public enum ActionOption
{
    Details,
    Sell,
    Upgrade,
    Enchant,
    UsePedestal,
    MoveToStash,
    MoveToBoard
}

/// <summary>
/// Handles the action mode overlay for board/stash items.
/// Manages action menu navigation (Sell, Upgrade, Enchant, Move).
/// </summary>
public class ActionMenuHandler
{
    private readonly GameplayNavigator _navigator;
    private readonly Action<Card> _onUsePedestalAction;
    private readonly Action _onRefreshAndAnnounce;
    private readonly Action<Card> _onShowDetails;

    // Action mode state
    private bool _isInActionMode = false;
    private List<ActionOption> _actionOptions = new List<ActionOption>();
    private int _actionIndex = 0;
    private Card _actionCard = null;

    /// <summary>
    /// Whether the action menu is currently active.
    /// </summary>
    public bool IsActive => _isInActionMode;

    /// <summary>
    /// The card currently being acted upon.
    /// </summary>
    public Card ActionCard => _actionCard;

    public ActionMenuHandler(
        GameplayNavigator navigator,
        Action<Card> onUsePedestalAction,
        Action onRefreshAndAnnounce,
        Action<Card> onShowDetails)
    {
        _navigator = navigator;
        _onUsePedestalAction = onUsePedestalAction;
        _onRefreshAndAnnounce = onRefreshAndAnnounce;
        _onShowDetails = onShowDetails;
    }

    /// <summary>
    /// Enters action mode for the current item.
    /// Builds a menu of available actions.
    /// </summary>
    public void Enter(Card card)
    {
        if (card == null) return;

        _actionCard = card;
        _actionOptions.Clear();
        _actionIndex = 0;

        var currentState = StateChangePatch.GetCurrentRunState();
        bool canSellState = _navigator.CanSellInCurrentState();
        bool canSellCard = ActionHelper.CanSell(card);
        bool canMove = _navigator.CanMoveInCurrentState();
        bool isInBoard = _navigator.CurrentSection == NavigationSection.Board;
        bool isInStash = _navigator.CurrentSection == NavigationSection.Stash;
        bool stashOpen = _navigator.IsStashOpen();

        if (card is ItemCard)
        {
            _actionOptions.Add(ActionOption.Details);
        }

        // Build available options
        // At pedestal, show Upgrade/Enchant first (primary action)
        if (currentState == ERunState.Pedestal)
        {
            var pedestalInfo = PedestalManager.GetCurrentPedestalInfo();
            if (pedestalInfo.Type == PedestalManager.PedestalType.Enchant ||
                pedestalInfo.Type == PedestalManager.PedestalType.EnchantRandom)
            {
                // Enchant pedestal - no tier restriction, only check if already enchanted
                var itemCardCheck = card as ItemCard;
                if (itemCardCheck == null || !itemCardCheck.Enchantment.HasValue)
                {
                    _actionOptions.Add(ActionOption.Enchant);
                }
            }
            else if (pedestalInfo.Type == PedestalManager.PedestalType.Upgrade)
            {
                // Upgrade pedestal - block only Legendary tier
                if (card.Tier != ETier.Legendary)
                {
                    _actionOptions.Add(ActionOption.Upgrade);
                }
            }
            else
            {
                // Detection failed - offer generic "use pedestal" option (game handles logic)
                Plugin.Logger.LogWarning("ActionMenuHandler: pedestal detection failed, offering generic option");
                _actionOptions.Add(ActionOption.UsePedestal);
            }
        }

        if (canSellState && canSellCard)
        {
            _actionOptions.Add(ActionOption.Sell);
        }

        if (canMove && isInBoard && stashOpen)
        {
            _actionOptions.Add(ActionOption.MoveToStash);
        }

        if (canMove && isInStash)
        {
            _actionOptions.Add(ActionOption.MoveToBoard);
        }

        if (_actionOptions.Count == 0)
        {
            TolkWrapper.Speak(Loc.T("action.menu.no.actions"));
            return;
        }

        _isInActionMode = true;

        // Announce action mode with first option
        string cardName = ItemReader.GetCardName(card);
        TolkWrapper.Speak(Loc.Plural("action.menu.count", _actionOptions.Count,
            cardName, GetActionOptionText(_actionOptions[0]), _actionOptions.Count));
    }

    /// <summary>
    /// Exits the action menu.
    /// </summary>
    public void Exit()
    {
        _isInActionMode = false;
        _actionCard = null;
        TolkWrapper.Speak(Loc.T("action.exited"));
    }

    /// <summary>
    /// Handles input while in action mode.
    /// </summary>
    public void HandleInput(AccessibleKey key)
    {
        if (_actionCard == null)
        {
            _isInActionMode = false;
            TolkWrapper.Speak(Loc.T("action.cancelled"));
            return;
        }

        // Check if we can reorder (only in board section)
        bool canReorder = _navigator.IsInBoardSection() && _navigator.CanMoveInCurrentState();
        var itemCard = _actionCard as ItemCard;

        switch (key)
        {
            // Navigate menu options
            case AccessibleKey.Up:
                if (_actionOptions.Count > 1)
                {
                    _actionIndex = (_actionIndex - 1 + _actionOptions.Count) % _actionOptions.Count;
                    AnnounceCurrentActionOption();
                }
                break;

            case AccessibleKey.Down:
                if (_actionOptions.Count > 1)
                {
                    _actionIndex = (_actionIndex + 1) % _actionOptions.Count;
                    AnnounceCurrentActionOption();
                }
                break;

            // Confirm selected option
            case AccessibleKey.Confirm:
                if (_actionOptions.Count > 0)
                {
                    ExecuteActionOption(_actionOptions[_actionIndex]);
                }
                break;

            // Shortcut: S = Sell
            case AccessibleKey.StashInfo: // S key
                if (_actionOptions.Contains(ActionOption.Sell))
                {
                    ExecuteActionOption(ActionOption.Sell);
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.sell"));
                }
                break;

            // Shortcut: U = Upgrade/Enchant/UsePedestal
            case AccessibleKey.Upgrade:
                if (_actionOptions.Contains(ActionOption.Upgrade))
                {
                    ExecuteActionOption(ActionOption.Upgrade);
                }
                else if (_actionOptions.Contains(ActionOption.Enchant))
                {
                    ExecuteActionOption(ActionOption.Enchant);
                }
                else if (_actionOptions.Contains(ActionOption.UsePedestal))
                {
                    ExecuteActionOption(ActionOption.UsePedestal);
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.upgrade.enchant.here"));
                }
                break;

            // Shortcut: M = Move
            case AccessibleKey.ActionMove:
                if (_actionOptions.Contains(ActionOption.MoveToStash))
                {
                    ExecuteActionOption(ActionOption.MoveToStash);
                }
                else if (_actionOptions.Contains(ActionOption.MoveToBoard))
                {
                    ExecuteActionOption(ActionOption.MoveToBoard);
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.move"));
                }
                break;

            // Reorder: Left/Right arrows (stay in action mode)
            case AccessibleKey.Left:
                if (canReorder && itemCard != null)
                {
                    int currentSlot = _navigator.GetCurrentBoardSlot();
                    var itemId = itemCard.InstanceId;
                    if (ActionHelper.ReorderItem(itemCard, currentSlot, -1, silent: true))
                    {
                        _navigator.Refresh();
                        // Use ID-based tracking for reliability
                        if (!_navigator.GoToItemById(itemId))
                        {
                            _navigator.GoToBoardSlot(currentSlot - 1);
                        }
                        AnnounceReorderPosition(_navigator.GetCurrentBoardSlot(), itemCard);
                        _navigator.TriggerVisualSelection();
                    }
                    // ReorderItem announces "At left edge" if at limit
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.reorder"));
                }
                break;

            case AccessibleKey.Right:
                if (canReorder && itemCard != null)
                {
                    int currentSlot = _navigator.GetCurrentBoardSlot();
                    var itemId = itemCard.InstanceId;
                    if (ActionHelper.ReorderItem(itemCard, currentSlot, 1, silent: true))
                    {
                        _navigator.Refresh();
                        // Use ID-based tracking for reliability
                        if (!_navigator.GoToItemById(itemId))
                        {
                            _navigator.GoToBoardSlot(currentSlot + 1);
                        }
                        AnnounceReorderPosition(_navigator.GetCurrentBoardSlot(), itemCard);
                        _navigator.TriggerVisualSelection();
                    }
                    // ReorderItem announces "At right edge" if at limit
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.reorder"));
                }
                break;

            // Reorder: Home/End for edges (stay in action mode)
            case AccessibleKey.Home:
                if (canReorder && itemCard != null)
                {
                    MoveItemToEdge(itemCard, -1);
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.reorder"));
                }
                break;

            case AccessibleKey.End:
                if (canReorder && itemCard != null)
                {
                    MoveItemToEdge(itemCard, 1);
                }
                else
                {
                    TolkWrapper.Speak(Loc.T("action.cannot.reorder"));
                }
                break;

            // Exit with Backspace
            case AccessibleKey.Back:
                _isInActionMode = false;
                _actionCard = null;
                TolkWrapper.Speak(Loc.T("action.exited"));
                break;

            // All other keys are ignored (stay in action mode)
            default:
                break;
        }
    }

    /// <summary>
    /// Gets the display text for an action option.
    /// </summary>
    private string GetActionOptionText(ActionOption option)
    {
        switch (option)
        {
            case ActionOption.Details:
                return Loc.T("action.details");

            case ActionOption.Sell:
                int sellPrice = ItemReader.GetSellPrice(_actionCard);
                return Loc.T("action.sell", sellPrice);

            case ActionOption.Upgrade:
                var upgradeInfo = PedestalManager.GetCurrentPedestalInfo();
                string currentTier = ItemReader.GetTierName(_actionCard);
                // Use actual target tier from pedestal if available
                if (upgradeInfo.TargetTier.HasValue)
                {
                    if (upgradeInfo.TargetTier.Value == _actionCard.Tier)
                    {
                        // Same tier - just improving stats
                        return Loc.T("action.upgrade.stats", currentTier);
                    }
                    else
                    {
                        return Loc.T("action.upgrade.to", ItemReader.GetTierName(upgradeInfo.TargetTier.Value));
                    }
                }
                // Fallback to assuming next tier
                string nextTier = TierHelper.GetNextName(_actionCard.Tier);
                return Loc.T("action.upgrade.to", nextTier);

            case ActionOption.Enchant:
                var pedestalInfo = PedestalManager.GetCurrentPedestalInfo();
                string enchantName = pedestalInfo.EnchantmentName ?? "random";
                return Loc.T("action.enchant.with", enchantName);

            case ActionOption.UsePedestal:
                return Loc.T("action.use.pedestal");

            case ActionOption.MoveToStash:
                return Loc.T("action.move.to.stash");

            case ActionOption.MoveToBoard:
                return Loc.T("action.move.to.board");

            default:
                return option.ToString();
        }
    }

    /// <summary>
    /// Announces the current action option.
    /// </summary>
    private void AnnounceCurrentActionOption()
    {
        if (_actionOptions.Count == 0) return;

        string optionText = GetActionOptionText(_actionOptions[_actionIndex]);
        int position = _actionIndex + 1;
        int total = _actionOptions.Count;
        TolkWrapper.Speak(Loc.T("action.option.position", optionText, position, total));
    }

    /// <summary>
    /// Executes the specified action option directly (no confirmation).
    /// </summary>
    private void ExecuteActionOption(ActionOption option)
    {
        var itemCard = _actionCard as ItemCard;
        _isInActionMode = false;
        _actionCard = null;

        switch (option)
        {
            case ActionOption.Details:
                if (itemCard != null)
                {
                    _onShowDetails?.Invoke(itemCard);
                }
                break;

            case ActionOption.Sell:
                if (itemCard != null)
                {
                    string name = ItemReader.GetCardName(itemCard);
                    int price = ItemReader.GetSellPrice(itemCard);
                    ActionHelper.SellItem(itemCard);
                    TolkWrapper.Speak(Loc.T("action.sold", name, price));
                    _onRefreshAndAnnounce();
                }
                break;

            case ActionOption.Upgrade:
                if (itemCard != null)
                {
                    _onUsePedestalAction?.Invoke(itemCard);
                }
                break;

            case ActionOption.Enchant:
                if (itemCard != null)
                {
                    _onUsePedestalAction?.Invoke(itemCard);
                }
                break;

            case ActionOption.UsePedestal:
                if (itemCard != null)
                {
                    if (PedestalManager.UseCurrentPedestal(itemCard))
                    {
                        _onRefreshAndAnnounce();
                    }
                }
                break;

            case ActionOption.MoveToStash:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, true);
                    TolkWrapper.Speak(Loc.T("action.moved.stash.short"));
                    _onRefreshAndAnnounce();
                }
                break;

            case ActionOption.MoveToBoard:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, false);
                    TolkWrapper.Speak(Loc.T("action.moved.board.short"));
                    _onRefreshAndAnnounce();
                }
                break;
        }
    }

    /// <summary>
    /// Announces the position after reordering, including adjacent items.
    /// </summary>
    public void AnnounceReorderPosition(int slot, ItemCard movedCard)
    {
        int cardSize = (int)movedCard.Size;
        int leftEdge = 0;
        int rightEdge = 10 - cardSize;

        // Check if at edge
        if (slot <= leftEdge)
        {
            // At left edge - announce item to the right if any
            var rightItem = _navigator.GetItemAtSlot(slot + cardSize);
            if (rightItem != null)
            {
                string rightName = ItemReader.GetCardName(rightItem);
                TolkWrapper.Speak(Loc.T("action.reorder.left.edge.before", rightName));
            }
            else
            {
                TolkWrapper.Speak(Loc.T("action.reorder.left.edge"));
            }
        }
        else if (slot >= rightEdge)
        {
            // At right edge - announce item to the left if any
            var leftItem = _navigator.GetItemAtSlot(slot - 1);
            if (leftItem != null)
            {
                string leftName = ItemReader.GetCardName(leftItem);
                TolkWrapper.Speak(Loc.T("action.reorder.right.edge.after", leftName));
            }
            else
            {
                TolkWrapper.Speak(Loc.T("action.reorder.right.edge"));
            }
        }
        else
        {
            // In the middle - announce items on both sides
            var leftItem = _navigator.GetItemAtSlot(slot - 1);
            var rightItem = _navigator.GetItemAtSlot(slot + cardSize);

            if (leftItem != null && rightItem != null)
            {
                string leftName = ItemReader.GetCardName(leftItem);
                string rightName = ItemReader.GetCardName(rightItem);
                TolkWrapper.Speak(Loc.T("action.reorder.between", leftName, rightName));
            }
            else if (leftItem != null)
            {
                string leftName = ItemReader.GetCardName(leftItem);
                TolkWrapper.Speak(Loc.T("action.reorder.after", leftName));
            }
            else if (rightItem != null)
            {
                string rightName = ItemReader.GetCardName(rightItem);
                TolkWrapper.Speak(Loc.T("action.reorder.before", rightName));
            }
            else
            {
                TolkWrapper.Speak(Loc.T("action.reorder.position", slot + 1));
            }
        }
    }

    /// <summary>
    /// Moves an item to the left or right edge of the board.
    /// Uses a coroutine with delays to let the game properly update adjacency effects.
    /// </summary>
    private void MoveItemToEdge(ItemCard card, int direction)
    {
        if (card == null)
        {
            TolkWrapper.Speak(Loc.T("action.cannot.move.this"));
            return;
        }

        int currentSlot = _navigator.GetCurrentBoardSlot();
        if (currentSlot < 0)
        {
            TolkWrapper.Speak(Loc.T("action.cannot.determine.position"));
            return;
        }

        int cardSize = (int)card.Size;
        int targetSlot;

        if (direction < 0)
        {
            // Move to left edge (slot 0)
            targetSlot = 0;
        }
        else
        {
            // Move to right edge (slot 10 - cardSize)
            targetSlot = 10 - cardSize;
        }

        if (targetSlot == currentSlot)
        {
            TolkWrapper.Speak(direction < 0 ? Loc.T("action.already.at.left.edge") : Loc.T("action.already.at.right.edge"));
            return;
        }

        // Use coroutine to move with delays between steps
        Plugin.Instance.StartCoroutine(MoveItemToEdgeCoroutine(card, currentSlot, targetSlot));
    }

    /// <summary>
    /// Coroutine that moves an item step by step with delays.
    /// This allows the game to properly update adjacency effects between moves.
    /// </summary>
    private IEnumerator MoveItemToEdgeCoroutine(ItemCard card, int startSlot, int targetSlot)
    {
        int currentSlot = startSlot;
        int stepDirection = (targetSlot > currentSlot) ? 1 : -1;
        int moveCount = Math.Abs(targetSlot - startSlot);

        // Store the item's InstanceId for reliable tracking after moves
        var itemId = card.InstanceId;

        TolkWrapper.Speak(stepDirection < 0 ? Loc.T("action.moving.to.left.edge") : Loc.T("action.moving.to.right.edge"));

        // Move step by step with small delays
        while (currentSlot != targetSlot)
        {
            if (!ActionHelper.ReorderItem(card, currentSlot, stepDirection, silent: true))
            {
                break; // Stop if a move fails
            }
            currentSlot += stepDirection;

            // Small delay to let the game process adjacency effects
            yield return new WaitForSeconds(0.05f);
        }

        // Final refresh and navigate to the moved item by its ID (more reliable than slot)
        _navigator.Refresh();
        if (!_navigator.GoToItemById(itemId))
        {
            // Fallback to slot-based navigation
            _navigator.GoToBoardSlot(currentSlot);
        }
        AnnounceReorderPosition(_navigator.GetCurrentBoardSlot(), card);
        _navigator.TriggerVisualSelection();
    }
}
