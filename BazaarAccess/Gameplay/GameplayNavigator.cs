using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Navigation;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Current navigation section.
/// </summary>
public enum NavigationSection
{
    Selection,  // What the game offers (encounters, shop, rewards) + actions
    Board,      // Your equipped items
    Stash,      // Your storage
    Skills,     // Your skills
    Hero        // Hero stats
}

/// <summary>
/// Subsections within Hero.
/// </summary>
public enum HeroSubsection
{
    Stats,      // Hero stats (health, gold, level, etc.)
    Skills      // Equipped hero skills
}

/// <summary>
/// Subsections within enemy mode.
/// </summary>
public enum EnemySubsection
{
    Items,      // Enemy items
    Skills      // Enemy skills
}

/// <summary>
/// Sections in recap mode (post-combat with E key).
/// </summary>
public enum RecapSection
{
    None,           // Not in any recap section
    HeroStats,      // Own hero stats (V)
    HeroSkills,     // Own hero skills (V + Right)
    EnemyStats,     // Enemy hero stats (F)
    EnemySkills,    // Enemy hero skills (F + Right)
    EnemyBoard,     // Enemy board (G)
    PlayerBoard,    // Own board (B)
    CombatStats     // Per-card combat stats (H)
}

/// <summary>
/// Types of navigable items (cards or actions).
/// </summary>
public enum NavItemType
{
    Card,       // A normal card
    Exit,       // Exit action
    Reroll      // Refresh action
}

/// <summary>
/// Navigable item (can be card or action).
/// </summary>
public class NavItem
{
    public NavItemType Type { get; set; }
    public Card Card { get; set; }
    public int RerollCost { get; set; }

    public static NavItem FromCard(Card card) => new NavItem { Type = NavItemType.Card, Card = card };
    public static NavItem CreateExit() => new NavItem { Type = NavItemType.Exit };
    public static NavItem CreateReroll(int cost) => new NavItem { Type = NavItemType.Reroll, RerollCost = cost };
}

/// <summary>
/// Gameplay navigator faithful to the original game flow.
/// - Selection: what you can choose (encounters, shop items)
/// - Board: your items (can sell, move)
/// Delegates hero, enemy, recap, and detail navigation to sub-navigators.
/// </summary>
public class GameplayNavigator
{
    private NavigationSection _currentSection = NavigationSection.Selection;
    private int _currentIndex = 0;

    // Cache of navigable items (cards + actions)
    private List<NavItem> _selectionItems = new List<NavItem>();  // SelectionSet + Exit/Reroll
    private List<int> _boardIndices = new List<int>();        // Occupied board slots
    private List<int> _stashIndices = new List<int>();        // Occupied stash slots

    // Combat mode
    private bool _inCombat = false;

    // Stash state (open/closed)
    private bool _stashOpen = false;

    // Previous section before opening stash (to restore on close)
    private NavigationSection _sectionBeforeStash = NavigationSection.Selection;

    // Replay mode (post-combat)
    private bool _inReplayMode = false;
    private bool _inRecapMode = false;

    // Sub-navigators
    public readonly HeroNavigator Hero;
    public readonly EnemyNavigator Enemy;
    public readonly RecapNavigator Recap;
    public readonly DetailReader Detail;

    public GameplayNavigator()
    {
        Hero = new HeroNavigator();
        Enemy = new EnemyNavigator();
        Detail = new DetailReader();
        Recap = new RecapNavigator(Hero, Enemy, EnterRecapPlayerBoardInternal);

        // Wire up hero skill visual selection callback
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
        RefreshBoard();

        if (_boardIndices.Count == 0)
        {
            TolkWrapper.Speak("Your board is empty");
            return;
        }

        TolkWrapper.Speak($"Your board, {_boardIndices.Count} items");
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

        if (!Detail.HasLines)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        string text = Detail.LineDown();
        if (text != null)
            TolkWrapper.Speak(text);
        else
            TolkWrapper.Speak("No details");
    }

    public void ReadDetailLineUp()
    {
        InitDetailLines();

        if (!Detail.HasLines)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        string text = Detail.LineUp();
        if (text != null)
            TolkWrapper.Speak(text);
        else
            TolkWrapper.Speak("No details");
    }

    // ===============================================
    // STATE QUERIES
    // ===============================================

    public ERunState GetCurrentState()
    {
        return StateChangePatch.GetCurrentRunState();
    }

    public string GetStateDescription()
    {
        return StateChangePatch.GetStateDescription(GetCurrentState());
    }

    // ===============================================
    // REFRESH
    // ===============================================

    /// <summary>
    /// Updates all card lists.
    /// </summary>
    public void Refresh()
    {
        RefreshSelection();
        RefreshBoard();
        RefreshStash();
        Hero.Refresh();

        // Clear detail cache to prevent stale card references
        // This is important after items are moved, sold, or upgraded
        Detail.Clear();

        // Just adjust index if out of range
        int count = GetCurrentSectionCount();
        if (count == 0)
        {
            _currentIndex = 0;
        }
        else if (_currentIndex >= count)
        {
            _currentIndex = count - 1; // Stay on last item instead of jumping to first
        }
    }

    private void RefreshSelection()
    {
        _selectionItems.Clear();
        try
        {
            // Add cards from SelectionSet
            var selectionSet = Data.CurrentState?.SelectionSet;
            if (selectionSet != null)
            {
                foreach (var id in selectionSet)
                {
                    var card = Data.GetCard(id);
                    if (card != null) _selectionItems.Add(NavItem.FromCard(card));
                }
            }

            // Add available actions at the end
            if (CanReroll())
            {
                _selectionItems.Add(NavItem.CreateReroll(GetRerollCost()));
            }

            if (CanExit())
            {
                _selectionItems.Add(NavItem.CreateExit());
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"RefreshSelection error: {ex.Message}");
        }
    }

    private void RefreshBoard()
    {
        _boardIndices.Clear();
        var bm = GetBoardManager();
        if (bm?.playerItemSockets == null) return;

        // Track which items we've already seen to avoid duplicates
        // (large items occupy multiple slots but should only appear once)
        var seenItems = new HashSet<BazaarGameShared.Domain.Core.InstanceId>();

        for (int i = 0; i < bm.playerItemSockets.Length; i++)
        {
            var card = bm.playerItemSockets[i]?.CardController?.CardData;
            if (card != null)
            {
                // Only add the first slot of each item
                if (!seenItems.Contains(card.InstanceId))
                {
                    seenItems.Add(card.InstanceId);
                    _boardIndices.Add(i);
                }
            }
        }
    }

    private void RefreshStash()
    {
        _stashIndices.Clear();
        var bm = GetBoardManager();

        if (bm?.playerStorageSockets != null)
        {
            Plugin.Logger.LogInfo($"RefreshStash: playerStorageSockets.Length = {bm.playerStorageSockets.Length}");
            for (int i = 0; i < bm.playerStorageSockets.Length; i++)
            {
                var socket = bm.playerStorageSockets[i];
                if (socket?.CardController?.CardData != null)
                {
                    _stashIndices.Add(i);
                    Plugin.Logger.LogInfo($"RefreshStash: Found item at index {i}: {socket.CardController.CardData}");
                }
            }
        }
        else
        {
            Plugin.Logger.LogWarning("RefreshStash: playerStorageSockets is null");
        }

        Plugin.Logger.LogInfo($"RefreshStash: Total {_stashIndices.Count} items");
    }

    private Card GetStashSocketCard(BoardManager bm, int idx)
    {
        if (bm?.playerStorageSockets != null && idx < bm.playerStorageSockets.Length)
        {
            return bm.playerStorageSockets[idx]?.CardController?.CardData;
        }
        return null;
    }

    // ===============================================
    // NAVIGATION
    // ===============================================

    /// <summary>
    /// Switches to the next section with content.
    /// </summary>
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

    /// <summary>
    /// Goes directly to a specific section.
    /// </summary>
    public void GoToSection(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        Hero.Reset();
        Detail.Clear();
        AnnounceSection();
    }

    /// <summary>
    /// Switches to a section without announcing (for internal use).
    /// </summary>
    public void SetSectionSilent(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        Hero.Reset();
    }

    /// <summary>
    /// Goes to the choices/selection section.
    /// </summary>
    public void GoToChoices()
    {
        if (_selectionItems.Count > 0)
        {
            GoToSection(NavigationSection.Selection);
        }
        else
        {
            TolkWrapper.Speak("No choices available");
        }
    }

    /// <summary>
    /// Goes to the board section.
    /// </summary>
    public void GoToBoard()
    {
        if (_boardIndices.Count > 0)
        {
            GoToSection(NavigationSection.Board);
        }
        else if (_stashIndices.Count > 0)
        {
            GoToSection(NavigationSection.Stash);
            TolkWrapper.Speak("Board empty, showing stash");
        }
        else
        {
            TolkWrapper.Speak("No items on board");
        }
    }

    /// <summary>
    /// Goes to the hero section.
    /// </summary>
    public void GoToHero()
    {
        GoToSection(NavigationSection.Hero);
    }

    /// <summary>
    /// Activates or deactivates combat mode.
    /// In combat only Hero navigation is allowed.
    /// </summary>
    public void SetCombatMode(bool inCombat)
    {
        _inCombat = inCombat;
        if (inCombat)
        {
            // In combat, force go to Hero
            GoToSection(NavigationSection.Hero);
        }
    }

    /// <summary>
    /// Activates or deactivates replay mode (post-combat).
    /// </summary>
    public void SetReplayMode(bool inReplayMode)
    {
        _inReplayMode = inReplayMode;
        if (inReplayMode)
        {
            // Exit combat mode when entering replay
            _inCombat = false;
        }
        else
        {
            // When leaving replay, also leave recap
            _inRecapMode = false;
        }
    }

    /// <summary>
    /// Activates recap mode (after pressing E in ReplayState).
    /// </summary>
    public void SetRecapMode(bool inRecapMode)
    {
        _inRecapMode = inRecapMode;
        if (!inRecapMode)
        {
            Recap.Reset();
        }
    }

    /// <summary>
    /// Updates the stash state (open/closed).
    /// </summary>
    public void SetStashState(bool isOpen)
    {
        if (isOpen && !_stashOpen)
        {
            // Save current section before opening stash
            _sectionBeforeStash = _currentSection;
        }

        _stashOpen = isOpen;

        if (isOpen)
        {
            // Refresh the stash when opened
            RefreshStash();
        }
        else
        {
            // Stash closed - just clear the stash indices
            // Section change is handled by GameplayScreen.OnStorageToggled
            _stashIndices.Clear();
        }
    }

    /// <summary>
    /// Gets the section to return to when stash closes.
    /// </summary>
    public NavigationSection GetSectionBeforeStash() => _sectionBeforeStash;

    /// <summary>
    /// Opens/closes the stash and navigates to it if open.
    /// </summary>
    public void ToggleStash()
    {
        try
        {
            var bm = GetBoardManager();
            if (bm == null)
            {
                TolkWrapper.Speak("Not available");
                return;
            }

            // Check if we can interact
            if (!bm.AllowInteraction)
            {
                TolkWrapper.Speak("Cannot open stash now");
                return;
            }

            // Call the game method to open/close the stash
            bm.TryToggleStorage();
            // The StorageToggled event will fire and OnStorageToggled will handle the rest
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ToggleStash error: {ex.Message}");
            TolkWrapper.Speak("Cannot toggle stash");
        }
    }

    /// <summary>
    /// Goes to the stash section (without opening/closing).
    /// </summary>
    public void GoToStash()
    {
        if (!_stashOpen)
        {
            TolkWrapper.Speak("Stash is closed. Press Space to open.");
            return;
        }

        if (_stashIndices.Count > 0)
        {
            GoToSection(NavigationSection.Stash);
        }
        else
        {
            TolkWrapper.Speak("Stash is empty");
        }
    }

    private List<NavigationSection> GetAvailableSections()
    {
        var list = new List<NavigationSection>();

        // In combat only allow Hero
        if (_inCombat)
        {
            list.Add(NavigationSection.Hero);
            return list;
        }

        if (_selectionItems.Count > 0) list.Add(NavigationSection.Selection);
        if (_boardIndices.Count > 0) list.Add(NavigationSection.Board);
        // Only include Stash if open
        if (_stashOpen && _stashIndices.Count > 0) list.Add(NavigationSection.Stash);
        if (Hero.Skills.Count > 0) list.Add(NavigationSection.Skills);
        list.Add(NavigationSection.Hero); // Hero always available
        return list;
    }

    public void Next()
    {
        // In Hero, don't use normal arrows - use Ctrl+Up/Down
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        // No wrap - stay at end
        if (_currentIndex >= count - 1)
        {
            // Only Board shows limit messages, others just read current item
            if (_currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            // For other sections, just read the last item again
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
        // In Hero, don't use normal arrows - use Ctrl+Up/Down
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        // No wrap - stay at start
        if (_currentIndex <= 0)
        {
            // Only Board shows limit messages, others just read current item
            if (_currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            // For other sections, just read the first item again
            AnnounceCurrentItem();
            TriggerVisualSelection();
            return;
        }

        _currentIndex--;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    /// <summary>
    /// Navigate to the first item in the current section.
    /// </summary>
    public void NavigateToFirst()
    {
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        _currentIndex = 0;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    /// <summary>
    /// Navigate to the last item in the current section.
    /// </summary>
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

    /// <summary>
    /// Navigate by page (10 items at a time).
    /// </summary>
    public void NavigatePage(int direction)
    {
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;

        // For small lists, just go to start/end
        if (count <= 10)
        {
            if (direction < 0)
                NavigateToFirst();
            else
                NavigateToLast();
            return;
        }

        int newIndex = _currentIndex + (direction * 10);

        // Clamp to bounds - only Board shows limit messages
        if (newIndex < 0)
        {
            if (_currentIndex == 0 && _currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            newIndex = 0;
        }
        if (newIndex >= count)
        {
            if (_currentIndex == count - 1 && _currentSection == NavigationSection.Board)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            newIndex = count - 1;
        }

        _currentIndex = newIndex;
        Detail.Clear();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    // ===============================================
    // ANNOUNCEMENTS
    // ===============================================

    /// <summary>
    /// Verifies that the reported state matches the actual content.
    /// Fixes cases where the game reports Choice/Shop but content is encounters.
    /// </summary>
    private ERunState VerifyStateMatchesContent(ERunState reportedState)
    {
        // Only verify if state says Choice (Shop) - this is the problematic case
        if (reportedState != ERunState.Choice)
        {
            return reportedState;
        }

        // Check what's actually in the selection
        var cards = _selectionItems.Where(i => i.Type == NavItemType.Card).ToList();
        if (cards.Count == 0)
        {
            return reportedState; // No cards, trust the state
        }

        // Check if content is encounters
        var firstCard = cards[0].Card;
        if (IsEncounterCard(firstCard))
        {
            Plugin.Logger.LogInfo($"VerifyStateMatchesContent: State says Choice but content is encounters, correcting to Encounter");
            return ERunState.Encounter;
        }

        // Check if content is skills (LevelUp)
        if (firstCard.Type == ECardType.Skill)
        {
            Plugin.Logger.LogInfo($"VerifyStateMatchesContent: State says Choice but content is skills, correcting to LevelUp");
            return ERunState.LevelUp;
        }

        // Content is items, Choice/Shop is correct
        return reportedState;
    }

    /// <summary>
    /// Announces the current state simply.
    /// Only says the state name, no extra details.
    /// </summary>
    public void AnnounceState()
    {
        Refresh();

        var runState = GetCurrentState();

        // Verify state matches actual content - fix for incorrect "Shop" announcements
        runState = VerifyStateMatchesContent(runState);

        // Simplified announcement with relevant info
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
                // For level up, include current level and number of available skills
                int level = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Level) ?? 0;
                int skillCount = GetSelectionCardCount();
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
                announcement = GetStateDescription();
                break;
        }

        TolkWrapper.Speak(announcement);

        // Trigger visual selection of the first item (if not in combat)
        if (runState != ERunState.Combat && runState != ERunState.PVPCombat)
        {
            TriggerVisualSelection();
        }
    }

    private string GetSelectionTypeName()
    {
        // Count only cards (not Exit/Reroll)
        var cards = _selectionItems.Where(i => i.Type == NavItemType.Card).ToList();
        if (cards.Count == 0) return "options";

        // In Loot state, they are rewards
        var state = GetCurrentState();
        if (state == ERunState.Loot) return "rewards";

        var firstCard = cards[0].Card;
        if (IsEncounterCard(firstCard)) return "encounters";
        if (firstCard.Type == ECardType.Skill) return "skills";
        return "items";
    }

    /// <summary>
    /// Checks if the current state auto-exits after selecting.
    /// </summary>
    public bool WillAutoExit()
    {
        try
        {
            // If you can't exit manually, it's because it auto-exits
            bool canExit = Data.CurrentState?.SelectionContextRules?.CanExit ?? true;
            return !canExit;
        }
        catch
        {
            return false;
        }
    }

    public void AnnounceSection()
    {
        if (_currentSection == NavigationSection.Hero)
        {
            Hero.AnnounceSubsection();
            return;
        }

        int count = GetCurrentSectionCount();

        // Don't announce empty sections
        if (count == 0) return;

        string name = _currentSection switch
        {
            NavigationSection.Selection => GetSelectionTypeName(),
            NavigationSection.Board => "Board",
            NavigationSection.Stash => "Stash",
            NavigationSection.Skills => "Skills",
            _ => "Unknown"
        };

        // Only announce section name + count, not the item
        // User will hear the item when they press arrow keys
        TolkWrapper.Speak($"{name}, {count} items");
        TriggerVisualSelection();
    }

    public void AnnounceCurrentItem()
    {
        int pos = _currentIndex + 1;
        int total = GetCurrentSectionCount();

        // If in Hero, announce stat
        if (_currentSection == NavigationSection.Hero)
        {
            Hero.AnnounceStat();
            return;
        }

        // If in Selection, can be card or action
        if (_currentSection == NavigationSection.Selection)
        {
            var navItem = GetCurrentNavItem();
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
                    desc = GetCardDescription(navItem.Card);
                    break;
                default:
                    desc = "Unknown";
                    break;
            }

            TolkWrapper.Speak(desc);
            return;
        }

        // For other sections (Board, Stash, Skills)
        var card = GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        string cardDesc = GetCardDescription(card);
        TolkWrapper.Speak(cardDesc);
    }

    public void ReadDetailedInfo()
    {
        if (_currentSection == NavigationSection.Hero)
        {
            Hero.ReadAllStats();
            return;
        }

        // If in Selection, can be NavItem
        if (_currentSection == NavigationSection.Selection)
        {
            var navItem = GetCurrentNavItem();
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
                    if (IsEncounterCard(card))
                        TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(card));
                    else
                        TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
                    return;
            }
        }

        // For other sections
        var currentCard = GetCurrentCard();
        if (currentCard == null)
        {
            TolkWrapper.Speak("Nothing selected");
            return;
        }

        if (IsEncounterCard(currentCard))
            TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(currentCard));
        else
            TolkWrapper.Speak(ItemReader.GetDetailedDescription(currentCard));
    }

    /// <summary>
    /// Announces wins for the current run.
    /// </summary>
    public void AnnounceWins()
    {
        var wins = Data.Run?.Victories ?? 0;
        TolkWrapper.Speak($"{wins} wins");
    }

    /// <summary>
    /// Announces the board capacity information.
    /// Shows: used slots / total unlocked slots, and free space.
    /// </summary>
    public void AnnounceBoardCapacity()
    {
        try
        {
            var player = Data.Run?.Player;
            if (player?.Hand?.Container == null)
            {
                TolkWrapper.Speak("Board info not available");
                return;
            }

            var container = player.Hand.Container;

            // Count unlocked sockets
            int unlockedCount = 0;
            for (int i = 0; i < 10; i++)
            {
                if (!container.IsSocketLocked((EContainerSocketId)i))
                {
                    unlockedCount++;
                }
            }

            // Count used capacity (considering item sizes)
            // GetSocketableList returns unique items
            var socketables = container.GetSocketableList();
            int usedCapacity = 0;
            foreach (var socketable in socketables)
            {
                usedCapacity += (int)socketable.Size;
            }

            // Free slots
            int freeSlots = container.CountEmptySockets();

            // Item count
            int itemCount = socketables.Count;

            var parts = new List<string>();
            parts.Add($"Board: {usedCapacity} of {unlockedCount} capacity used");
            parts.Add($"{itemCount} items");
            parts.Add($"{freeSlots} slots free");

            TolkWrapper.Speak(string.Join(", ", parts));
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"AnnounceBoardCapacity error: {ex.Message}");
            TolkWrapper.Speak("Cannot read board info");
        }
    }

    /// <summary>
    /// Announces the stash capacity and item count.
    /// </summary>
    public void AnnounceStashCapacity()
    {
        try
        {
            var player = Data.Run?.Player;
            if (player?.Stash?.Container == null)
            {
                TolkWrapper.Speak("Stash info not available");
                return;
            }

            var container = player.Stash.Container;

            // Stash has 10 fixed slots
            int totalSlots = 10;

            // Count used capacity (considering item sizes)
            var socketables = container.GetSocketableList();
            int usedCapacity = 0;
            foreach (var socketable in socketables)
            {
                usedCapacity += (int)socketable.Size;
            }

            // Free slots
            int freeSlots = container.CountEmptySockets();

            // Item count
            int itemCount = socketables.Count;

            var parts = new List<string>();
            parts.Add($"Stash: {usedCapacity} of {totalSlots} capacity used");
            parts.Add($"{itemCount} items");
            parts.Add($"{freeSlots} slots free");

            TolkWrapper.Speak(string.Join(", ", parts));
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"AnnounceStashCapacity error: {ex.Message}");
            TolkWrapper.Speak("Cannot read stash info");
        }
    }

    /// <summary>
    /// Gets a description of the current item including its size in slots.
    /// </summary>
    public string GetCurrentItemSizeInfo()
    {
        var card = GetCurrentCard();
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

    // ===============================================
    // GAME ACTIONS
    // ===============================================

    /// <summary>
    /// Checks if we can exit the current state.
    /// </summary>
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

    /// <summary>
    /// Executes the exit command for the current state.
    /// </summary>
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

    /// <summary>
    /// Checks if we can reroll.
    /// </summary>
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

    /// <summary>
    /// Gets the reroll cost.
    /// </summary>
    public int GetRerollCost()
    {
        return (int)(Data.CurrentState?.RerollCost ?? 0);
    }

    /// <summary>
    /// Executes the reroll command.
    /// </summary>
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
                // Refresh after reroll
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

    private System.Collections.IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(0.5f);
        Refresh();
        AnnounceState();
    }

    /// <summary>
    /// Announces available actions (Exit, Reroll).
    /// </summary>
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

    // ===============================================
    // DATA ACCESS
    // ===============================================

    /// <summary>
    /// Gets the current NavItem in the Selection section.
    /// </summary>
    public NavItem GetCurrentNavItem()
    {
        if (_currentSection != NavigationSection.Selection) return null;
        if (_currentIndex < 0 || _currentIndex >= _selectionItems.Count) return null;
        return _selectionItems[_currentIndex];
    }

    public Card GetCurrentCard()
    {
        var bm = GetBoardManager();

        switch (_currentSection)
        {
            case NavigationSection.Selection:
                if (_currentIndex < _selectionItems.Count)
                {
                    var navItem = _selectionItems[_currentIndex];
                    return navItem.Type == NavItemType.Card ? navItem.Card : null;
                }
                break;

            case NavigationSection.Board:
                if (_currentIndex < _boardIndices.Count && bm != null)
                {
                    int idx = _boardIndices[_currentIndex];
                    return bm.playerItemSockets[idx]?.CardController?.CardData;
                }
                break;

            case NavigationSection.Stash:
                if (_currentIndex < _stashIndices.Count && bm != null)
                {
                    int idx = _stashIndices[_currentIndex];
                    return GetStashSocketCard(bm, idx);
                }
                break;

            case NavigationSection.Skills:
                if (_currentIndex < Hero.Skills.Count)
                {
                    return Hero.Skills[_currentIndex];
                }
                break;
        }
        return null;
    }

    public bool IsInSelectionSection() => _currentSection == NavigationSection.Selection;
    public bool IsInPlayerSection() => _currentSection == NavigationSection.Board ||
                                        _currentSection == NavigationSection.Stash;
    public bool IsInBoardSection() => _currentSection == NavigationSection.Board;
    public bool IsInStashSection() => _currentSection == NavigationSection.Stash;

    /// <summary>
    /// Gets the index of the current slot on the board.
    /// </summary>
    public int GetCurrentBoardSlot()
    {
        if (_currentSection != NavigationSection.Board) return -1;
        if (_currentIndex < 0 || _currentIndex >= _boardIndices.Count) return -1;
        return _boardIndices[_currentIndex];
    }

    /// <summary>
    /// Adjusts the navigator index to point to the specified board slot.
    /// Used after reordering to follow the moved item.
    /// </summary>
    /// <param name="targetSlot">The slot to navigate to</param>
    /// <returns>True if the slot was found</returns>
    public bool GoToBoardSlot(int targetSlot)
    {
        if (_currentSection != NavigationSection.Board) return false;

        for (int i = 0; i < _boardIndices.Count; i++)
        {
            if (_boardIndices[i] == targetSlot)
            {
                _currentIndex = i;
                return true;
            }
        }

        // Slot not found - log warning and stay on current valid index
        Plugin.Logger.LogWarning($"GoToBoardSlot: targetSlot {targetSlot} not found in _boardIndices. Count={_boardIndices.Count}");

        // Ensure current index is still valid
        if (_boardIndices.Count > 0 && _currentIndex >= _boardIndices.Count)
        {
            _currentIndex = _boardIndices.Count - 1;
        }
        return false;
    }

    /// <summary>
    /// Finds and navigates to an item by its InstanceId.
    /// More reliable than slot-based navigation after items are moved.
    /// Works for both Board and Stash sections.
    /// </summary>
    /// <param name="instanceId">The InstanceId of the item to find</param>
    /// <returns>True if the item was found</returns>
    public bool GoToItemById(BazaarGameShared.Domain.Core.InstanceId instanceId)
    {
        var bm = GetBoardManager();

        if (_currentSection == NavigationSection.Board)
        {
            if (bm?.playerItemSockets == null) return false;

            for (int i = 0; i < _boardIndices.Count; i++)
            {
                int slot = _boardIndices[i];
                var card = bm.playerItemSockets[slot]?.CardController?.CardData;
                if (card != null && card.InstanceId == instanceId)
                {
                    _currentIndex = i;
                    return true;
                }
            }

            Plugin.Logger.LogWarning($"GoToItemById: item with InstanceId not found on board");
            return false;
        }

        if (_currentSection == NavigationSection.Stash)
        {
            if (bm?.playerStorageSockets == null) return false;

            for (int i = 0; i < _stashIndices.Count; i++)
            {
                int slot = _stashIndices[i];
                var card = bm.playerStorageSockets[slot]?.CardController?.CardData;
                if (card != null && card.InstanceId == instanceId)
                {
                    _currentIndex = i;
                    return true;
                }
            }

            Plugin.Logger.LogWarning($"GoToItemById: item with InstanceId not found in stash");
            return false;
        }

        return false;
    }

    /// <summary>
    /// Gets the current slot index in the stash.
    /// </summary>
    public int GetCurrentStashSlot()
    {
        if (_currentSection != NavigationSection.Stash) return -1;
        if (_currentIndex < 0 || _currentIndex >= _stashIndices.Count) return -1;
        return _stashIndices[_currentIndex];
    }

    /// <summary>
    /// Navigates to a specific stash slot.
    /// </summary>
    public bool GoToStashSlot(int targetSlot)
    {
        if (_currentSection != NavigationSection.Stash) return false;

        for (int i = 0; i < _stashIndices.Count; i++)
        {
            if (_stashIndices[i] == targetSlot)
            {
                _currentIndex = i;
                return true;
            }
        }

        Plugin.Logger.LogWarning($"GoToStashSlot: targetSlot {targetSlot} not found");
        if (_stashIndices.Count > 0 && _currentIndex >= _stashIndices.Count)
        {
            _currentIndex = _stashIndices.Count - 1;
        }
        return false;
    }

    /// <summary>
    /// Gets the item card at a specific board slot, or null if empty.
    /// Used to find adjacent items for reorder feedback.
    /// </summary>
    public Card GetItemAtSlot(int slot)
    {
        if (slot < 0 || slot >= 10) return null;

        var bm = GetBoardManager();
        if (bm?.playerItemSockets == null) return null;

        // Check if this slot has an item
        if (slot < bm.playerItemSockets.Length)
        {
            return bm.playerItemSockets[slot]?.CardController?.CardData;
        }
        return null;
    }

    public bool HasContent() => GetCurrentSectionCount() > 0;

    public bool IsSelectionFree()
    {
        try { return Data.CurrentState?.SelectionContextRules?.SelectionIsFree ?? false; }
        catch { return false; }
    }

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
    // CONTENT VERIFICATION
    // ===============================================

    /// <summary>
    /// Checks if there are items on the board.
    /// </summary>
    public bool HasBoardContent() => _boardIndices.Count > 0;

    /// <summary>
    /// Checks if there are items in the selection.
    /// </summary>
    public bool HasSelectionContent() => _selectionItems.Count > 0;

    /// <summary>
    /// Gets the number of items in the stash.
    /// </summary>
    public int GetStashItemCount() => _stashIndices.Count;

    /// <summary>
    /// Indicates if the stash is open.
    /// </summary>
    public bool IsStashOpen() => _stashOpen;

    /// <summary>
    /// Gets the number of cards (not actions) in the selection.
    /// </summary>
    public int GetSelectionCardCount() =>
        _selectionItems.Count(i => i.Type == NavItemType.Card);

    // ===============================================
    // HELPERS
    // ===============================================

    private int GetCurrentSectionCount() => _currentSection switch
    {
        NavigationSection.Selection => _selectionItems.Count,
        NavigationSection.Board => _boardIndices.Count,
        NavigationSection.Stash => _stashIndices.Count,
        NavigationSection.Skills => Hero.Skills.Count,
        NavigationSection.Hero => Hero.GetStatCount(),
        _ => 0
    };

    /// <summary>
    /// Initializes the detail lines for the current item.
    /// </summary>
    private void InitDetailLines()
    {
        var card = GetCurrentCard();

        if (card != null)
        {
            Detail.Init(card, c => ItemReader.GetDetailLines(c));
        }
        else
        {
            // Handle Exit/Reroll NavItems
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

    private string GetCardDescription(Card card)
    {
        // In selection (shop/rewards)
        if (_currentSection == NavigationSection.Selection)
        {
            if (IsEncounterCard(card))
                return ItemReader.GetEncounterInfo(card);

            string shopName = ItemReader.GetCardName(card);
            if (ItemReader.IsQuestItem(card))
            {
                string questProgress = ItemReader.GetQuestProgress(card);
                shopName = questProgress != null ? $"{questProgress}: {shopName}" : $"Quest: {shopName}";
            }
            string shopSize = card.Type != ECardType.Skill ? ItemReader.GetSizeName(card) : null;
            string shopTier = ItemReader.GetTierName(card);

            if (IsSelectionFree())
            {
                // Free selection - no price, but show size/tier
                if (card.Type == ECardType.Skill)
                    return !string.IsNullOrEmpty(shopTier) ? $"{shopName}, {shopTier.ToLower()}" : shopName;

                if (!string.IsNullOrEmpty(shopSize) && !string.IsNullOrEmpty(shopTier))
                    return $"{shopName}, {shopSize}, {shopTier.ToLower()}";
                if (!string.IsNullOrEmpty(shopSize))
                    return $"{shopName}, {shopSize}";
                return shopName;
            }

            int price = ItemReader.GetBuyPrice(card);
            if (price > 0)
            {
                if (card.Type == ECardType.Skill)
                    return !string.IsNullOrEmpty(shopTier) ? $"{shopName}, {shopTier.ToLower()}, {price} gold" : $"{shopName}, {price} gold";

                if (!string.IsNullOrEmpty(shopSize) && !string.IsNullOrEmpty(shopTier))
                    return $"{shopName}, {shopSize}, {shopTier.ToLower()}, {price} gold";
                if (!string.IsNullOrEmpty(shopSize))
                    return $"{shopName}, {shopSize}, {price} gold";
                return $"{shopName}, {price} gold";
            }
            return shopName;
        }

        // In board/stash/skills
        string name = ItemReader.GetCardName(card);

        // Skills show tier only, items show size and tier
        if (card.Type == ECardType.Skill)
        {
            string tier = ItemReader.GetTierName(card);
            return !string.IsNullOrEmpty(tier) ? $"{name}, {tier.ToLower()}" : name;
        }

        string size = ItemReader.GetSizeName(card);
        string itemTier = ItemReader.GetTierName(card);

        if (!string.IsNullOrEmpty(size) && !string.IsNullOrEmpty(itemTier))
            return $"{name}, {size}, {itemTier.ToLower()}";
        if (!string.IsNullOrEmpty(size))
            return $"{name}, {size}";
        if (!string.IsNullOrEmpty(itemTier))
            return $"{name}, {itemTier.ToLower()}";
        return name;
    }

    private bool IsEncounterCard(Card card) =>
        card.Type == ECardType.CombatEncounter ||
        card.Type == ECardType.EventEncounter ||
        card.Type == ECardType.PedestalEncounter ||
        card.Type == ECardType.EncounterStep ||
        card.Type == ECardType.PvpEncounter;

    private BoardManager GetBoardManager()
    {
        try { return Singleton<BoardManager>.Instance; }
        catch { return null; }
    }

    /// <summary>
    /// Triggers visual selection of the current card using VisualSelector.
    /// </summary>
    public void TriggerVisualSelection()
    {
        try
        {
            var bm = GetBoardManager();
            if (bm == null) return;

            CardController controller = null;

            switch (_currentSection)
            {
                case NavigationSection.Selection:
                    // Items in selection are in the merchant sockets
                    var navItem = GetCurrentNavItem();
                    if (navItem?.Type == NavItemType.Card && navItem.Card != null)
                    {
                        controller = VisualSelector.FindCardController(navItem.Card, bm);
                    }
                    break;

                case NavigationSection.Board:
                    if (_currentIndex < _boardIndices.Count)
                    {
                        int idx = _boardIndices[_currentIndex];
                        controller = bm.playerItemSockets[idx]?.CardController;
                    }
                    break;

                case NavigationSection.Stash:
                    if (_currentIndex < _stashIndices.Count)
                    {
                        int idx = _stashIndices[_currentIndex];
                        controller = bm.playerStorageSockets?[idx]?.CardController;
                    }
                    break;

                case NavigationSection.Skills:
                    if (_currentIndex < Hero.Skills.Count)
                    {
                        // Player skills are in playerSkillSockets
                        if (bm.playerSkillSockets != null && _currentIndex < bm.playerSkillSockets.Length)
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
