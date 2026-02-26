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
    Sell,
    Upgrade,
    Enchant,
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
    private readonly Action<Card, bool?> _onUpgradeConfirm;
    private readonly Action _onRefreshAndAnnounce;

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

    public ActionMenuHandler(GameplayNavigator navigator, Action<Card, bool?> onUpgradeConfirm, Action onRefreshAndAnnounce)
    {
        _navigator = navigator;
        _onUpgradeConfirm = onUpgradeConfirm;
        _onRefreshAndAnnounce = onRefreshAndAnnounce;
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
                // Detection failed - offer generic pedestal action based on card state
                // Try enchant if item isn't enchanted, otherwise try upgrade
                var itemCardCheck = card as ItemCard;
                if (itemCardCheck != null && !itemCardCheck.Enchantment.HasValue)
                {
                    _actionOptions.Add(ActionOption.Enchant);
                }
                else if (card.Tier != ETier.Legendary)
                {
                    _actionOptions.Add(ActionOption.Upgrade);
                }
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
            TolkWrapper.Speak("No actions available");
            return;
        }

        _isInActionMode = true;

        // Announce action mode with first option
        string cardName = ItemReader.GetCardName(card);
        TolkWrapper.Speak($"{cardName}. {GetActionOptionText(_actionOptions[0])}. " +
                          $"{_actionOptions.Count} actions. Backspace to cancel.");
    }

    /// <summary>
    /// Exits the action menu.
    /// </summary>
    public void Exit()
    {
        _isInActionMode = false;
        _actionCard = null;
        TolkWrapper.Speak("Exited");
    }

    /// <summary>
    /// Handles input while in action mode.
    /// </summary>
    public void HandleInput(AccessibleKey key)
    {
        if (_actionCard == null)
        {
            _isInActionMode = false;
            TolkWrapper.Speak("Action cancelled");
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
                    TolkWrapper.Speak("Cannot sell");
                }
                break;

            // Shortcut: U = Upgrade/Enchant
            case AccessibleKey.Upgrade:
                if (_actionOptions.Contains(ActionOption.Upgrade))
                {
                    ExecuteActionOption(ActionOption.Upgrade);
                }
                else if (_actionOptions.Contains(ActionOption.Enchant))
                {
                    ExecuteActionOption(ActionOption.Enchant);
                }
                else
                {
                    TolkWrapper.Speak("Cannot upgrade or enchant here");
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
                    TolkWrapper.Speak("Cannot move");
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
                    TolkWrapper.Speak("Cannot reorder");
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
                    TolkWrapper.Speak("Cannot reorder");
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
                    TolkWrapper.Speak("Cannot reorder");
                }
                break;

            case AccessibleKey.End:
                if (canReorder && itemCard != null)
                {
                    MoveItemToEdge(itemCard, 1);
                }
                else
                {
                    TolkWrapper.Speak("Cannot reorder");
                }
                break;

            // Exit with Backspace
            case AccessibleKey.Back:
                _isInActionMode = false;
                _actionCard = null;
                TolkWrapper.Speak("Exited");
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
            case ActionOption.Sell:
                int sellPrice = ItemReader.GetSellPrice(_actionCard);
                return $"Sell for {sellPrice} gold (S)";

            case ActionOption.Upgrade:
                var upgradeInfo = PedestalManager.GetCurrentPedestalInfo();
                string currentTier = ItemReader.GetTierName(_actionCard);
                // Use actual target tier from pedestal if available
                if (upgradeInfo.TargetTier.HasValue)
                {
                    if (upgradeInfo.TargetTier.Value == _actionCard.Tier)
                    {
                        // Same tier - just improving stats
                        return $"Upgrade {currentTier} stats (U)";
                    }
                    else
                    {
                        return $"Upgrade to {ItemReader.GetTierName(upgradeInfo.TargetTier.Value)} (U)";
                    }
                }
                // Fallback to assuming next tier
                string nextTier = TierHelper.GetNextName(_actionCard.Tier);
                return $"Upgrade to {nextTier} (U)";

            case ActionOption.Enchant:
                var pedestalInfo = PedestalManager.GetCurrentPedestalInfo();
                string enchantName = pedestalInfo.EnchantmentName ?? "random";
                return $"Enchant with {enchantName} (U)";

            case ActionOption.MoveToStash:
                return "Move to stash (M)";

            case ActionOption.MoveToBoard:
                return "Move to board (M)";

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
        TolkWrapper.Speak($"{optionText}, {position} of {total}");
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
            case ActionOption.Sell:
                if (itemCard != null)
                {
                    string name = ItemReader.GetCardName(itemCard);
                    int price = ItemReader.GetSellPrice(itemCard);
                    ActionHelper.SellItem(itemCard);
                    TolkWrapper.Speak($"Sold {name} for {price} gold");
                    _onRefreshAndAnnounce();
                }
                break;

            case ActionOption.Upgrade:
                // Show confirmation dialog with preview instead of executing directly
                if (itemCard != null)
                {
                    _onUpgradeConfirm(itemCard, false);
                }
                break;

            case ActionOption.Enchant:
                if (itemCard != null)
                {
                    _onUpgradeConfirm(itemCard, true);
                }
                break;

            case ActionOption.MoveToStash:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, true);
                    TolkWrapper.Speak("Moved to stash");
                    _onRefreshAndAnnounce();
                }
                break;

            case ActionOption.MoveToBoard:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, false);
                    TolkWrapper.Speak("Moved to board");
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
                TolkWrapper.Speak($"Left edge, before {rightName}");
            }
            else
            {
                TolkWrapper.Speak("Left edge");
            }
        }
        else if (slot >= rightEdge)
        {
            // At right edge - announce item to the left if any
            var leftItem = _navigator.GetItemAtSlot(slot - 1);
            if (leftItem != null)
            {
                string leftName = ItemReader.GetCardName(leftItem);
                TolkWrapper.Speak($"Right edge, after {leftName}");
            }
            else
            {
                TolkWrapper.Speak("Right edge");
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
                TolkWrapper.Speak($"Between {leftName} and {rightName}");
            }
            else if (leftItem != null)
            {
                string leftName = ItemReader.GetCardName(leftItem);
                TolkWrapper.Speak($"After {leftName}");
            }
            else if (rightItem != null)
            {
                string rightName = ItemReader.GetCardName(rightItem);
                TolkWrapper.Speak($"Before {rightName}");
            }
            else
            {
                TolkWrapper.Speak($"Position {slot + 1}");
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
            TolkWrapper.Speak("Cannot move this");
            return;
        }

        int currentSlot = _navigator.GetCurrentBoardSlot();
        if (currentSlot < 0)
        {
            TolkWrapper.Speak("Cannot determine position");
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
            string edge = direction < 0 ? "left" : "right";
            TolkWrapper.Speak($"Already at {edge} edge");
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

        string edge = stepDirection < 0 ? "left" : "right";
        TolkWrapper.Speak($"Moving to {edge} edge");

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
