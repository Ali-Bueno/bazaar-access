using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Navigation;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Gameplay navigator facade. Coordinates sub-navigators for board, selection,
/// hero, enemy, recap, detail, and announcements.
/// </summary>
public class GameplayNavigator
{
    private NavigationSection _currentSection = NavigationSection.Selection;
    private int _currentIndex = 0;

    // Mode flags
    private bool _inCombat = false;
    private bool _inReplayMode = false;
    private bool _inRecapMode = false;

    // Sub-navigators
    public readonly HeroNavigator Hero;
    public readonly EnemyNavigator Enemy;
    public readonly RecapNavigator Recap;
    public readonly DetailReader Detail;
    public readonly BoardStashNavigator Board;
    public readonly SelectionNavigator Selection;
    private readonly GameplayAnnouncer _announcer;

    public GameplayNavigator()
    {
        Hero = new HeroNavigator();
        Enemy = new EnemyNavigator();
        Detail = new DetailReader();
        Board = new BoardStashNavigator();
        Selection = new SelectionNavigator();
        _announcer = new GameplayAnnouncer(this);
        Recap = new RecapNavigator(Hero, Enemy, EnterRecapPlayerBoardInternal);

        Hero.OnSkillVisualSelect = (skillIndex) =>
        {
            VisualSelector.SelectHeroSkill(skillIndex);
        };
    }

    private void EnterRecapPlayerBoardInternal()
    {
        _currentSection = NavigationSection.Board;
        _currentIndex = 0;
        Detail.Clear();
        Board.RefreshBoard();

        if (Board.BoardCount == 0)
        {
            TolkWrapper.Speak("Your board is empty");
            return;
        }

        TolkWrapper.Speak($"Your board, {Board.BoardCount} items");
    }

    // ===============================================
    // PROPERTIES
    // ===============================================

    public NavigationSection CurrentSection => _currentSection;
    public bool IsInHeroSection => _currentSection == NavigationSection.Hero;
    public bool IsInCombat => _inCombat;
    public bool IsInReplayMode => _inReplayMode;
    public bool IsInRecapMode => _inRecapMode;

    // --- Hero delegates ---
    public HeroSubsection CurrentHeroSubsection => Hero.CurrentSubsection;
    public void HeroNext() => Hero.Next();
    public void HeroPrevious() => Hero.Previous();
    public void HeroNextSubsection() => Hero.NextSubsection();
    public void HeroPreviousSubsection() => Hero.PreviousSubsection();
    public Card GetCurrentHeroSkill() => Hero.GetCurrentSkill();
    public void ReadHeroSkillDetails() => Hero.ReadSkillDetails();
    public void ReadAllHeroStats() => Hero.ReadAllStats();

    // --- Enemy delegates ---
    public bool IsInEnemyMode => Enemy.IsActive;
    public EnemySubsection CurrentEnemySubsection => Enemy.CurrentSubsection;
    public bool IsInEnemySkillsSubsection => Enemy.IsInSkillsSubsection;
    public void ReadEnemyInfo() => Enemy.ReadInfo(_inCombat);
    public void ExitEnemyMode() => Enemy.Exit();
    public void ReadCurrentEnemyItemDetails() => Enemy.ReadCurrentItemDetails();
    public void EnemyNavigateRight() => Enemy.NavigateRight();
    public void EnemyNavigateLeft() => Enemy.NavigateLeft();
    public void EnemyNextSubsection() => Enemy.NextSubsection();
    public void EnemyPreviousSubsection() => Enemy.PreviousSubsection();
    public void EnemySkillNext() => Enemy.SkillNext();
    public void EnemySkillPrevious() => Enemy.SkillPrevious();
    public void EnemyDetailNext() => Enemy.DetailNext();
    public void EnemyDetailPrevious() => Enemy.DetailPrevious();
    public void EnemyNext() => Enemy.Next();
    public void EnemyPrevious() => Enemy.Previous();

    public void EnterOpponentBoardMode()
    {
        bool shouldSetRecap = Enemy.EnterBoardMode(_inRecapMode);
        if (shouldSetRecap)
            Recap.SetSection(RecapSection.EnemyBoard);
    }

    // --- Recap delegates ---
    public RecapSection CurrentRecapSection => Recap.CurrentSection;
    public void EnterRecapHeroMode() => Recap.EnterHeroMode();
    public void RecapHeroPrevious() => Recap.HeroPrevious();
    public void RecapHeroNext() => Recap.HeroNext();
    public void RecapHeroToStats() => Recap.HeroToStats();
    public void RecapHeroToSkills() => Recap.HeroToSkills();
    public void EnterRecapEnemyStatsMode() => Recap.EnterEnemyStatsMode();
    public void EnterCombatEnemyStatsMode() => Recap.EnterCombatEnemyStatsMode();
    public void RecapEnemyStatsPrevious() => Recap.EnemyStatsPrevious();
    public void RecapEnemyStatsNext() => Recap.EnemyStatsNext();
    public void RecapEnemyToStats() => Recap.EnemyToStats();
    public void RecapEnemyToSkills() => Recap.EnemyToSkills();
    public void EnterRecapPlayerBoardMode() => Recap.EnterPlayerBoardMode();
    public void EnterRecapCombatStatsMode() => Recap.EnterCombatStatsMode();
    public void RecapCombatStatsPrevious() => Recap.CombatStatsPrevious();
    public void RecapCombatStatsNext() => Recap.CombatStatsNext();

    // --- Detail delegates ---
    public void ClearDetailCache() => Detail.Clear();

    public void ReadDetailLineDown()
    {
        InitDetailLines();
        if (!Detail.HasLines) { TolkWrapper.Speak("No details"); return; }
        string text = Detail.LineDown();
        TolkWrapper.Speak(text ?? "No details");
    }

    public void ReadDetailLineUp()
    {
        InitDetailLines();
        if (!Detail.HasLines) { TolkWrapper.Speak("No details"); return; }
        string text = Detail.LineUp();
        TolkWrapper.Speak(text ?? "No details");
    }

    // --- Board/Stash delegates ---
    public bool IsStashOpen() => Board.IsStashOpen;
    public int GetStashItemCount() => Board.StashCount;
    public NavigationSection GetSectionBeforeStash() => Board.SectionBeforeStash;
    public void ToggleStash() => Board.ToggleStash();
    public void AnnounceBoardCapacity() => Board.AnnounceBoardCapacity();
    public void AnnounceStashCapacity() => Board.AnnounceStashCapacity();
    public Card GetItemAtSlot(int slot) => Board.GetItemAtSlot(slot);
    public bool HasBoardContent() => Board.BoardCount > 0;
    public bool HasSelectionContent() => Selection.HasContent;
    public int GetSelectionCardCount() => Selection.CardCount;

    public int GetCurrentBoardSlot()
    {
        if (_currentSection != NavigationSection.Board) return -1;
        return Board.GetCurrentBoardSlot(_currentIndex);
    }

    public bool GoToBoardSlot(int targetSlot) => Board.GoToBoardSlot(targetSlot, ref _currentIndex);
    public int GetCurrentStashSlot()
    {
        if (_currentSection != NavigationSection.Stash) return -1;
        return Board.GetCurrentStashSlot(_currentIndex);
    }
    public bool GoToStashSlot(int targetSlot) => Board.GoToStashSlot(targetSlot, ref _currentIndex);

    public bool GoToItemById(BazaarGameShared.Domain.Core.InstanceId instanceId)
        => Board.GoToItemById(instanceId, _currentSection, ref _currentIndex);

    // --- Announcer delegates ---
    public void AnnounceState() => _announcer.AnnounceState();
    public void AnnounceSection() => _announcer.AnnounceSection();
    public void AnnounceCurrentItem() => _announcer.AnnounceCurrentItem();
    public void ReadDetailedInfo() => _announcer.ReadDetailedInfo();
    public void AnnounceWins() => _announcer.AnnounceWins();
    public string GetCurrentItemSizeInfo() => _announcer.GetCurrentItemSizeInfo();
    public bool WillAutoExit() => _announcer.WillAutoExit();
    public bool CanExit() => _announcer.CanExit();
    public bool TryExit() => _announcer.TryExit();
    public bool CanReroll() => _announcer.CanReroll();
    public int GetRerollCost() => _announcer.GetRerollCost();
    public bool TryReroll() => _announcer.TryReroll();
    public void AnnounceAvailableActions() => _announcer.AnnounceAvailableActions();
    public string GetSelectionTypeName() => Selection.GetSelectionTypeName(GetCurrentState());

    // ===============================================
    // STATE QUERIES
    // ===============================================

    public ERunState GetCurrentState() => StateChangePatch.GetCurrentRunState();
    public string GetStateDescription() => StateChangePatch.GetStateDescription(GetCurrentState());

    // ===============================================
    // REFRESH
    // ===============================================

    public void Refresh()
    {
        Selection.Refresh(_announcer.CanReroll, _announcer.GetRerollCost, _announcer.CanExit);
        Board.RefreshBoard();
        Board.RefreshStash();
        Hero.Refresh();
        Detail.Clear();

        int count = GetCurrentSectionCount();
        if (count == 0)
            _currentIndex = 0;
        else if (_currentIndex >= count)
            _currentIndex = count - 1;
    }

    // ===============================================
    // SECTION NAVIGATION
    // ===============================================

    public void NextSection()
    {
        var sections = GetAvailableSections();
        if (sections.Count <= 1) return;

        int idx = sections.IndexOf(_currentSection);
        int nextIdx = (idx + 1) % sections.Count;
        _currentSection = sections[nextIdx];
        _currentIndex = 0;
        Hero.Reset();
        Detail.Clear();
        AnnounceSection();
    }

    public void GoToSection(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        Hero.Reset();
        Detail.Clear();
        AnnounceSection();
    }

    public void SetSectionSilent(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        Hero.Reset();
    }

    public void GoToChoices()
    {
        if (Selection.Count > 0)
            GoToSection(NavigationSection.Selection);
        else
            TolkWrapper.Speak("No choices available");
    }

    public void GoToBoard()
    {
        if (Board.BoardCount > 0)
            GoToSection(NavigationSection.Board);
        else if (Board.StashCount > 0)
        {
            GoToSection(NavigationSection.Stash);
            TolkWrapper.Speak("Board empty, showing stash");
        }
        else
            TolkWrapper.Speak("No items on board");
    }

    public void GoToHero() => GoToSection(NavigationSection.Hero);

    public void GoToStash()
    {
        if (!Board.IsStashOpen)
        {
            TolkWrapper.Speak("Stash is closed. Press Space to open.");
            return;
        }

        if (Board.StashCount > 0)
            GoToSection(NavigationSection.Stash);
        else
            TolkWrapper.Speak("Stash is empty");
    }

    // ===============================================
    // STATE MANAGEMENT
    // ===============================================

    public void SetCombatMode(bool inCombat)
    {
        _inCombat = inCombat;
        if (inCombat) GoToSection(NavigationSection.Hero);
    }

    public void SetReplayMode(bool inReplayMode)
    {
        _inReplayMode = inReplayMode;
        if (inReplayMode)
            _inCombat = false;
        else
            _inRecapMode = false;
    }

    public void SetRecapMode(bool inRecapMode)
    {
        _inRecapMode = inRecapMode;
        if (!inRecapMode) Recap.Reset();
    }

    public void SetStashState(bool isOpen) => Board.SetStashState(isOpen, _currentSection);

    // ===============================================
    // ITEM NAVIGATION
    // ===============================================

    public void Next()
    {
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        if (_currentIndex >= count - 1)
        {
            if (_currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            AnnounceCurrentItem();
            TriggerVisualSelection();
            return;
        }

        _currentIndex++;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void Previous()
    {
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        if (_currentIndex <= 0)
        {
            if (_currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            AnnounceCurrentItem();
            TriggerVisualSelection();
            return;
        }

        _currentIndex--;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void NavigateToFirst()
    {
        if (_currentSection == NavigationSection.Hero) return;
        if (GetCurrentSectionCount() == 0) return;
        _currentIndex = 0;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void NavigateToLast()
    {
        if (_currentSection == NavigationSection.Hero) return;
        int count = GetCurrentSectionCount();
        if (count == 0) return;
        _currentIndex = count - 1;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void NavigatePage(int direction)
    {
        if (_currentSection == NavigationSection.Hero) return;
        int count = GetCurrentSectionCount();
        if (count == 0) return;

        if (count <= 10)
        {
            if (direction < 0) NavigateToFirst(); else NavigateToLast();
            return;
        }

        int newIndex = _currentIndex + (direction * 10);

        if (newIndex < 0)
        {
            if (_currentIndex == 0 && _currentSection == NavigationSection.Board)
            { TolkWrapper.Speak("Start of list"); return; }
            newIndex = 0;
        }
        if (newIndex >= count)
        {
            if (_currentIndex == count - 1 && _currentSection == NavigationSection.Board)
            { TolkWrapper.Speak("End of list"); return; }
            newIndex = count - 1;
        }

        _currentIndex = newIndex;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    // ===============================================
    // DATA ACCESS
    // ===============================================

    public NavItem GetCurrentNavItem()
    {
        if (_currentSection != NavigationSection.Selection) return null;
        return Selection.GetNavItem(_currentIndex);
    }

    /// <summary>
    /// Gets the card at a specific selection index (for state verification).
    /// </summary>
    internal Card GetCardAtSelectionIndex(int index) => Selection.GetCard(index);

    public Card GetCurrentCard()
    {
        switch (_currentSection)
        {
            case NavigationSection.Selection:
                return Selection.GetCard(_currentIndex);
            case NavigationSection.Board:
                return Board.GetBoardCard(_currentIndex);
            case NavigationSection.Stash:
                return Board.GetStashCard(_currentIndex);
            case NavigationSection.Skills:
                if (_currentIndex < Hero.Skills.Count)
                    return Hero.Skills[_currentIndex];
                break;
        }
        return null;
    }

    public bool IsInSelectionSection() => _currentSection == NavigationSection.Selection;
    public bool IsInPlayerSection() => _currentSection == NavigationSection.Board ||
                                        _currentSection == NavigationSection.Stash;
    public bool IsInBoardSection() => _currentSection == NavigationSection.Board;
    public bool IsInStashSection() => _currentSection == NavigationSection.Stash;
    public bool IsSelectionFree() => SelectionNavigator.IsSelectionFree();
    public bool HasContent() => GetCurrentSectionCount() > 0;

    public bool CanSellInCurrentState()
    {
        var state = AppState.CurrentState;
        return state?.CanHandleOperation(StateOps.SellItem) ?? false;
    }

    public bool CanMoveInCurrentState()
    {
        var state = AppState.CurrentState;
        return state?.CanHandleOperation(StateOps.MoveItem) ?? false;
    }

    // ===============================================
    // INTERNAL HELPERS
    // ===============================================

    internal int GetCurrentSectionCount() => _currentSection switch
    {
        NavigationSection.Selection => Selection.Count,
        NavigationSection.Board => Board.BoardCount,
        NavigationSection.Stash => Board.StashCount,
        NavigationSection.Skills => Hero.Skills.Count,
        NavigationSection.Hero => Hero.GetStatCount(),
        _ => 0
    };

    private List<NavigationSection> GetAvailableSections()
    {
        var list = new List<NavigationSection>();

        if (_inCombat)
        {
            list.Add(NavigationSection.Hero);
            return list;
        }

        if (Selection.Count > 0) list.Add(NavigationSection.Selection);
        if (Board.BoardCount > 0) list.Add(NavigationSection.Board);
        if (Board.IsStashOpen && Board.StashCount > 0) list.Add(NavigationSection.Stash);
        if (Hero.Skills.Count > 0) list.Add(NavigationSection.Skills);
        list.Add(NavigationSection.Hero);
        return list;
    }

    private void InitDetailLines()
    {
        var card = GetCurrentCard();

        if (card != null)
        {
            Detail.Init(card, c => ItemReader.GetDetailLines(c));
        }
        else
        {
            if (Detail.CurrentCard == null && !Detail.HasLines)
            {
                var navItem = GetCurrentNavItem();
                if (navItem != null)
                {
                    var lines = new List<string>();
                    switch (navItem.Type)
                    {
                        case NavItemType.Exit:
                            lines.Add("Exit");
                            lines.Add("Leave the current state and continue");
                            break;
                        case NavItemType.Reroll:
                            int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;
                            lines.Add($"Refresh: {navItem.RerollCost} gold");
                            lines.Add($"Your gold: {gold}");
                            break;
                    }
                    Detail.InitCustom(lines);
                }
            }
        }
    }

    public void TriggerVisualSelection()
    {
        try
        {
            CardController controller = null;

            switch (_currentSection)
            {
                case NavigationSection.Selection:
                    var navItem = GetCurrentNavItem();
                    if (navItem?.Type == NavItemType.Card && navItem.Card != null)
                    {
                        var bm = BoardStashNavigator.GetBoardManager();
                        if (bm != null)
                            controller = VisualSelector.FindCardController(navItem.Card, bm);
                    }
                    break;

                case NavigationSection.Board:
                    controller = Board.GetBoardCardController(_currentIndex);
                    break;

                case NavigationSection.Stash:
                    controller = Board.GetStashCardController(_currentIndex);
                    break;

                case NavigationSection.Skills:
                    if (_currentIndex < Hero.Skills.Count)
                    {
                        var bm = BoardStashNavigator.GetBoardManager();
                        if (bm?.playerSkillSockets != null && _currentIndex < bm.playerSkillSockets.Length)
                        {
                            controller = bm.playerSkillSockets[_currentIndex]?.CardController;
                        }
                    }
                    break;
            }

            if (controller != null)
            {
                VisualSelector.SelectSocket(controller);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerVisualSelection error: {ex.Message}");
        }
    }
}
