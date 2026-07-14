using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarGameShared.TempoNet.Responses;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Tooltips;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Screens;

/// <summary>
/// Accessible screen for the Battle Pass / Season Pass menu.
/// Allows navigation through challenges, tiers and collecting rewards.
/// </summary>
public class BattlePassScreen : BaseScreen
{
    public override string ScreenName => Loc.T("screen.battlepass.name");

    private BattlePassView _view;

    private enum MenuMode
    {
        Main,
        Challenges,
        Tiers
    }

    private MenuMode _currentMode = MenuMode.Main;

    // For challenge navigation
    private bool _inWeeklyChallenges = false;
    private int _currentChallengeIndex = 0;
    private ChallengeProgress[] _dailyChallenges;
    private ChallengeProgress[] _weeklyChallenges;

    // For tier navigation
    private int _currentTierIndex = 0;

    public BattlePassScreen(Transform root, BattlePassView view) : base(root)
    {
        _view = view;
        RefreshChallenges();
    }

    protected override void BuildMenu()
    {
        // Back button
        Menu.AddOption(
            () => Loc.T("screen.battlepass.back"),
            () => HandleBack());

        // Challenges submenu
        Menu.AddOption(
            () => Loc.T("screen.battlepass.challenges"),
            () => EnterChallenges());

        // Tiers / Rewards submenu
        Menu.AddOption(
            () => Loc.T("screen.battlepass.tiers"),
            () => EnterTiers());

        // Collect All button
        Menu.AddOption(
            () => Loc.T("screen.battlepass.collect_all"),
            () => CollectAll());

        // Open Chests
        Menu.AddOption(
            () => Loc.T("screen.battlepass.open_chests"),
            () => OpenChests());
    }

    #region Challenges

    private void RefreshChallenges()
    {
        try
        {
            if (ClientCache.Challenges.HasData)
            {
                var profile = ClientCache.Challenges.Value;
                _dailyChallenges = profile.dailyChallenges ?? Array.Empty<ChallengeProgress>();
                _weeklyChallenges = profile.weeklyChallenges ?? Array.Empty<ChallengeProgress>();
            }
            else
            {
                _dailyChallenges = Array.Empty<ChallengeProgress>();
                _weeklyChallenges = Array.Empty<ChallengeProgress>();
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error refreshing challenges: {e.Message}");
            _dailyChallenges = Array.Empty<ChallengeProgress>();
            _weeklyChallenges = Array.Empty<ChallengeProgress>();
        }
    }

    private void EnterChallenges()
    {
        _currentMode = MenuMode.Challenges;
        _inWeeklyChallenges = false;
        _currentChallengeIndex = 0;
        RefreshChallenges();

        string info = GetChallengesOverview();
        TolkWrapper.Speak($"{Loc.T("screen.battlepass.challenges")}. {info}. {Loc.T("screen.battlepass.nav_hint")}");

        ReadCurrentChallenge();
    }

    private string GetChallengesOverview()
    {
        int daily = _dailyChallenges?.Length ?? 0;
        int weekly = _weeklyChallenges?.Length ?? 0;
        string dailyText = Loc.Plural("screen.battlepass.daily", daily, daily);
        string weeklyText = Loc.Plural("screen.battlepass.weekly", weekly, weekly);
        return $"{dailyText}, {weeklyText}";
    }

    private void ReadCurrentChallenge()
    {
        RefreshChallenges();

        var challenges = _inWeeklyChallenges ? _weeklyChallenges : _dailyChallenges;
        string type = _inWeeklyChallenges ? Loc.T("screen.battlepass.weekly_label") : Loc.T("screen.battlepass.daily_label");

        if (challenges == null || challenges.Length == 0)
        {
            TolkWrapper.Speak(_inWeeklyChallenges
                ? Loc.T("screen.battlepass.no_challenges_weekly")
                : Loc.T("screen.battlepass.no_challenges_daily"));
            return;
        }

        if (_currentChallengeIndex < 0) _currentChallengeIndex = 0;
        if (_currentChallengeIndex >= challenges.Length) _currentChallengeIndex = challenges.Length - 1;

        var challenge = challenges[_currentChallengeIndex];
        string text = GetChallengeText(challenge, type, _currentChallengeIndex + 1, challenges.Length);
        TolkWrapper.Speak(text);
    }

    private string GetChallengeText(ChallengeProgress challenge, string type, int position, int total)
    {
        try
        {
            var data = Data.GetStatic()?.GetChallengeById(challenge.Id);
            if (data != null)
            {
                string title = data.Localization?.Title?.GetLocalizedText() ?? Loc.T("screen.battlepass.challenge_fallback_title");
                string desc = data.Localization?.Description?.GetLocalizedText() ?? "";

                if (desc.Contains("{completionRequirement}"))
                {
                    desc = desc.Replace("{completionRequirement}", data.CompletionRequirement.ToString());
                }

                string progress = Loc.T("screen.battlepass.progress", challenge.Progress, data.CompletionRequirement);
                string status = challenge.Progress >= data.CompletionRequirement
                    ? (challenge.Acknowledged ? Loc.T("screen.battlepass.status_claimed") : Loc.T("screen.battlepass.status_ready"))
                    : Loc.T("screen.battlepass.status_inprogress");
                string xp = Loc.T("screen.battlepass.xp_reward", data.XpReward);

                return Loc.T("screen.battlepass.challenge_summary",
                    Loc.T("screen.battlepass.challenge_header", type, position, total), title, desc, progress, status, xp);
            }
            else
            {
                return $"{Loc.T("screen.battlepass.challenge_header", type, position, total)}. {Loc.T("screen.battlepass.progress_label_simple", challenge.Progress)}";
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error reading challenge: {e.Message}");
            return Loc.T("screen.battlepass.challenge_header", type, position, total);
        }
    }

    private void ClaimCurrentChallenge()
    {
        RefreshChallenges();

        var challenges = _inWeeklyChallenges ? _weeklyChallenges : _dailyChallenges;
        if (challenges == null || challenges.Length == 0 || _currentChallengeIndex >= challenges.Length)
        {
            ReadCurrentChallenge();
            return;
        }

        var challenge = challenges[_currentChallengeIndex];

        try
        {
            var data = Data.GetStatic()?.GetChallengeById(challenge.Id);
            if (data != null)
            {
                // Check if completed but not claimed
                bool isComplete = challenge.Progress >= data.CompletionRequirement;
                bool isClaimed = challenge.Acknowledged;

                if (isComplete && !isClaimed)
                {
                    // Claim the challenge via reflection (Events is internal)
                    TriggerAcknowledgeChallenge(challenge.Id);

                    // Announce what was received
                    string reward = Loc.T("screen.battlepass.xp_reward", data.XpReward);
                    TolkWrapper.Speak(Loc.T("screen.battlepass.claimed", reward));

                    // Refresh data
                    RefreshChallenges();
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error claiming challenge: {e.Message}");
        }

        // If not claimable, just read it
        ReadCurrentChallenge();
    }

    /// <summary>
    /// Triggers the AcknowledgeChallenge event via reflection (Events is internal).
    /// </summary>
    private void TriggerAcknowledgeChallenge(Guid challengeId)
    {
        try
        {
            var eventsType = typeof(Data).Assembly.GetType("TheBazaar.Events");
            if (eventsType == null)
            {
                Plugin.Logger.LogError("Could not find Events type");
                return;
            }

            var acknowledgeField = eventsType.GetField("AcknowledgeChallenge",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (acknowledgeField == null)
            {
                Plugin.Logger.LogError("Could not find AcknowledgeChallenge field");
                return;
            }

            var eventObj = acknowledgeField.GetValue(null);
            if (eventObj == null)
            {
                Plugin.Logger.LogError("AcknowledgeChallenge event is null");
                return;
            }

            var triggerMethod = eventObj.GetType().GetMethod("Trigger", new Type[] { typeof(Guid) });
            if (triggerMethod == null)
            {
                Plugin.Logger.LogError("Could not find Trigger method");
                return;
            }

            triggerMethod.Invoke(eventObj, new object[] { challengeId });
            Plugin.Logger.LogInfo($"Triggered AcknowledgeChallenge for {challengeId}");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error triggering AcknowledgeChallenge: {e.Message}");
        }
    }

    private void NavigateChallengeUp()
    {
        RefreshChallenges();

        if (_currentChallengeIndex > 0)
        {
            _currentChallengeIndex--;
            ReadCurrentChallenge();
        }
        else if (_inWeeklyChallenges && _dailyChallenges != null && _dailyChallenges.Length > 0)
        {
            _inWeeklyChallenges = false;
            _currentChallengeIndex = _dailyChallenges.Length - 1;
            TolkWrapper.Speak(Loc.T("screen.battlepass.daily_challenges_header"));
            ReadCurrentChallenge();
        }
        else
        {
            TolkWrapper.Speak(Loc.T("screen.battlepass.first_challenge"));
        }
    }

    private void NavigateChallengeDown()
    {
        RefreshChallenges();

        var currentChallenges = _inWeeklyChallenges ? _weeklyChallenges : _dailyChallenges;

        if (_currentChallengeIndex < currentChallenges.Length - 1)
        {
            _currentChallengeIndex++;
            ReadCurrentChallenge();
        }
        else if (!_inWeeklyChallenges && _weeklyChallenges != null && _weeklyChallenges.Length > 0)
        {
            _inWeeklyChallenges = true;
            _currentChallengeIndex = 0;
            TolkWrapper.Speak(Loc.T("screen.battlepass.weekly_challenges_header"));
            ReadCurrentChallenge();
        }
        else
        {
            TolkWrapper.Speak(Loc.T("screen.battlepass.last_challenge"));
        }
    }

    #endregion

    #region Tiers

    private void EnterTiers()
    {
        _currentMode = MenuMode.Tiers;
        _currentTierIndex = GetCurrentUserTierIndex();

        var tiersView = BattlePassTiersView.Instance;
        if (tiersView == null || !tiersView.Initialized)
        {
            TolkWrapper.Speak(Loc.T("screen.battlepass.tiers_not_loaded"));
            _currentMode = MenuMode.Main;
            return;
        }

        int totalTiers = GetTotalTiers();
        TolkWrapper.Speak($"{Loc.T("screen.battlepass.tiers_total", totalTiers)} {Loc.T("screen.battlepass.nav_hint")}");

        ReadCurrentTier();
    }

    private int GetTotalTiers()
    {
        try
        {
            var tiersView = BattlePassTiersView.Instance;
            if (tiersView == null) return 0;

            var tierDataListField = typeof(BattlePassTiersView).GetField("_tierDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tierDataListField == null) return 0;

            var tierDataList = tierDataListField.GetValue(tiersView) as List<BattlePassTierData>;
            return tierDataList?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private int GetCurrentUserTierIndex()
    {
        try
        {
            var tiersView = BattlePassTiersView.Instance;
            if (tiersView == null) return 0;

            var tierDataListField = typeof(BattlePassTiersView).GetField("_tierDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tierDataListField == null) return 0;

            var tierDataList = tierDataListField.GetValue(tiersView) as List<BattlePassTierData>;
            if (tierDataList == null || tierDataList.Count == 0) return 0;

            int userXP = tierDataList[0].userXP;
            for (int i = 0; i < tierDataList.Count; i++)
            {
                if (tierDataList[i].tierXPRequired > userXP)
                {
                    return i;
                }
            }
            return tierDataList.Count - 1;
        }
        catch
        {
            return 0;
        }
    }

    private void ReadCurrentTier()
    {
        try
        {
            var tiersView = BattlePassTiersView.Instance;
            if (tiersView == null)
            {
                TolkWrapper.Speak(Loc.T("screen.battlepass.tiers_unavailable"));
                return;
            }

            var tierDataListField = typeof(BattlePassTiersView).GetField("_tierDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tierDataListField == null) return;

            var tierDataList = tierDataListField.GetValue(tiersView) as List<BattlePassTierData>;
            if (tierDataList == null || tierDataList.Count == 0)
            {
                TolkWrapper.Speak(Loc.T("screen.battlepass.no_tiers"));
                return;
            }

            if (_currentTierIndex < 0) _currentTierIndex = 0;
            if (_currentTierIndex >= tierDataList.Count) _currentTierIndex = tierDataList.Count - 1;

            var tierData = tierDataList[_currentTierIndex];
            string text = GetTierText(tierData, _currentTierIndex + 1, tierDataList.Count);
            TolkWrapper.Speak(text);

            // Focus the game's view on this tier
            tiersView.FocusTier(tierData.tierNumber);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error reading tier: {e.Message}");
            TolkWrapper.Speak(Loc.T("screen.battlepass.tier_fallback", _currentTierIndex + 1));
        }
    }

    private string GetTierText(BattlePassTierData tierData, int position, int total)
    {
        var parts = new List<string>();

        parts.Add(Loc.T("screen.battlepass.tier_header", tierData.tierNumber, total));

        // Status
        string status = tierData.currentState switch
        {
            BattlePassTierState.NotStarted => Loc.T("screen.battlepass.tier_status_locked"),
            BattlePassTierState.InProgress => Loc.T("screen.battlepass.tier_status_inprogress"),
            BattlePassTierState.ReadyToClaim => Loc.T("screen.battlepass.tier_status_ready"),
            BattlePassTierState.Claimed => Loc.T("screen.battlepass.tier_status_claimed"),
            _ => Loc.T("screen.battlepass.tier_status_unknown")
        };
        parts.Add(status);

        // XP info
        if (tierData.currentState == BattlePassTierState.InProgress)
        {
            int currentXP = tierData.userXP - tierData.previousTierXPRequired;
            int neededXP = tierData.tierXPRequired - tierData.previousTierXPRequired;
            parts.Add(Loc.T("screen.battlepass.tier_xp_progress", currentXP, neededXP));
        }
        else
        {
            parts.Add(Loc.T("screen.battlepass.tier_xp_required", tierData.tierXPRequired));
        }

        // Rewards
        if (tierData.tierResponse.HasValue)
        {
            var rewards = tierData.tierResponse.Value.freeRewards;
            var rewardParts = new List<string>();

            if (rewards.chests > 0)
            {
                rewardParts.Add(Loc.Plural("screen.battlepass.chest_count", rewards.chests, rewards.chests));
            }

            if (rewards.currencies != null)
            {
                foreach (var currency in rewards.currencies)
                {
                    rewardParts.Add(Loc.T("screen.battlepass.currency_amount", currency.Amount, currency.CurrencyType));
                }
            }

            if (rewards.collectionItemIds != null && rewards.collectionItemIds.Length > 0)
            {
                rewardParts.Add(Loc.Plural("screen.battlepass.collection_item_count", rewards.collectionItemIds.Length, rewards.collectionItemIds.Length));
            }

            if (rewardParts.Count > 0)
            {
                parts.Add(Loc.T("screen.battlepass.rewards_prefix", string.Join(", ", rewardParts)));
            }
        }

        return string.Join(". ", parts);
    }

    private void NavigateTierUp()
    {
        if (_currentTierIndex > 0)
        {
            _currentTierIndex--;
            ReadCurrentTier();
        }
        else
        {
            TolkWrapper.Speak(Loc.T("screen.battlepass.first_tier"));
        }
    }

    private void NavigateTierDown()
    {
        int total = GetTotalTiers();
        if (_currentTierIndex < total - 1)
        {
            _currentTierIndex++;
            ReadCurrentTier();
        }
        else
        {
            TolkWrapper.Speak(Loc.T("screen.battlepass.last_tier"));
        }
    }

    #endregion

    #region Actions

    private void CollectAll()
    {
        try
        {
            // Try to find and click the collect all button via reflection
            var collectAllField = typeof(BattlePassView).GetField("_collectAllButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (collectAllField != null)
            {
                var button = collectAllField.GetValue(_view) as BazaarButtonController;
                if (button != null && button.interactable)
                {
                    button.onClick.Invoke();
                    TolkWrapper.Speak(Loc.T("screen.battlepass.collecting_all"));
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error collecting rewards: {e.Message}");
        }

        TolkWrapper.Speak(Loc.T("screen.battlepass.no_rewards"));
    }

    private void OpenChests()
    {
        try
        {
            // Access the private _chestButton field via reflection
            var chestButtonField = typeof(BattlePassView).GetField("_chestButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (chestButtonField != null)
            {
                var button = chestButtonField.GetValue(_view) as BazaarButtonController;
                if (button != null)
                {
                    button.onClick.Invoke();
                    TolkWrapper.Speak(Loc.T("screen.battlepass.opening_chests"));
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error opening chests: {e.Message}");
        }

        TolkWrapper.Speak(Loc.T("screen.battlepass.chests_open_failed"));
    }

    private void HandleBack()
    {
        if (_currentMode != MenuMode.Main)
        {
            _currentMode = MenuMode.Main;
            TolkWrapper.Speak(ScreenName);
        }
        else
        {
            GoBack();
        }
    }

    private void GoBack()
    {
        try
        {
            var backButtonField = typeof(BattlePassView).GetField("_backButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backButtonField != null)
            {
                var button = backButtonField.GetValue(_view) as BazaarButtonController;
                if (button != null)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Error going back: {e.Message}");
        }
    }

    #endregion

    public override void HandleInput(AccessibleKey key)
    {
        if (_currentMode == MenuMode.Challenges)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    NavigateChallengeUp();
                    return;
                case AccessibleKey.Down:
                    NavigateChallengeDown();
                    return;
                case AccessibleKey.Back:
                    _currentMode = MenuMode.Main;
                    TolkWrapper.Speak(ScreenName);
                    return;
                case AccessibleKey.Confirm:
                    ClaimCurrentChallenge();
                    return;
            }
            return;
        }

        if (_currentMode == MenuMode.Tiers)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    NavigateTierUp();
                    return;
                case AccessibleKey.Down:
                    NavigateTierDown();
                    return;
                case AccessibleKey.Back:
                    _currentMode = MenuMode.Main;
                    TolkWrapper.Speak(ScreenName);
                    return;
                case AccessibleKey.Confirm:
                    ReadCurrentTier();
                    return;
            }
            return;
        }

        // Main mode
        switch (key)
        {
            case AccessibleKey.Back:
                HandleBack();
                return;
        }

        base.HandleInput(key);
    }

    public override void OnFocus()
    {
        _currentMode = MenuMode.Main;
        TolkWrapper.Speak(ScreenName);
    }
}
