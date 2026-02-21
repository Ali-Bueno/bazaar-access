using System.Collections;
using System.Collections.Generic;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarAccess.UI;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Pantalla accesible para el gameplay.
/// Navegación dinámica con items y acciones.
/// Auto-focus a la sección correcta según el estado del juego.
/// </summary>
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

public class GameplayScreen : IAccessibleScreen
{
    public string ScreenName => "Gameplay";

    private readonly GameplayNavigator _navigator;
    private bool _isValid = true;
    private ERunState _lastState = ERunState.Choice;

    // Action mode state
    private bool _isInActionMode = false;
    private List<ActionOption> _actionOptions = new List<ActionOption>();
    private int _actionIndex = 0;
    private Card _actionCard = null;

    // Combat navigation mode
    private enum CombatNavSection { None, PlayerBoard, EnemyBoard, EnemyStats, HeroStats }
    private CombatNavSection _combatNavSection = CombatNavSection.None;

    public GameplayScreen()
    {
        _navigator = new GameplayNavigator();
    }

    public void HandleInput(AccessibleKey key)
    {
        // Handle action mode input (when in action mode)
        if (_isInActionMode)
        {
            HandleActionModeInput(key);
            return;
        }

        // En modo replay (post-combate), Enter/R/E + V/F/G/B para navegación
        if (_navigator.IsInReplayMode)
        {
            // En modo recap, navegación completa con V/F/G/B
            if (_navigator.IsInRecapMode)
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
                        TriggerReplayContinue();
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

                return;
            }

            // Modo replay normal (antes de E para recap)
            switch (key)
            {
                case AccessibleKey.Confirm:
                    TriggerReplayContinue();
                    break;

                case AccessibleKey.Reroll:
                    TriggerReplayReplay();
                    break;

                case AccessibleKey.Exit:
                    TriggerReplayRecap();
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
            return;
        }

        // Verificar estado de combate tanto por el flag como por el ERunState actual
        var currentState = StateChangePatch.GetCurrentRunState();
        bool inCombat = _navigator.IsInCombat ||
                        currentState == ERunState.Combat ||
                        currentState == ERunState.PVPCombat;

        // En modo combate, permitir navegación de tableros con B/G y flechas
        if (inCombat)
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
            return;
        }

        // Reset combat nav section when exiting combat
        _combatNavSection = CombatNavSection.None;

        // Si estamos en modo enemigo, manejar navegación de items del enemigo
        if (_navigator.IsInEnemyMode)
        {
            switch (key)
            {
                case AccessibleKey.Up:
                    _navigator.EnemyPrevious();
                    return;

                case AccessibleKey.Down:
                    _navigator.EnemyNext();
                    return;

                case AccessibleKey.Confirm:
                    _navigator.ReadCurrentEnemyItemDetails();
                    return;

                case AccessibleKey.GoToEnemy:
                    // F de nuevo relee los stats del enemigo
                    _navigator.ReadEnemyInfo();
                    return;

                case AccessibleKey.Back:
                    _navigator.ExitEnemyMode();
                    TolkWrapper.Speak("Exited enemy view");
                    return;

                default:
                    // Cualquier otra tecla sale del modo enemigo
                    _navigator.ExitEnemyMode();
                    break;
            }
        }

        switch (key)
        {
            // Navegación de secciones
            case AccessibleKey.Tab:
                _navigator.NextSection();
                break;

            case AccessibleKey.GoToBoard:
                _navigator.GoToBoard();
                break;

            case AccessibleKey.GoToHero:
                _navigator.GoToHero();
                break;

            case AccessibleKey.GoToChoices:
                _navigator.GoToChoices();
                break;

            case AccessibleKey.GoToStash:
                // G opens stash (if closed) and navigates to it
                if (!_navigator.IsStashOpen())
                {
                    _navigator.ToggleStash(); // Open the stash
                }
                else
                {
                    _navigator.GoToStash(); // Navigate to stash
                }
                break;


            case AccessibleKey.GoToEnemy:
                _navigator.ReadEnemyInfo();
                break;

            // Navegación dentro de la sección actual
            case AccessibleKey.Right:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroNextSubsection();
                else
                    _navigator.Next();
                break;

            case AccessibleKey.Left:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroPreviousSubsection();
                else
                    _navigator.Previous();
                break;

            // Up/Down: Navigate hero stats in Hero section, or read item details elsewhere
            case AccessibleKey.Up:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroPrevious();
                else
                    _navigator.ReadDetailLineUp();
                break;

            case AccessibleKey.Down:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroNext();
                else
                    _navigator.ReadDetailLineDown();
                break;

            // Fast navigation
            case AccessibleKey.Home:
                _navigator.NavigateToFirst();
                break;

            case AccessibleKey.End:
                _navigator.NavigateToLast();
                break;

            case AccessibleKey.PageUp:
                _navigator.NavigatePage(-1);
                break;

            case AccessibleKey.PageDown:
                _navigator.NavigatePage(1);
                break;

            // Acción principal
            case AccessibleKey.Confirm:
                HandleConfirm();
                break;

            // Atajos directos para Exit/Reroll
            case AccessibleKey.Exit:
                if (_navigator.TryExit())
                    Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                break;

            case AccessibleKey.Reroll:
                if (_navigator.TryReroll())
                    Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                break;

            // Escape - don't intercept, let the game handle it (opens pause menu)
            case AccessibleKey.Back:
                // Do nothing - let the game open the pause menu
                break;

            // Buffer de mensajes
            case AccessibleKey.NextMessage:
                MessageBuffer.ReadNewest();
                break;

            case AccessibleKey.PrevMessage:
                MessageBuffer.ReadPrevious();
                break;

            // Space - toggle stash open/close
            case AccessibleKey.Space:
                _navigator.ToggleStash();
                break;

            // Shift+Up - mover del stash al board
            case AccessibleKey.MoveToBoard:
                HandleMoveToBoard();
                break;

            // Shift+Down - mover del board al stash
            case AccessibleKey.MoveToStash:
                HandleMoveToStash();
                break;

            // Shift+Left/Right - reordenar items en el tablero
            case AccessibleKey.ReorderLeft:
                HandleReorder(-1);
                break;

            case AccessibleKey.ReorderRight:
                HandleReorder(1);
                break;

            // I - Información de propiedades/keywords
            case AccessibleKey.Info:
                ReadPropertyInfo();
                break;

            // Shift+U - Upgrade item at pedestal
            case AccessibleKey.Upgrade:
                HandleUpgrade();
                break;

            // T - Board capacity info
            case AccessibleKey.BoardInfo:
                _navigator.AnnounceBoardCapacity();
                break;

            // S - Stash capacity info
            case AccessibleKey.StashInfo:
                _navigator.AnnounceStashCapacity();
                break;

            // W - Wins info
            case AccessibleKey.WinsInfo:
                _navigator.AnnounceWins();
                break;

            // Ctrl+M - Toggle combat announcement mode
            case AccessibleKey.ToggleCombatMode:
                CombatDescriber.ToggleMode();
                break;
        }
    }

    /// <summary>
    /// Enters action mode for the current item.
    /// Builds a menu of available actions.
    /// </summary>
    private void EnterActionMode(Card card)
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
            if (ActionHelper.IsEnchantPedestal())
            {
                // For enchant pedestals, check if item is already enchanted
                var itemCardCheck = card as ItemCard;
                if (itemCardCheck == null || !itemCardCheck.Enchantment.HasValue)
                {
                    _actionOptions.Add(ActionOption.Enchant);
                }
            }
            else if (ActionHelper.CanUpgrade() && card.Tier != ETier.Legendary)
            {
                _actionOptions.Add(ActionOption.Upgrade);
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
                var upgradeInfo = ActionHelper.GetCurrentPedestalInfo();
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
                string nextTier = GetNextTierName(_actionCard.Tier);
                return $"Upgrade to {nextTier} (U)";

            case ActionOption.Enchant:
                var pedestalInfo = ActionHelper.GetCurrentPedestalInfo();
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
                    RefreshAndAnnounce();
                }
                break;

            case ActionOption.Upgrade:
                // Show confirmation dialog with preview instead of executing directly
                if (itemCard != null)
                {
                    HandleUpgradeConfirm(itemCard, isEnchant: false);
                }
                break;

            case ActionOption.Enchant:
                if (itemCard != null)
                {
                    HandleUpgradeConfirm(itemCard, isEnchant: true);
                }
                break;

            case ActionOption.MoveToStash:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, true);
                    TolkWrapper.Speak("Moved to stash");
                    RefreshAndAnnounce();
                }
                break;

            case ActionOption.MoveToBoard:
                if (itemCard != null)
                {
                    ActionHelper.MoveItem(itemCard, false);
                    TolkWrapper.Speak("Moved to board");
                    RefreshAndAnnounce();
                }
                break;
        }
    }

    /// <summary>
    /// Handles input while in action mode.
    /// </summary>
    private void HandleActionModeInput(AccessibleKey key)
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
    /// Announces the position after reordering, including adjacent items.
    /// </summary>
    private void AnnounceReorderPosition(int slot, ItemCard movedCard)
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
        int moveCount = System.Math.Abs(targetSlot - startSlot);

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

    /// <summary>
    /// Handles the upgrade action (Shift+U).
    /// </summary>
    private void HandleUpgrade()
    {
        // Only works when viewing board or stash items
        if (!_navigator.IsInPlayerSection())
        {
            TolkWrapper.Speak("Select an item on your board or stash to upgrade");
            return;
        }

        var card = _navigator.GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        // Route through confirmation dialog with explicit enchant detection
        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState == ERunState.Pedestal)
        {
            bool isEnchant = ActionHelper.IsEnchantPedestal();
            HandleUpgradeConfirm(card, isEnchant: isEnchant);
        }
        else
        {
            TolkWrapper.Speak("Not at a pedestal");
        }
    }

    /// <summary>
    /// Lee las descripciones de propiedades/keywords del item actual.
    /// </summary>
    private void ReadPropertyInfo()
    {
        var card = _navigator.GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        var descriptions = ItemReader.GetAllPropertyDescriptions(card);
        if (descriptions.Count == 0)
        {
            TolkWrapper.Speak("No property information available");
            return;
        }

        // Leer todas las descripciones
        string name = ItemReader.GetCardName(card);
        string info = $"{name} properties: " + string.Join(". ", descriptions);
        TolkWrapper.Speak(info);

        // También añadir al buffer para poder releer
        MessageBuffer.Add(info);
    }

    public string GetHelp()
    {
        return "Left/Right: Navigate items. Up/Down: Read details. " +
               "Tab: Switch section. Space: Toggle stash. G: Go to stash. " +
               "B: Board. V: Hero. C: Choices. F: Enemy. I: Properties. W: Wins. " +
               "Enter: Select/Buy or Action menu on board items. E: Exit. R: Refresh. " +
               "In Action menu: S sell, U upgrade, M move, Arrows reorder. " +
               "Ctrl+Arrows: Detail reading. Period/Comma: Messages.";
    }

    public void OnFocus()
    {
        _lastState = StateChangePatch.GetCurrentRunState();
        _navigator.Refresh();

        // Only auto-focus when this is NOT returning from a UI popup
        // When returning from a popup (e.g., sell confirmation), keep the user in their current section
        if (!AccessibilityMgr.IsReturningFromUI)
        {
            // Auto-focus a la sección correcta según el estado
            AutoFocusForState(_lastState);
        }

        // No anunciar aquí - DelayedInitialize lo hará después de que el contenido esté listo
        // _navigator.AnnounceState();
    }

    /// <summary>
    /// Llamado cuando cambia el estado del juego.
    /// </summary>
    /// <param name="newState">El nuevo estado del juego</param>
    /// <param name="stateActuallyChanged">True si el estado realmente cambió (calculado por StateChangePatch)</param>
    public void OnStateChanged(ERunState newState, bool stateActuallyChanged = true)
    {
        _lastState = newState;

        // Durante combate, no anunciar nada aquí (OnCombatStateChanged lo hará)
        bool isCombatState = newState == ERunState.Combat || newState == ERunState.PVPCombat;
        if (isCombatState)
        {
            return; // El anuncio de combate se hace en OnCombatStateChanged
        }

        // No anunciar aquí - el sistema de debounce en StateChangePatch lo hará
        // Esto evita duplicados

        // Hacer refresh y auto-focus SOLO si el estado realmente cambió
        // Use the parameter from StateChangePatch, don't recalculate
        Plugin.Instance.StartCoroutine(RefreshAndAutoFocus(newState, stateActuallyChanged));
    }

    private System.Collections.IEnumerator RefreshAndAutoFocus(ERunState state, bool stateChanged)
    {
        // Primer refresh rápido
        yield return new WaitForSeconds(0.1f);
        _navigator.Refresh();

        // Auto-focus si cambió el estado
        if (stateChanged)
        {
            AutoFocusForState(state);
        }

        // Anunciar el primer item si hay contenido
        if (_navigator.HasContent())
        {
            _navigator.AnnounceCurrentItem();
        }

        // Segundo refresh para capturar cambios tardíos
        yield return new WaitForSeconds(0.4f);
        _navigator.Refresh();

        // Tercer refresh para estados que tardan más
        yield return new WaitForSeconds(0.5f);
        _navigator.Refresh();
    }

    /// <summary>
    /// Auto-focus a la sección correcta según el estado del juego.
    /// </summary>
    private void AutoFocusForState(ERunState state)
    {
        switch (state)
        {
            case ERunState.Encounter:
                // En encounter, ir a la selección de encuentros
                _navigator.GoToSection(NavigationSection.Selection);
                break;

            case ERunState.Choice:
                // En tienda, ir a la selección (items/skills)
                _navigator.GoToSection(NavigationSection.Selection);
                break;

            case ERunState.Loot:
                // En loot, ir a las recompensas
                _navigator.GoToSection(NavigationSection.Selection);
                break;

            case ERunState.LevelUp:
                // En level up, ir a la selección
                _navigator.GoToSection(NavigationSection.Selection);
                break;

            case ERunState.Pedestal:
                // En upgrade station, ir a la selección
                _navigator.GoToSection(NavigationSection.Selection);
                break;

            case ERunState.Combat:
            case ERunState.PVPCombat:
                // En combate, solo ir a Hero silenciosamente (no anunciar board)
                _navigator.SetSectionSilent(NavigationSection.Hero);
                break;

            default:
                // Por defecto, si hay selección ir ahí, sino al board
                if (_navigator.HasSelectionContent())
                {
                    _navigator.GoToSection(NavigationSection.Selection);
                }
                else if (_navigator.HasBoardContent())
                {
                    _navigator.GoToSection(NavigationSection.Board);
                }
                break;
        }
    }

    public bool IsValid()
    {
        if (!_isValid) return false;

        try { return Singleton<BoardManager>.Instance != null; }
        catch { return false; }
    }

    public void Invalidate() => _isValid = false;

    private void HandleConfirm()
    {
        // Si estamos en Hero, manejar según la subsección
        if (_navigator.IsInHeroSection)
        {
            if (_navigator.CurrentHeroSubsection == HeroSubsection.Skills)
            {
                // En Skills, leer detalles de la skill actual
                _navigator.ReadHeroSkillDetails();
            }
            else
            {
                // En Stats, leer todos los stats
                _navigator.ReadAllHeroStats();
            }
            return;
        }

        // Si estamos en Selection, ver qué tipo de item es
        if (_navigator.IsInSelectionSection())
        {
            var navItem = _navigator.GetCurrentNavItem();
            if (navItem == null)
            {
                TolkWrapper.Speak("Nothing selected");
                return;
            }

            switch (navItem.Type)
            {
                case NavItemType.Exit:
                    if (_navigator.TryExit())
                        Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                    break;

                case NavItemType.Reroll:
                    if (_navigator.TryReroll())
                        Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                    break;

                case NavItemType.Card:
                    HandleCardConfirm(navItem.Card);
                    break;
            }
            return;
        }

        // Si estamos en Board/Stash, abrir menú de acciones
        if (_navigator.IsInPlayerSection())
        {
            var card = _navigator.GetCurrentCard();
            if (card != null)
            {
                EnterActionMode(card);
            }
            else
            {
                TolkWrapper.Speak("Nothing selected");
            }
            return;
        }

        // Skills - solo leer info
        _navigator.ReadDetailedInfo();
    }

    private void HandleCardConfirm(Card card)
    {
        switch (card.Type)
        {
            case ECardType.Item:
                BuyItem(card);
                break;

            case ECardType.Skill:
                SelectSkill(card);
                break;

            case ECardType.CombatEncounter:
            case ECardType.EventEncounter:
            case ECardType.PedestalEncounter:
            case ECardType.EncounterStep:
            case ECardType.PvpEncounter:
                SelectEncounterDirect(card);
                break;

            default:
                TolkWrapper.Speak("Cannot select this");
                break;
        }
    }

    private void BuyItem(Card card)
    {
        var itemCard = card as ItemCard;
        if (itemCard == null) { TolkWrapper.Speak("Not an item"); return; }

        string name = ItemReader.GetCardName(card);

        if (_navigator.IsSelectionFree())
        {
            // En Loot/Rewards, los items son gratuitos
            ActionHelper.BuyItem(itemCard, toStash: false, silent: false, isFree: true);
            // Usar delayed refresh porque el SelectionSet tarda en actualizarse
            Plugin.Instance.StartCoroutine(DelayedRefreshAfterLoot());
        }
        else
        {
            int price = ItemReader.GetBuyPrice(card);
            var ui = new ConfirmActionUI(ConfirmActionType.Buy, name, price,
                onConfirm: () => {
                    ActionHelper.BuyItem(itemCard);
                    Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                },
                onCancel: () => TolkWrapper.Speak("Cancelled"));
            AccessibilityMgr.ShowUI(ui);
        }
    }

    /// <summary>
    /// Coroutine para refresh después de seleccionar loot/skill.
    /// </summary>
    private System.Collections.IEnumerator DelayedRefreshAfterLoot()
    {
        // Esperar a que el juego procese la selección
        yield return new WaitForSeconds(0.3f);
        _navigator.Refresh();

        // Solo anunciar si hay más items, sin decir el número
        if (_navigator.HasSelectionContent())
        {
            _navigator.AnnounceCurrentItem();
        }
        // Si no hay más, el sistema de eventos anunciará el nuevo estado
    }

    private void SelectSkill(Card card)
    {
        var skillCard = card as SkillCard;
        if (skillCard == null) { TolkWrapper.Speak("Not a skill"); return; }

        ActionHelper.SelectSkill(skillCard);
        // Usar delayed refresh para dar tiempo al juego de actualizar
        Plugin.Instance.StartCoroutine(DelayedRefreshAfterLoot());
    }

    private void SelectEncounterDirect(Card card)
    {
        // Use encounter info for PvP (includes hero name + rank), card name for others
        string name = card.Type == ECardType.PvpEncounter
            ? ItemReader.GetEncounterInfo(card)
            : ItemReader.GetCardName(card);
        TolkWrapper.Speak(name);

        ActionHelper.SelectEncounter(card);

        // StateChangePatch se encargará del anuncio con debounce
        Plugin.Instance.StartCoroutine(DelayedRefreshOnly());
    }

    /// <summary>
    /// Solo hace refresh sin anunciar (el debounce de StateChangePatch se encarga).
    /// </summary>
    private System.Collections.IEnumerator DelayedRefreshOnly()
    {
        yield return new WaitForSeconds(0.5f);
        _navigator.Refresh();
    }

    private void HandleSellConfirm(Card card)
    {
        var itemCard = card as ItemCard;
        if (itemCard == null) { TolkWrapper.Speak("Cannot sell this"); return; }

        // Check if we're in Pedestal state - offer upgrade instead of sell
        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState == ERunState.Pedestal && ActionHelper.CanUpgrade())
        {
            HandleUpgradeConfirm(card);
            return;
        }

        if (!_navigator.CanSellInCurrentState())
        {
            TolkWrapper.Speak("Cannot sell right now");
            return;
        }

        string name = ItemReader.GetCardName(card);
        int price = ItemReader.GetSellPrice(card);

        var ui = new ConfirmActionUI(ConfirmActionType.Sell, name, price,
            onConfirm: () => {
                ActionHelper.SellItem(itemCard);
                RefreshAndAnnounce();
            },
            onCancel: () => TolkWrapper.Speak("Cancelled"));
        AccessibilityMgr.ShowUI(ui);
    }

    /// <summary>
    /// Shows upgrade confirmation dialog with tier information.
    /// </summary>
    /// <param name="card">The card to upgrade/enchant</param>
    /// <param name="isEnchant">If known: true=enchant, false=upgrade. Null=auto-detect from pedestal.</param>
    private void HandleUpgradeConfirm(Card card, bool? isEnchant = null)
    {
        Plugin.Logger.LogInfo($"HandleUpgradeConfirm called for card: {card?.GetType().Name ?? "null"}, isEnchant={isEnchant}");

        string name = ItemReader.GetCardName(card);

        // Get pedestal info
        var pedestalInfo = ActionHelper.GetCurrentPedestalInfo();
        Plugin.Logger.LogInfo($"HandleUpgradeConfirm: pedestalInfo.Type={pedestalInfo.Type}, name={name}");

        // Determine if this is an enchant action
        bool doEnchant;
        if (isEnchant.HasValue)
        {
            doEnchant = isEnchant.Value;
        }
        else
        {
            doEnchant = pedestalInfo.Type == ActionHelper.PedestalType.Enchant ||
                        pedestalInfo.Type == ActionHelper.PedestalType.EnchantRandom;
        }

        if (doEnchant)
        {
            // Enchantment altar
            // Check if already enchanted
            if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
            {
                string currentEnchant = ItemReader.GetEnchantmentName(itemCard.Enchantment.Value);
                TolkWrapper.Speak($"{name} is already enchanted with {currentEnchant}");
                return;
            }

            string enchantName = pedestalInfo.EnchantmentName ?? "random";

            // Build message with preview
            var messageParts = new List<string>();
            if (pedestalInfo.Type == ActionHelper.PedestalType.EnchantRandom)
            {
                messageParts.Add($"Enchant {name} with a random enchantment?");
            }
            else
            {
                messageParts.Add($"Enchant {name} with {enchantName}.");
                // Get enchantment preview
                var preview = ActionHelper.GetEnchantPreview(card, enchantName);
                if (preview.Count > 0)
                {
                    messageParts.Add("Effects: " + string.Join(", ", preview));
                }
            }
            messageParts.Add("Press U to confirm, Backspace to cancel.");

            string message = string.Join(" ", messageParts);

            var ui = new ConfirmActionUI(ConfirmActionType.Upgrade, name, 0, message,
                onConfirm: () => {
                    if (ActionHelper.UseCurrentPedestal(card))
                    {
                        Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                    }
                },
                onCancel: () => TolkWrapper.Speak("Cancelled"));
            AccessibilityMgr.ShowUI(ui);
        }
        else
        {
            // Upgrade altar
            string currentTier = ItemReader.GetTierName(card);
            var upgradeInfo = ActionHelper.GetCurrentPedestalInfo();

            // Check if already at max tier
            if (card.Tier == ETier.Legendary)
            {
                TolkWrapper.Speak($"{name} is already at {currentTier}, cannot upgrade further");
                return;
            }

            // Build message with preview
            var messageParts = new List<string>();

            if (upgradeInfo.TargetTier.HasValue)
            {
                if (upgradeInfo.TargetTier.Value == card.Tier)
                {
                    messageParts.Add($"Upgrade {name} stats. Stays {currentTier}.");
                }
                else
                {
                    messageParts.Add($"Upgrade {name} from {currentTier} to {ItemReader.GetTierName(upgradeInfo.TargetTier.Value)}.");
                }
            }
            else
            {
                string nextTier = GetNextTierName(card.Tier);
                messageParts.Add($"Upgrade {name} from {currentTier} to {nextTier}.");
            }

            // Get post-upgrade stats
            try
            {
                var preview = ActionHelper.GetUpgradePreview(card);
                if (preview.Count > 0)
                {
                    messageParts.Add("After upgrade: " + string.Join(", ", preview));
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"GetUpgradePreview failed: {ex.Message}");
            }

            messageParts.Add("Press U to confirm, Backspace to cancel.");

            string message = string.Join(" ", messageParts);

            var ui = new ConfirmActionUI(ConfirmActionType.Upgrade, name, 0, message,
                onConfirm: () => {
                    if (ActionHelper.UpgradeItem(card))
                    {
                        Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
                    }
                },
                onCancel: () => TolkWrapper.Speak("Cancelled"));
            AccessibilityMgr.ShowUI(ui);
        }
    }

    /// <summary>
    /// Gets the name of the next tier.
    /// </summary>
    private string GetNextTierName(ETier currentTier)
    {
        return currentTier switch
        {
            ETier.Bronze => "Silver",
            ETier.Silver => "Gold",
            ETier.Gold => "Diamond",
            ETier.Diamond => "Legendary",
            _ => "max"
        };
    }

    private void HandleMoveAction()
    {
        if (!_navigator.IsInPlayerSection())
        {
            TolkWrapper.Speak("Select an item on your board or stash first");
            return;
        }

        var card = _navigator.GetCurrentCard() as ItemCard;
        if (card == null)
        {
            TolkWrapper.Speak("Cannot move this");
            return;
        }

        if (!_navigator.CanMoveInCurrentState())
        {
            TolkWrapper.Speak("Cannot move right now");
            return;
        }

        bool toStash = _navigator.CurrentSection == NavigationSection.Board;

        // ActionHelper.MoveItem already speaks success/failure messages
        if (ActionHelper.MoveItem(card, toStash))
        {
            RefreshAndAnnounce();
        }
    }

    private void HandleMoveToBoard()
    {
        // Solo funciona si estamos en el stash
        if (_navigator.CurrentSection != NavigationSection.Stash)
        {
            TolkWrapper.Speak("Select an item in your stash first");
            return;
        }

        var card = _navigator.GetCurrentCard() as ItemCard;
        if (card == null)
        {
            TolkWrapper.Speak("Cannot move this");
            return;
        }

        if (!_navigator.CanMoveInCurrentState())
        {
            TolkWrapper.Speak("Cannot move right now");
            return;
        }

        // ActionHelper.MoveItem already speaks success/failure messages
        if (ActionHelper.MoveItem(card, false)) // false = to board
        {
            RefreshAndAnnounce();
            _navigator.TriggerVisualSelection();
        }
    }

    private void HandleMoveToStash()
    {
        // Solo funciona si estamos en el board
        if (_navigator.CurrentSection != NavigationSection.Board)
        {
            TolkWrapper.Speak("Select an item on your board first");
            return;
        }

        var card = _navigator.GetCurrentCard() as ItemCard;
        if (card == null)
        {
            TolkWrapper.Speak("Cannot move this");
            return;
        }

        if (!_navigator.CanMoveInCurrentState())
        {
            TolkWrapper.Speak("Cannot move right now");
            return;
        }

        // ActionHelper.MoveItem already speaks success/failure messages
        if (ActionHelper.MoveItem(card, true)) // true = to stash
        {
            RefreshAndAnnounce();
            _navigator.TriggerVisualSelection();
        }
    }

    private void HandleReorder(int direction)
    {
        // Works for both board and stash
        if (!_navigator.IsInPlayerSection())
        {
            TolkWrapper.Speak("Select an item on your board or stash first");
            return;
        }

        var card = _navigator.GetCurrentCard() as ItemCard;
        if (card == null)
        {
            TolkWrapper.Speak("Cannot reorder this");
            return;
        }

        if (!_navigator.CanMoveInCurrentState())
        {
            TolkWrapper.Speak("Cannot reorder right now");
            return;
        }

        var itemId = card.InstanceId;

        if (_navigator.IsInBoardSection())
        {
            int currentSlot = _navigator.GetCurrentBoardSlot();
            if (currentSlot < 0)
            {
                TolkWrapper.Speak("Cannot determine position");
                return;
            }

            if (ActionHelper.ReorderItem(card, currentSlot, direction, silent: true))
            {
                _navigator.Refresh();
                if (!_navigator.GoToItemById(itemId))
                {
                    _navigator.GoToBoardSlot(currentSlot + direction);
                }
                AnnounceReorderPosition(_navigator.GetCurrentBoardSlot(), card);
                _navigator.TriggerVisualSelection();
            }
        }
        else if (_navigator.IsInStashSection())
        {
            int currentSlot = _navigator.GetCurrentStashSlot();
            if (currentSlot < 0)
            {
                TolkWrapper.Speak("Cannot determine position");
                return;
            }

            if (ActionHelper.ReorderStashItem(card, currentSlot, direction, silent: true))
            {
                _navigator.Refresh();
                if (!_navigator.GoToItemById(itemId))
                {
                    _navigator.GoToStashSlot(currentSlot + direction);
                }
                string itemName = ItemReader.GetCardName(card);
                int newSlot = _navigator.GetCurrentStashSlot();
                TolkWrapper.Speak($"Position {newSlot + 1}");
                _navigator.TriggerVisualSelection();
            }
        }
    }

    private void RefreshAndAnnounce()
    {
        // Only refresh - don't announce to avoid duplicates
        // User already got feedback from the action itself
        // If they want to know current position, they can press an arrow key
        _navigator.Refresh();
        _navigator.TriggerVisualSelection();
    }

    /// <summary>
    /// Refresca el navegador (llamado externamente por eventos del juego).
    /// </summary>
    public void RefreshNavigator()
    {
        _navigator.Refresh();
    }

    /// <summary>
    /// Clears the detail line cache (called when cards are enchanted/upgraded).
    /// This ensures the next Ctrl+Up/Down read shows updated stats.
    /// </summary>
    public void ClearDetailCache()
    {
        _navigator.ClearDetailCache();
    }

    /// <summary>
    /// Verifica si hay contenido en el navegador.
    /// </summary>
    public bool HasContent()
    {
        return _navigator.HasContent() || _navigator.HasSelectionContent() || _navigator.HasBoardContent();
    }

    /// <summary>
    /// Fuerza el anuncio del estado. Usa el sistema de debounce para evitar spam.
    /// </summary>
    public void ForceAnnounceState()
    {
        // Usar el sistema de debounce centralizado
        StateChangePatch.TriggerRefreshAndAnnounce();
    }

    /// <summary>
    /// Anuncia inmediatamente sin debounce (para uso interno cuando se necesita).
    /// </summary>
    public void AnnounceStateImmediate()
    {
        _navigator.AnnounceState();
    }

    /// <summary>
    /// Coroutine que espera a que el juego cambie de estado y luego anuncia.
    /// </summary>
    private System.Collections.IEnumerator DelayedRefreshAndAnnounce()
    {
        // Esperar a que el juego procese el cambio de estado
        yield return new WaitForSeconds(0.5f);
        _navigator.Refresh();

        // Esperar un poco más para que el nuevo contenido se cargue
        yield return new WaitForSeconds(0.3f);
        _navigator.Refresh();

        // Auto-focus a la sección correcta según el nuevo estado
        var newState = StateChangePatch.GetCurrentRunState();
        AutoFocusForState(newState);

        // No anunciar aquí - StateChangePatch lo hará con debounce
    }

    /// <summary>
    /// Coroutine for upgrade/enchant that waits longer for animations.
    /// Game animations can take up to 12 seconds.
    /// </summary>
    private System.Collections.IEnumerator DelayedRefreshAfterUpgrade()
    {
        // Wait for game to process and start animation
        yield return new WaitForSeconds(1.5f);

        // Check if BoardManager is still processing
        // Poll every 0.5 seconds up to 10 seconds total
        float maxWait = 10f;
        float waited = 0f;

        while (waited < maxWait)
        {
            var boardManager = Singleton<BoardManager>.Instance;
            if (boardManager != null)
            {
                // Check if animation is done using reflection
                var isProcessingProp = boardManager.GetType().GetProperty("IsProcessingUpgradeOrFuseOrEnchant",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var isAnimatingProp = boardManager.GetType().GetProperty("IsPlayingUpgradeOrFuseOrEnchantAnimation",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                bool isProcessing = isProcessingProp != null && (bool)isProcessingProp.GetValue(boardManager);
                bool isAnimating = isAnimatingProp != null && (bool)isAnimatingProp.GetValue(boardManager);

                if (!isProcessing && !isAnimating)
                {
                    break; // Animation done
                }
            }

            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        // Final refresh
        yield return new WaitForSeconds(0.3f);
        _navigator.Refresh();

        TolkWrapper.Speak("Done");

        // Auto-focus
        var newState = StateChangePatch.GetCurrentRunState();
        AutoFocusForState(newState);
    }

    /// <summary>
    /// Llamado cuando cambia el estado de combate.
    /// </summary>
    public void OnCombatStateChanged(bool inCombat)
    {
        _navigator.SetCombatMode(inCombat);

        if (inCombat)
        {
            // Mensaje corto
            TolkWrapper.Speak("Combat");
        }
        // No anunciar "Exiting combat" - el siguiente estado lo dirá
    }

    /// <summary>
    /// Llamado cuando se abre/cierra el stash.
    /// </summary>
    public void OnStorageToggled(bool isOpen)
    {
        _navigator.SetStashState(isOpen);

        if (isOpen)
        {
            // Usar coroutine para dar tiempo a que se actualice el stash
            Plugin.Instance.StartCoroutine(DelayedStashAnnounce());
        }
        else
        {
            TolkWrapper.Speak("Stash closed");
            // If user was navigating the stash, return to section before stash was opened
            if (_navigator.CurrentSection == NavigationSection.Stash)
            {
                var previousSection = _navigator.GetSectionBeforeStash();
                _navigator.SetSectionSilent(previousSection);
                _navigator.Refresh();
            }
        }
    }

    /// <summary>
    /// Coroutine para anunciar el stash después de un pequeño delay.
    /// </summary>
    private System.Collections.IEnumerator DelayedStashAnnounce()
    {
        // Esperar a que el juego actualice el stash
        yield return new WaitForSeconds(0.2f);

        // Refrescar para obtener los items del stash
        _navigator.Refresh();

        int stashCount = _navigator.GetStashItemCount();
        if (stashCount > 0)
        {
            // Navegar al stash - esto anuncia la sección y el primer item
            _navigator.GoToSection(NavigationSection.Stash);
        }
        else
        {
            TolkWrapper.Speak("Stash opened, empty. Press Space to close.");
        }
    }

    /// <summary>
    /// Llamado cuando entramos/salimos del ReplayState (post-combat).
    /// </summary>
    public void OnReplayStateChanged(bool inReplayState)
    {
        _navigator.SetReplayMode(inReplayState);

        if (inReplayState)
        {
            // Mensaje corto - el usuario aprenderá los controles
            TolkWrapper.Speak("Combat ended. Enter to continue.");
        }
        else
        {
            // Al salir del replay, refrescar la UI después de un delay
            Plugin.Instance.StartCoroutine(DelayedRefreshAfterReplayExit());
        }
    }

    /// <summary>
    /// Refresca la UI después de salir del ReplayState.
    /// </summary>
    private System.Collections.IEnumerator DelayedRefreshAfterReplayExit()
    {
        // Esperar a que el juego cargue el nuevo estado
        yield return new WaitForSeconds(0.5f);

        RefreshNavigator();
        Plugin.Logger.LogInfo($"DelayedRefreshAfterReplayExit: Refreshed, state={_navigator.GetStateDescription()}");

        // Ir a la sección de selección sin anunciar (no quedarse en Hero)
        _navigator.SetSectionSilent(NavigationSection.Selection);

        // No anunciar aquí - StateChangePatch lo hará con debounce
    }

    /// <summary>
    /// Triggers the Continue action in ReplayState.
    /// </summary>
    public void TriggerReplayContinue()
    {
        try
        {
            // Get the current state and check if it's ReplayState
            var currentState = AppState.CurrentState;
            if (currentState == null)
            {
                Plugin.Logger.LogWarning("TriggerReplayContinue: CurrentState is null");
                return;
            }

            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType == null)
            {
                Plugin.Logger.LogWarning("TriggerReplayContinue: ReplayState type not found");
                return;
            }

            if (!replayStateType.IsInstanceOfType(currentState))
            {
                Plugin.Logger.LogInfo($"TriggerReplayContinue: Current state is {currentState.GetType().Name}, not ReplayState");
                // No estamos en ReplayState, forzar salir del modo replay
                _navigator.SetReplayMode(false);
                return;
            }

            // Call Exit() on the current ReplayState
            var exitMethod = replayStateType.GetMethod("Exit");
            if (exitMethod != null)
            {
                TolkWrapper.Speak("Continuing");
                exitMethod.Invoke(currentState, null);
                // NO llamar a SetReplayMode(false) aquí - OnReplayStateChanged lo hará cuando el estado cambie
            }
            else
            {
                Plugin.Logger.LogWarning("TriggerReplayContinue: Exit method not found");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TriggerReplayContinue error: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers the Replay action in ReplayState.
    /// </summary>
    public void TriggerReplayReplay()
    {
        try
        {
            var currentState = AppState.CurrentState;
            if (currentState == null) return;

            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType == null || !replayStateType.IsInstanceOfType(currentState))
            {
                // No estamos en ReplayState
                _navigator.SetReplayMode(false);
                return;
            }

            var replayMethod = replayStateType.GetMethod("Replay");
            if (replayMethod != null)
            {
                TolkWrapper.Speak("Replaying combat");
                replayMethod.Invoke(currentState, null);
                // Salir del modo recap si estábamos en él (R inicia animación)
                _navigator.SetRecapMode(false);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TriggerReplayReplay error: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers the Recap action in ReplayState.
    /// </summary>
    public void TriggerReplayRecap()
    {
        try
        {
            var currentState = AppState.CurrentState;
            if (currentState == null) return;

            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType == null || !replayStateType.IsInstanceOfType(currentState))
            {
                // No estamos en ReplayState
                _navigator.SetReplayMode(false);
                return;
            }

            var recapMethod = replayStateType.GetMethod("Recap");
            if (recapMethod != null)
            {
                TolkWrapper.Speak("Recap. V hero, F enemy, G enemy board, B your board, H combat stats.");
                recapMethod.Invoke(currentState, null);
                // Activar modo recap - ahora G funcionará
                _navigator.SetRecapMode(true);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TriggerReplayRecap error: {ex.Message}");
        }
    }
}
