using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Handles all announcements, state descriptions, and game actions (exit/reroll).
/// </summary>
public class GameplayAnnouncer
{
    private readonly GameplayNavigator _nav;

    public GameplayAnnouncer(GameplayNavigator nav)
    {
        _nav = nav;
    }

    // ===============================================
    // STATE VERIFICATION
    // ===============================================

    /// <summary>
    /// Verifies that the reported state matches the actual content.
    /// Fixes cases where the game reports Choice/Shop but content is encounters.
    /// </summary>
    public ERunState VerifyStateMatchesContent(ERunState reportedState)
    {
        if (reportedState != ERunState.Choice)
        {
            return reportedState;
        }

        if (!_nav.HasSelectionContent())
        {
            return reportedState;
        }

        // Check first card to detect actual content type
        var firstCard = _nav.GetCardAtSelectionIndex(0);
        if (firstCard == null) return reportedState;

        if (SelectionNavigator.IsEncounterCard(firstCard))
        {
            Plugin.Logger.LogInfo($"VerifyStateMatchesContent: State says Choice but content is encounters, correcting to Encounter");
            return ERunState.Encounter;
        }

        if (firstCard.Type == ECardType.Skill)
        {
            Plugin.Logger.LogInfo($"VerifyStateMatchesContent: State says Choice but content is skills, correcting to LevelUp");
            return ERunState.LevelUp;
        }

        return reportedState;
    }

    // ===============================================
    // ANNOUNCEMENTS
    // ===============================================

    /// <summary>
    /// Announces the current state simply.
    /// </summary>
    public void AnnounceState()
    {
        _nav.Refresh();

        var runState = _nav.GetCurrentState();
        runState = VerifyStateMatchesContent(runState);

        string announcement;
        switch (runState)
        {
            case ERunState.Choice:
                announcement = "Shop";
                break;
            case ERunState.Encounter:
                announcement = "Encounters";
                break;
            case ERunState.Loot:
                announcement = "Loot";
                break;
            case ERunState.LevelUp:
                int level = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Level) ?? 0;
                int skillCount = _nav.GetSelectionCardCount();
                announcement = skillCount > 0
                    ? $"Level up to {level}! Choose a skill, {skillCount} available"
                    : $"Level up to {level}!";
                break;
            case ERunState.Pedestal:
                var pedInfo = PedestalManager.GetCurrentPedestalInfo();
                if (pedInfo.Type == PedestalManager.PedestalType.Enchant ||
                    pedInfo.Type == PedestalManager.PedestalType.EnchantRandom)
                {
                    string enchName = pedInfo.EnchantmentName ?? "random";
                    announcement = $"Enchant altar, {enchName}";
                }
                else if (pedInfo.Type == PedestalManager.PedestalType.Upgrade)
                {
                    announcement = "Upgrade altar";
                }
                else
                {
                    announcement = "Altar";
                }
                break;
            case ERunState.Combat:
                announcement = "Combat";
                break;
            case ERunState.PVPCombat:
                announcement = "PvP";
                break;
            case ERunState.EndRunVictory:
                announcement = "Victory";
                break;
            case ERunState.EndRunDefeat:
                announcement = "Defeat";
                break;
            default:
                announcement = _nav.GetStateDescription();
                break;
        }

        TolkWrapper.Speak(announcement);

        if (runState != ERunState.Combat && runState != ERunState.PVPCombat)
        {
            _nav.TriggerVisualSelection();
        }
    }

    public void AnnounceSection()
    {
        if (_nav.CurrentSection == NavigationSection.Hero)
        {
            _nav.Hero.AnnounceSubsection();
            return;
        }

        int count = _nav.GetCurrentSectionCount();
        if (count == 0) return;

        string name = _nav.CurrentSection switch
        {
            NavigationSection.Selection => _nav.GetSelectionTypeName(),
            NavigationSection.Board => "Board",
            NavigationSection.Stash => "Stash",
            NavigationSection.Skills => "Skills",
            _ => "Unknown"
        };

        TolkWrapper.Speak($"{name}, {count} items");
        _nav.TriggerVisualSelection();
    }

    public void AnnounceCurrentItem()
    {
        if (_nav.CurrentSection == NavigationSection.Hero)
        {
            _nav.Hero.AnnounceStat();
            return;
        }

        if (_nav.CurrentSection == NavigationSection.Selection)
        {
            var navItem = _nav.GetCurrentNavItem();
            if (navItem == null)
            {
                TolkWrapper.Speak("Empty");
                return;
            }

            string desc;
            switch (navItem.Type)
            {
                case NavItemType.Exit:
                    desc = "Exit";
                    break;
                case NavItemType.Reroll:
                    desc = $"Refresh, {navItem.RerollCost} gold";
                    break;
                case NavItemType.Card:
                    desc = SelectionNavigator.GetCardDescription(navItem.Card, NavigationSection.Selection, SelectionNavigator.IsSelectionFree());
                    break;
                default:
                    desc = "Unknown";
                    break;
            }

            TolkWrapper.Speak(desc);
            return;
        }

        var card = _nav.GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        string cardDesc = SelectionNavigator.GetCardDescription(card, _nav.CurrentSection, false);
        TolkWrapper.Speak(cardDesc);
    }

    public void ReadDetailedInfo()
    {
        if (_nav.CurrentSection == NavigationSection.Hero)
        {
            _nav.Hero.ReadAllStats();
            return;
        }

        if (_nav.CurrentSection == NavigationSection.Selection)
        {
            var navItem = _nav.GetCurrentNavItem();
            if (navItem == null)
            {
                TolkWrapper.Speak("Nothing selected");
                return;
            }

            switch (navItem.Type)
            {
                case NavItemType.Exit:
                    TolkWrapper.Speak("Exit. Leave the current state and continue.");
                    return;
                case NavItemType.Reroll:
                    int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;
                    TolkWrapper.Speak($"Refresh. Get new items for {navItem.RerollCost} gold. You have {gold} gold.");
                    return;
                case NavItemType.Card:
                    var card = navItem.Card;
                    if (SelectionNavigator.IsEncounterCard(card))
                        TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(card));
                    else
                        TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
                    return;
            }
        }

        var currentCard = _nav.GetCurrentCard();
        if (currentCard == null)
        {
            TolkWrapper.Speak("Nothing selected");
            return;
        }

        if (SelectionNavigator.IsEncounterCard(currentCard))
            TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(currentCard));
        else
            TolkWrapper.Speak(ItemReader.GetDetailedDescription(currentCard));
    }

    public void AnnounceWins()
    {
        var wins = Data.Run?.Victories ?? 0;
        TolkWrapper.Speak($"{wins} wins");
    }

    public string GetCurrentItemSizeInfo()
    {
        var card = _nav.GetCurrentCard();
        if (card == null) return "No item selected";

        var template = card.Template;
        if (template == null) return "No size info";

        int size = (int)template.Size;
        string sizeName = template.Size switch
        {
            ECardSize.Small => "Small",
            ECardSize.Medium => "Medium",
            ECardSize.Large => "Large",
            _ => "Unknown"
        };

        return $"{ItemReader.GetCardName(card)}: Size {size} ({sizeName})";
    }

    public bool WillAutoExit()
    {
        try
        {
            bool canExit = Data.CurrentState?.SelectionContextRules?.CanExit ?? true;
            return !canExit;
        }
        catch
        {
            return false;
        }
    }

    // ===============================================
    // GAME ACTIONS
    // ===============================================

    public bool CanExit()
    {
        try
        {
            var state = AppState.CurrentState;
            if (state == null)
            {
                Plugin.Logger.LogInfo("CanExit: AppState.CurrentState is null");
                return false;
            }

            bool canHandle = state.CanHandleOperation(StateOps.ExitState);
            bool rulesAllow = Data.CurrentState?.SelectionContextRules?.CanExit ?? true;

            Plugin.Logger.LogInfo($"CanExit: canHandle={canHandle}, rulesAllow={rulesAllow}");

            if (!canHandle) return false;
            return rulesAllow;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"CanExit error: {ex.Message}");
            return false;
        }
    }

    public bool TryExit()
    {
        if (!CanExit())
        {
            TolkWrapper.Speak("Cannot exit now");
            return false;
        }

        try
        {
            AppState.CurrentState.ExitStateCommand();
            TolkWrapper.Speak("Exiting");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TryExit error: {ex.Message}");
            TolkWrapper.Speak("Exit failed");
            return false;
        }
    }

    public bool CanReroll()
    {
        try
        {
            var state = AppState.CurrentState;
            if (state == null) return false;
            if (!state.CanHandleOperation(StateOps.Reroll)) return false;

            var rerollCost = Data.CurrentState?.RerollCost;
            var rerollsRemaining = Data.CurrentState?.RerollsRemaining;

            if (!rerollCost.HasValue || rerollCost.Value < 0) return false;
            if (rerollsRemaining.HasValue && rerollsRemaining.Value == 0) return false;

            return true;
        }
        catch { return false; }
    }

    public int GetRerollCost()
    {
        return (int)(Data.CurrentState?.RerollCost ?? 0);
    }

    public bool TryReroll()
    {
        if (!CanReroll())
        {
            TolkWrapper.Speak("Cannot refresh now");
            return false;
        }

        int cost = GetRerollCost();
        int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;

        if (gold < cost)
        {
            TolkWrapper.Speak($"Not enough gold. Need {cost}, have {gold}");
            return false;
        }

        try
        {
            if (AppState.CurrentState.RerollCommand())
            {
                TolkWrapper.Speak($"Refreshed for {cost} gold");
                Plugin.Instance.StartCoroutine(DelayedRefresh());
                return true;
            }
            else
            {
                TolkWrapper.Speak("Refresh failed");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TryReroll error: {ex.Message}");
            TolkWrapper.Speak("Refresh failed");
            return false;
        }
    }

    private IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(0.5f);
        _nav.Refresh();
        AnnounceState();
    }

    public void AnnounceAvailableActions()
    {
        var actions = new List<string>();

        if (CanExit())
            actions.Add("E to exit");

        if (CanReroll())
        {
            int cost = GetRerollCost();
            actions.Add($"R to refresh ({cost} gold)");
        }

        if (actions.Count > 0)
            TolkWrapper.Speak(string.Join(", ", actions));
        else
            TolkWrapper.Speak("No actions available");
    }
}
