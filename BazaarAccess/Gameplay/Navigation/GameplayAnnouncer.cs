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
                announcement = Loc.T("nav.state.shop");
                break;
            case ERunState.Encounter:
                announcement = Loc.T("nav.state.encounters");
                break;
            case ERunState.Loot:
                announcement = Loc.T("nav.state.loot");
                break;
            case ERunState.LevelUp:
                int level = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Level) ?? 0;
                int skillCount = _nav.GetSelectionCardCount();
                announcement = skillCount > 0
                    ? Loc.Plural("nav.state.levelup_choose", skillCount, level, skillCount)
                    : Loc.T("nav.state.levelup", level);
                break;
            case ERunState.Pedestal:
                var pedInfo = PedestalManager.GetCurrentPedestalInfo();
                if (pedInfo.Type == PedestalManager.PedestalType.Enchant ||
                    pedInfo.Type == PedestalManager.PedestalType.EnchantRandom)
                {
                    string enchName = pedInfo.EnchantmentName ?? Loc.T("nav.state.random_enchant");
                    announcement = Loc.T("nav.state.enchant_altar", enchName);
                }
                else if (pedInfo.Type == PedestalManager.PedestalType.Upgrade)
                {
                    announcement = Loc.T("nav.state.upgrade_altar");
                }
                else
                {
                    announcement = Loc.T("nav.state.altar");
                }
                break;
            case ERunState.Combat:
                announcement = Loc.T("nav.state.combat");
                break;
            case ERunState.PVPCombat:
                announcement = Loc.T("nav.state.pvp");
                break;
            case ERunState.EndRunVictory:
                announcement = Loc.T("nav.state.victory");
                break;
            case ERunState.EndRunDefeat:
                announcement = Loc.T("nav.state.defeat");
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
            NavigationSection.Board => Loc.T("nav.section.board"),
            NavigationSection.Stash => Loc.T("nav.section.stash"),
            NavigationSection.Skills => Loc.T("nav.section.skills"),
            _ => Loc.T("nav.unknown")
        };

        // Free item encounters (patch 16.0) advertise a rarity but no price. The per-item tier
        // is already read; flag the selection as free so the missing price reads as intentional.
        // Loot rewards are already implicitly free, so skip the prefix there to avoid redundancy.
        // (Compares the run state directly rather than the now-localized "rewards" display name.)
        if (_nav.CurrentSection == NavigationSection.Selection
            && SelectionNavigator.IsSelectionFree()
            && _nav.GetCurrentState() != ERunState.Loot)
        {
            name = Loc.T("nav.section.free_prefix", name);
        }

        TolkWrapper.Speak(Loc.Plural("nav.section.count", count, name, count));
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
                TolkWrapper.Speak(Loc.T("nav.empty"));
                return;
            }

            string desc;
            switch (navItem.Type)
            {
                case NavItemType.Exit:
                    desc = Loc.T("nav.action.exit");
                    break;
                case NavItemType.Reroll:
                    desc = Loc.T("nav.action.reroll_cost", navItem.RerollCost);
                    break;
                case NavItemType.Card:
                    desc = SelectionNavigator.GetCardDescription(navItem.Card, NavigationSection.Selection, SelectionNavigator.IsSelectionFree());
                    break;
                default:
                    desc = Loc.T("nav.unknown");
                    break;
            }

            TolkWrapper.Speak(desc);
            return;
        }

        var card = _nav.GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak(Loc.T("nav.empty"));
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
                TolkWrapper.Speak(Loc.T("nav.nothing_selected"));
                return;
            }

            switch (navItem.Type)
            {
                case NavItemType.Exit:
                    TolkWrapper.Speak(Loc.T("nav.action.exit_detail"));
                    return;
                case NavItemType.Reroll:
                    int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;
                    TolkWrapper.Speak(Loc.T("nav.action.reroll_detail", navItem.RerollCost, gold));
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
            TolkWrapper.Speak(Loc.T("nav.nothing_selected"));
            return;
        }

        if (SelectionNavigator.IsEncounterCard(currentCard))
            TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(currentCard));
        else
            TolkWrapper.Speak(ItemReader.GetDetailedDescription(currentCard));
    }

    public void AnnounceWins()
    {
        int wins = (int)(Data.Run?.Victories ?? 0);
        TolkWrapper.Speak(Loc.Plural("nav.wins", wins, wins));
    }

    public string GetCurrentItemSizeInfo()
    {
        var card = _nav.GetCurrentCard();
        if (card == null) return Loc.T("nav.no_item_selected");

        var template = card.Template;
        if (template == null) return Loc.T("nav.item.no_size_info");

        int size = (int)template.Size;
        string sizeName = ItemReader.GetSizeName(template.Size);

        return Loc.T("nav.item.size_info", ItemReader.GetCardName(card), size, sizeName);
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
            TolkWrapper.Speak(Loc.T("nav.action.cannot_exit"));
            return false;
        }

        try
        {
            AppState.CurrentState.ExitStateCommand();
            TolkWrapper.Speak(Loc.T("nav.action.exiting"));
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TryExit error: {ex.Message}");
            TolkWrapper.Speak(Loc.T("nav.action.exit_failed"));
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
            TolkWrapper.Speak(Loc.T("nav.action.cannot_reroll"));
            return false;
        }

        int cost = GetRerollCost();
        int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;

        if (gold < cost)
        {
            TolkWrapper.Speak(Loc.T("nav.action.not_enough_gold", cost, gold));
            return false;
        }

        try
        {
            if (AppState.CurrentState.RerollCommand())
            {
                TolkWrapper.Speak(Loc.T("nav.action.rerolled", cost));
                Plugin.Instance.StartCoroutine(DelayedRefresh());
                return true;
            }
            else
            {
                TolkWrapper.Speak(Loc.T("nav.action.reroll_failed"));
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TryReroll error: {ex.Message}");
            TolkWrapper.Speak(Loc.T("nav.action.reroll_failed"));
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
            actions.Add(Loc.T("nav.action.exit_hint"));

        if (CanReroll())
        {
            int cost = GetRerollCost();
            actions.Add(Loc.T("nav.action.reroll_hint", cost));
        }

        if (actions.Count > 0)
            TolkWrapper.Speak(string.Join(", ", actions));
        else
            TolkWrapper.Speak(Loc.T("nav.action.none_available"));
    }
}
