using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Patches;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Handles all combat input routing.
/// Manages combat navigation sections (player board, enemy board, enemy stats, hero stats).
/// </summary>
public class CombatInputHandler
{
    private readonly GameplayNavigator _navigator;

    /// <summary>
    /// Combat navigation section enum.
    /// </summary>
    public enum CombatNavSection { None, PlayerBoard, EnemyBoard, EnemyStats, HeroStats }

    private CombatNavSection _combatNavSection = CombatNavSection.None;

    /// <summary>
    /// The current combat navigation section.
    /// </summary>
    public CombatNavSection CurrentSection => _combatNavSection;

    public CombatInputHandler(GameplayNavigator navigator)
    {
        _navigator = navigator;
    }

    /// <summary>
    /// Resets the combat navigation section to None.
    /// </summary>
    public void Reset()
    {
        _combatNavSection = CombatNavSection.None;
    }

    /// <summary>
    /// Handles input during combat. Returns true if the input was handled.
    /// </summary>
    public void HandleInput(AccessibleKey key)
    {
        // Handle combat navigation sections (player board or enemy board)
        if (_combatNavSection == CombatNavSection.PlayerBoard)
        {
            switch (key)
            {
                case AccessibleKey.Left:
                    _navigator.Previous();
                    return;
                case AccessibleKey.Right:
                    _navigator.Next();
                    return;
                case AccessibleKey.Up:
                    _navigator.ReadDetailLineUp();
                    return;
                case AccessibleKey.Down:
                    _navigator.ReadDetailLineDown();
                    return;
                case AccessibleKey.GoToBoard: // B again re-announces
                    _navigator.GoToBoard();
                    return;
                case AccessibleKey.GoToStash: // G switches to enemy board
                    _combatNavSection = CombatNavSection.EnemyBoard;
                    _navigator.EnterOpponentBoardMode();
                    return;
                case AccessibleKey.GoToHero: // V switches to hero
                    _combatNavSection = CombatNavSection.HeroStats;
                    _navigator.GoToHero();
                    return;
                case AccessibleKey.GoToEnemy: // F switches to enemy stats
                    _combatNavSection = CombatNavSection.EnemyStats;
                    _navigator.EnterCombatEnemyStatsMode();
                    return;
                case AccessibleKey.Back:
                    _combatNavSection = CombatNavSection.None;
                    TolkWrapper.Speak("Exited board view");
                    return;
            }
        }
        else if (_combatNavSection == CombatNavSection.EnemyBoard)
        {
            switch (key)
            {
                case AccessibleKey.Left:
                    _navigator.EnemyNavigateLeft();
                    return;
                case AccessibleKey.Right:
                    _navigator.EnemyNavigateRight();
                    return;
                case AccessibleKey.Up:
                    _navigator.EnemyDetailPrevious();
                    return;
                case AccessibleKey.Down:
                    _navigator.EnemyDetailNext();
                    return;
                case AccessibleKey.GoToStash: // G again re-announces
                    _navigator.EnterOpponentBoardMode();
                    return;
                case AccessibleKey.GoToBoard: // B switches to player board
                    _combatNavSection = CombatNavSection.PlayerBoard;
                    _navigator.ExitEnemyMode();
                    _navigator.GoToBoard();
                    return;
                case AccessibleKey.GoToHero: // V switches to hero
                    _combatNavSection = CombatNavSection.HeroStats;
                    _navigator.ExitEnemyMode();
                    _navigator.GoToHero();
                    return;
                case AccessibleKey.GoToEnemy: // F switches to enemy stats
                    _combatNavSection = CombatNavSection.EnemyStats;
                    _navigator.ExitEnemyMode();
                    _navigator.EnterCombatEnemyStatsMode();
                    return;
                case AccessibleKey.Back:
                    _combatNavSection = CombatNavSection.None;
                    _navigator.ExitEnemyMode();
                    TolkWrapper.Speak("Exited enemy board view");
                    return;
            }
        }
        else if (_combatNavSection == CombatNavSection.EnemyStats)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    _navigator.RecapEnemyStatsPrevious();
                    return;
                case AccessibleKey.Down:
                    _navigator.RecapEnemyStatsNext();
                    return;
                case AccessibleKey.Left:
                    _navigator.RecapEnemyToStats();
                    return;
                case AccessibleKey.Right:
                    _navigator.RecapEnemyToSkills();
                    return;
                case AccessibleKey.GoToEnemy: // F again re-announces
                    _navigator.EnterCombatEnemyStatsMode();
                    return;
                case AccessibleKey.GoToBoard: // B switches to player board
                    _combatNavSection = CombatNavSection.PlayerBoard;
                    _navigator.GoToBoard();
                    return;
                case AccessibleKey.GoToStash: // G switches to enemy board
                    _combatNavSection = CombatNavSection.EnemyBoard;
                    _navigator.EnterOpponentBoardMode();
                    return;
                case AccessibleKey.GoToHero: // V switches to hero
                    _combatNavSection = CombatNavSection.HeroStats;
                    _navigator.GoToHero();
                    return;
                case AccessibleKey.Back:
                    _combatNavSection = CombatNavSection.None;
                    TolkWrapper.Speak("Exited enemy stats view");
                    return;
            }
        }
        else if (_combatNavSection == CombatNavSection.HeroStats)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    _navigator.HeroPrevious();
                    return;
                case AccessibleKey.Down:
                    _navigator.HeroNext();
                    return;
                case AccessibleKey.Left:
                    _navigator.HeroPreviousSubsection();
                    return;
                case AccessibleKey.Right:
                    _navigator.HeroNextSubsection();
                    return;
                case AccessibleKey.GoToHero: // V again re-announces
                    _navigator.GoToHero();
                    return;
                case AccessibleKey.GoToBoard: // B switches to player board
                    _combatNavSection = CombatNavSection.PlayerBoard;
                    _navigator.GoToBoard();
                    return;
                case AccessibleKey.GoToStash: // G switches to enemy board
                    _combatNavSection = CombatNavSection.EnemyBoard;
                    _navigator.EnterOpponentBoardMode();
                    return;
                case AccessibleKey.GoToEnemy: // F switches to enemy stats
                    _combatNavSection = CombatNavSection.EnemyStats;
                    _navigator.EnterCombatEnemyStatsMode();
                    return;
                case AccessibleKey.Back:
                    _combatNavSection = CombatNavSection.None;
                    TolkWrapper.Speak("Exited hero stats view");
                    return;
            }
        }

        switch (key)
        {
            case AccessibleKey.GoToBoard: // B = Player board
                if (!StateChangePatch.IsCombatBoardReady)
                    break;
                _combatNavSection = CombatNavSection.PlayerBoard;
                _navigator.GoToBoard();
                break;

            case AccessibleKey.GoToStash: // G = Enemy board
                if (!StateChangePatch.IsCombatBoardReady)
                    break;
                _combatNavSection = CombatNavSection.EnemyBoard;
                _navigator.EnterOpponentBoardMode();
                break;

            case AccessibleKey.GoToHero:
                _combatNavSection = CombatNavSection.HeroStats;
                _navigator.GoToHero();
                break;

            case AccessibleKey.GoToEnemy: // F = Enemy stats with navigation
                if (!StateChangePatch.IsCombatBoardReady)
                    break;
                _combatNavSection = CombatNavSection.EnemyStats;
                _navigator.EnterCombatEnemyStatsMode();
                break;

            case AccessibleKey.ToggleCombatMode:
                CombatDescriber.ToggleMode();
                break;

            case AccessibleKey.Confirm:
                if (_navigator.IsInHeroSection)
                    _navigator.ReadAllHeroStats();
                break;

            case AccessibleKey.CombatSummary:
                // H key - get combat summary
                TolkWrapper.Speak(CombatDescriber.GetCombatSummary());
                break;

            case AccessibleKey.WinsInfo:
                // W key - wins info (also available during combat)
                _navigator.AnnounceWins();
                break;

            case AccessibleKey.PlayerHealth:
                TolkWrapper.Speak(CombatDescriber.GetPlayerHealth());
                break;

            case AccessibleKey.EnemyHealth:
                TolkWrapper.Speak(CombatDescriber.GetEnemyHealth());
                break;

            case AccessibleKey.DamageDealt:
                TolkWrapper.Speak(CombatDescriber.GetDamageDealt());
                break;

            case AccessibleKey.DamageTaken:
                TolkWrapper.Speak(CombatDescriber.GetDamageTaken());
                break;

            case AccessibleKey.Back:
                // Do nothing - let the game handle it (opens pause menu)
                break;

            // Ignorar todas las demás teclas durante combate
            default:
                break;
        }
    }
}
