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
public class GameplayScreen : IAccessibleScreen
{
    public string ScreenName => "Gameplay";

    private readonly GameplayNavigator _navigator;
    private bool _isValid = true;
    private ERunState _lastState = ERunState.Choice;

    public GameplayScreen()
    {
        _navigator = new GameplayNavigator();
    }

    public void HandleInput(AccessibleKey key)
    {
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

            case AccessibleKey.GoToEnemy:
                _navigator.ReadEnemyInfo();
                break;

            // Navegación dentro de la sección actual
            case AccessibleKey.Right:
                _navigator.Next();
                break;

            case AccessibleKey.Left:
                _navigator.Previous();
                break;

            // Up/Down para navegación vertical en Hero mode o detallada
            case AccessibleKey.Up:
                if (_navigator.IsInHeroSection)
                    _navigator.Previous();
                break;

            case AccessibleKey.Down:
                if (_navigator.IsInHeroSection)
                    _navigator.Next();
                break;

            // Información detallada línea por línea (Ctrl+flecha)
            case AccessibleKey.DetailUp:
                _navigator.ReadDetailPrevious();
                break;

            case AccessibleKey.DetailDown:
                _navigator.ReadDetailNext();
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

            // Cancelar/Releer estado
            case AccessibleKey.Back:
                _navigator.AnnounceState();
                break;

            // Buffer de mensajes
            case AccessibleKey.NextMessage:
                MessageBuffer.ReadNewest();
                break;

            case AccessibleKey.PrevMessage:
                MessageBuffer.ReadPrevious();
                break;

            // Espacio - ir al stash
            case AccessibleKey.Space:
                _navigator.GoToStash();
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
        }
    }

    public string GetHelp()
    {
        return "Left/Right: Navigate. Tab: Switch section. Space: Stash. " +
               "B: Board. V: Hero. C: Choices. " +
               "Enter: Select/Buy/Sell. E: Exit. R: Refresh. " +
               "Shift+Up/Down: Move to board/stash. Shift+Left/Right: Reorder. " +
               "Ctrl+Up/Down: Read details. Period/Comma: Messages.";
    }

    public void OnFocus()
    {
        _lastState = StateChangePatch.GetCurrentRunState();
        _navigator.Refresh();

        // Auto-focus a la sección correcta según el estado
        AutoFocusForState(_lastState);

        // No anunciar aquí - DelayedInitialize lo hará después de que el contenido esté listo
        // _navigator.AnnounceState();
    }

    /// <summary>
    /// Llamado cuando cambia el estado del juego.
    /// </summary>
    public void OnStateChanged(ERunState newState, bool announceChange = true)
    {
        bool stateChanged = newState != _lastState;
        _lastState = newState;

        // Anunciar el cambio de estado
        if (announceChange && stateChanged)
        {
            string stateDesc = StateChangePatch.GetStateDescription(newState);
            TolkWrapper.Speak(stateDesc);
        }

        // Hacer refresh y auto-focus
        Plugin.Instance.StartCoroutine(RefreshAndAutoFocus(newState, stateChanged));
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
                // En combate, ir al board del jugador
                if (_navigator.HasBoardContent())
                {
                    _navigator.GoToSection(NavigationSection.Board);
                }
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
        // Si estamos en Hero, leer toda la info
        if (_navigator.IsInHeroSection)
        {
            _navigator.ReadAllHeroStats();
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

        // Si estamos en Board/Stash, es venta
        if (_navigator.IsInPlayerSection())
        {
            var card = _navigator.GetCurrentCard();
            if (card != null)
            {
                HandleSellConfirm(card);
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
            ActionHelper.BuyItem(itemCard);
            RefreshAndAnnounce();
        }
        else
        {
            int price = ItemReader.GetBuyPrice(card);
            var ui = new ConfirmActionUI(ConfirmActionType.Buy, name, price,
                onConfirm: () => {
                    ActionHelper.BuyItem(itemCard);
                    RefreshAndAnnounce();
                },
                onCancel: () => TolkWrapper.Speak("Cancelled"));
            AccessibilityMgr.ShowUI(ui);
        }
    }

    private void SelectSkill(Card card)
    {
        var skillCard = card as SkillCard;
        if (skillCard == null) { TolkWrapper.Speak("Not a skill"); return; }

        ActionHelper.SelectSkill(skillCard);
        RefreshAndAnnounce();
    }

    private void SelectEncounterDirect(Card card)
    {
        string name = ItemReader.GetCardName(card);

        string flavorText = ItemReader.GetFlavorText(card);
        if (!string.IsNullOrEmpty(flavorText))
        {
            TolkWrapper.Speak(flavorText);
        }

        TolkWrapper.Speak($"Selecting {name}");
        ActionHelper.SelectEncounter(card);

        Plugin.Instance.StartCoroutine(DelayedRefresh());
    }

    private void HandleSellConfirm(Card card)
    {
        var itemCard = card as ItemCard;
        if (itemCard == null) { TolkWrapper.Speak("Cannot sell this"); return; }

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
        string destination = toStash ? "stash" : "board";
        string name = ItemReader.GetCardName(card);

        ActionHelper.MoveItem(card, toStash);
        TolkWrapper.Speak($"Moved {name} to {destination}");
        RefreshAndAnnounce();
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

        string name = ItemReader.GetCardName(card);
        ActionHelper.MoveItem(card, false); // false = to board
        TolkWrapper.Speak($"Moved {name} to board");
        RefreshAndAnnounce();
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

        string name = ItemReader.GetCardName(card);
        ActionHelper.MoveItem(card, true); // true = to stash
        TolkWrapper.Speak($"Moved {name} to stash");
        RefreshAndAnnounce();
    }

    private void HandleReorder(int direction)
    {
        // Solo funciona si estamos en el board
        if (!_navigator.IsInBoardSection())
        {
            TolkWrapper.Speak("Select an item on your board first");
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

        int currentSlot = _navigator.GetCurrentBoardSlot();
        if (currentSlot < 0)
        {
            TolkWrapper.Speak("Cannot determine position");
            return;
        }

        if (ActionHelper.ReorderItem(card, currentSlot, direction))
        {
            RefreshAndAnnounce();
        }
    }

    private void RefreshAndAnnounce()
    {
        _navigator.Refresh();
        _navigator.AnnounceCurrentItem();
    }

    /// <summary>
    /// Refresca el navegador (llamado externamente por eventos del juego).
    /// </summary>
    public void RefreshNavigator()
    {
        _navigator.Refresh();
    }

    /// <summary>
    /// Verifica si hay contenido en el navegador.
    /// </summary>
    public bool HasContent()
    {
        return _navigator.HasContent() || _navigator.HasSelectionContent() || _navigator.HasBoardContent();
    }

    /// <summary>
    /// Fuerza el anuncio del estado (simula lo que hace backspace).
    /// </summary>
    public void ForceAnnounceState()
    {
        _navigator.AnnounceState();
    }

    private System.Collections.IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(0.5f);
        _navigator.Refresh();
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
        yield return new WaitForSeconds(0.5f);
        _navigator.Refresh();

        // Auto-focus a la sección correcta según el nuevo estado
        var newState = StateChangePatch.GetCurrentRunState();
        AutoFocusForState(newState);

        // Anunciar el nuevo estado
        _navigator.AnnounceState();

        // Un refresh más por si acaso
        yield return new WaitForSeconds(0.3f);
        _navigator.Refresh();
    }

    /// <summary>
    /// Llamado cuando cambia el estado de combate.
    /// </summary>
    public void OnCombatStateChanged(bool inCombat)
    {
        _navigator.SetCombatMode(inCombat);

        if (inCombat)
        {
            TolkWrapper.Speak("Combat started. Press V for your stats, F for enemy stats.");
        }
        else
        {
            TolkWrapper.Speak("Combat ended.");
        }
    }
}
