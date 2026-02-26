using System;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Handles replay/recap input routing.
/// Manages recap sub-mode navigation (hero stats, enemy stats, enemy board, player board, combat stats).
/// </summary>
public class ReplayInputHandler
{
    private readonly GameplayNavigator _navigator;
    private readonly Action _onContinue;
    private readonly Action _onReplay;
    private readonly Action _onRecap;

    public ReplayInputHandler(GameplayNavigator navigator, Action onContinue, Action onReplay, Action onRecap)
    {
        _navigator = navigator;
        _onContinue = onContinue;
        _onReplay = onReplay;
        _onRecap = onRecap;
    }

    /// <summary>
    /// Handles input during recap sub-mode (after pressing E in replay mode).
    /// Supports V/F/G/B for section switching and arrow keys for navigation within sections.
    /// </summary>
    public void HandleRecapInput(AccessibleKey key)
    {
        // Teclas de navegación entre secciones (siempre disponibles)
        switch (key)
        {
            case AccessibleKey.GoToHero: // V = Hero stats
                _navigator.ExitEnemyMode();
                _navigator.EnterRecapHeroMode();
                return;

            case AccessibleKey.GoToEnemy: // F = Enemy stats
                _navigator.ExitEnemyMode();
                _navigator.EnterRecapEnemyStatsMode();
                return;

            case AccessibleKey.GoToStash: // G = Enemy board
                _navigator.EnterOpponentBoardMode();
                return;

            case AccessibleKey.GoToBoard: // B = Player board
                _navigator.ExitEnemyMode();
                _navigator.EnterRecapPlayerBoardMode();
                return;

            case AccessibleKey.Confirm: // Enter = Continue
                _onContinue();
                return;

            case AccessibleKey.WinsInfo: // W = Wins
                _navigator.AnnounceWins();
                return;

            case AccessibleKey.CombatSummary: // H = Combat stats per card
                _navigator.EnterRecapCombatStatsMode();
                return;

            case AccessibleKey.Back: // Backspace = Exit recap
                _navigator.SetRecapMode(false);
                TolkWrapper.Speak("Exited recap. Enter to continue, R to replay, E to return to recap.");
                return;
        }

        // Navegación dentro de la sección actual
        var recapSection = _navigator.CurrentRecapSection;

        if (recapSection == RecapSection.HeroStats || recapSection == RecapSection.HeroSkills)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    _navigator.RecapHeroPrevious();
                    return;
                case AccessibleKey.Down:
                    _navigator.RecapHeroNext();
                    return;
                case AccessibleKey.Left:
                    _navigator.RecapHeroToStats();
                    return;
                case AccessibleKey.Right:
                    _navigator.RecapHeroToSkills();
                    return;
            }
        }
        else if (recapSection == RecapSection.EnemyStats || recapSection == RecapSection.EnemySkills)
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
            }
        }
        else if (recapSection == RecapSection.EnemyBoard)
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
            }
        }
        else if (recapSection == RecapSection.PlayerBoard)
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
            }
        }
        else if (recapSection == RecapSection.CombatStats)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    _navigator.RecapCombatStatsPrevious();
                    return;
                case AccessibleKey.Down:
                    _navigator.RecapCombatStatsNext();
                    return;
            }
        }
    }

    /// <summary>
    /// Handles input during normal replay mode (before pressing E for recap).
    /// Supports Enter (continue), R (replay), E (recap), V (hero stats), F (enemy info).
    /// </summary>
    public void HandleReplayInput(AccessibleKey key)
    {
        // Modo replay normal (antes de E para recap)
        switch (key)
        {
            case AccessibleKey.Confirm:
                _onContinue();
                break;

            case AccessibleKey.Reroll:
                _onReplay();
                break;

            case AccessibleKey.Exit:
                _onRecap();
                break;

            case AccessibleKey.GoToHero:
                _navigator.ReadAllHeroStats();
                break;

            case AccessibleKey.GoToEnemy:
                _navigator.ReadEnemyInfo();
                break;

            case AccessibleKey.GoToStash:
                TolkWrapper.Speak("Press E for Recap first, then G for opponent board.");
                break;

            case AccessibleKey.WinsInfo:
                _navigator.AnnounceWins();
                break;

            case AccessibleKey.Back:
                TolkWrapper.Speak("Post-combat. Enter to continue, R to replay, E for recap.");
                break;

            default:
                break;
        }
    }
}
