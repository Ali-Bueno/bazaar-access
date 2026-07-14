using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using TheBazaar.Feature.Chest.Scene;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Accessible screen for the chest opening scene.
/// Allows navigation through chest types and provides information about available chests.
/// </summary>
public class ChestSceneScreen : BaseScreen
{
    public override string ScreenName => Loc.T("screen.chestscene.name");

    private ChestSceneController _controller;
    private int _currentChestIndex = 0;
    private List<ChestInfo> _chests = new List<ChestInfo>();

    private struct ChestInfo
    {
        public int SeasonId;
        public string SeasonName;
        public int Quantity;
    }

    public ChestSceneScreen(Transform root, ChestSceneController controller) : base(root)
    {
        _controller = controller;
    }

    protected override void BuildMenu()
    {
        RefreshChestData();

        // Home button
        Menu.AddOption(
            () => Loc.T("screen.chestscene.back"),
            () => GoBack());

        // Current chest type with navigation - Enter opens chest
        Menu.AddOption(
            () => GetCurrentChestText(),
            () => OpenChest(),
            (dir) => NavigateChestType(dir),
            () => HasChestsToOpen());

        // Multi-open option (if available)
        Menu.AddOption(
            () => GetMultiOpenText(),
            () => ClickMultiOpen(),
            () => CanMultiOpen());
    }

    private bool HasChestsToOpen()
    {
        RefreshChestData();
        return _chests.Count > 0;
    }

    private void RefreshChestData()
    {
        _chests.Clear();

        if (_controller == null || _controller.playerChestInventory == null) return;

        try
        {
            var inventories = _controller.playerChestInventory.GetChestsInventory();
            if (inventories == null) return;

            foreach (var inv in inventories)
            {
                if (inv == null || inv.Quantity <= 0) continue;

                var info = new ChestInfo
                {
                    SeasonId = inv.seasonId,
                    Quantity = inv.Quantity,
                    SeasonName = inv.seasonName ?? Loc.T("screen.chestscene.season_fallback", inv.seasonId)
                };
                _chests.Add(info);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error reading chest inventory: {e.Message}");
        }
    }

    private string GetCurrentChestText()
    {
        RefreshChestData();

        if (_chests.Count == 0)
            return Loc.T("screen.chestscene.no_chests");

        if (_currentChestIndex >= _chests.Count)
            _currentChestIndex = 0;

        var chest = _chests[_currentChestIndex];
        return Loc.T("screen.chestscene.chest_summary", chest.SeasonName, chest.Quantity);
    }

    private void NavigateChestType(int direction)
    {
        RefreshChestData();

        if (_chests.Count == 0) return;

        _currentChestIndex += direction;
        if (_currentChestIndex < 0) _currentChestIndex = _chests.Count - 1;
        if (_currentChestIndex >= _chests.Count) _currentChestIndex = 0;

        // Update the game's selection to match
        try
        {
            var chest = _chests[_currentChestIndex];
            _controller.playerChestInventory.SetSelectedInventoryBySeasonId(chest.SeasonId);

            // Also update the wheel visually
            if (_controller.ChestWheel != null)
            {
                _controller.ChestWheel.SetSelectedSeasonChestbySeasonId(chest.SeasonId);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error navigating chest type: {e.Message}");
        }

        TolkWrapper.Speak(GetCurrentChestText());
    }

    private void ReadCurrentChest()
    {
        TolkWrapper.Speak(GetCurrentChestText());
    }

    private bool CanMultiOpen()
    {
        if (_controller == null) return false;

        try
        {
            var selectedInv = _controller.playerChestInventory?.selectedSeasonInventory;
            if (selectedInv == null) return false;

            int required = _controller.numChestsRequiredForMultiOpen;
            return selectedInv.Quantity >= required;
        }
        catch
        {
            return false;
        }
    }

    private string GetMultiOpenText()
    {
        if (_controller == null) return Loc.T("screen.chestscene.open_n", 10);

        int required = _controller.numChestsRequiredForMultiOpen;
        return Loc.T("screen.chestscene.open_multi", required);
    }

    private async void ClickMultiOpen()
    {
        if (!CanMultiOpen())
        {
            TolkWrapper.Speak(Loc.T("screen.chestscene.not_enough_multi"));
            return;
        }

        try
        {
            int numChests = _controller.numChestsRequiredForMultiOpen;
            var chest = _chests[_currentChestIndex];

            TolkWrapper.Speak(Loc.T("screen.chestscene.opening_multi", numChests, chest.SeasonName));

            // Change to MultiSelect state which spawns the chests
            _controller.ChangeState(ChestSceneController.States.MultiSelect);

            // Wait for chests to spawn and lever to be ready
            await Task.Delay(5500);

            // Trigger the lever pull to start multi-open
            if (_controller.MultiOpenLever != null)
            {
                _controller.MultiOpenLever.TriggerPullAndRelease();
            }

            // Wait for multi-open animations to complete (varies by rarity, ~20 seconds)
            await Task.Delay(20000);

            // Show rewards
            var rewards = _controller.playerChestInventory?.openedChestRewards;
            if (rewards != null && rewards.Count > 0)
            {
                var ui = new UI.ChestRewardsUI(_controller.transform, rewards);
                AccessibilityMgr.ShowUI(ui);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error in multi-open: {e.Message}");
            TolkWrapper.Speak(Loc.T("screen.chestscene.error_opening_multi"));
        }
    }

    private void GoBack()
    {
        try
        {
            // Click the home button to exit
            if (_controller.HomeButton != null)
            {
                _controller.HomeButton.onClick.Invoke();
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error going home: {e.Message}");
        }
    }

    /// <summary>
    /// Called by ChestRewardsUI when closing rewards to return to selection.
    /// </summary>
    public void ReturnToSelection()
    {
        try
        {
            _controller.ChangeState(ChestSceneController.States.Select);
            RefreshChestData();
            TolkWrapper.Speak($"{ScreenName}. {GetCurrentChestText()}");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error returning to selection: {e.Message}");
            TolkWrapper.Speak(ScreenName);
        }
    }

    public override void HandleInput(AccessibleKey key)
    {
        if (key == AccessibleKey.Back)
        {
            GoBack();
            return;
        }

        base.HandleInput(key);
    }

    /// <summary>
    /// Opens a chest of the currently selected type.
    /// </summary>
    private async void OpenChest()
    {
        RefreshChestData();

        if (_chests.Count == 0)
        {
            TolkWrapper.Speak(Loc.T("screen.chestscene.no_chests"));
            return;
        }

        if (_currentChestIndex >= _chests.Count)
            _currentChestIndex = 0;

        var chest = _chests[_currentChestIndex];

        try
        {
            TolkWrapper.Speak(Loc.T("screen.chestscene.opening_single", chest.SeasonName));

            // Open the chest on the server
            bool success = await _controller.playerChestInventory.OpenChests(chest.SeasonId, 1);

            if (success)
            {
                // Transition to the Open state
                _controller.ChangeState(ChestSceneController.States.Open);

                // Wait for animations to complete, then show rewards
                await WaitAndShowRewards();
            }
            else
            {
                TolkWrapper.Speak(Loc.T("screen.chestscene.open_failed"));
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error opening chest: {e.Message}");
            TolkWrapper.Speak(Loc.T("screen.chestscene.open_error"));
        }
    }

    /// <summary>
    /// Waits for chest opening animation and shows rewards UI.
    /// </summary>
    private async Task WaitAndShowRewards()
    {
        // Wait for chest animation to complete (approximately 5-6 seconds)
        await Task.Delay(6000);

        var rewards = _controller.playerChestInventory?.openedChestRewards;
        if (rewards == null || rewards.Count == 0)
        {
            Plugin.Logger.LogWarning("ChestSceneScreen: No rewards found after opening");
            return;
        }

        // Show rewards UI
        var ui = new UI.ChestRewardsUI(_controller.transform, rewards);
        AccessibilityMgr.ShowUI(ui);
    }
}
