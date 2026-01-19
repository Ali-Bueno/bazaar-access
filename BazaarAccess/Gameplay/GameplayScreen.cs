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
        // En modo replay (post-combate), Enter/R/E + V/F/G para stats y tablero oponente
        if (_navigator.IsInReplayMode)
        {
            // Si estamos navegando el tablero del oponente (modo enemigo)
            if (_navigator.IsInEnemyMode)
            {
                switch (key)
                {
                    // Arrows: Navigate items/skills
                    case AccessibleKey.Left:
                        _navigator.EnemyNavigateLeft();
                        return;

                    case AccessibleKey.Right:
                        _navigator.EnemyNavigateRight();
                        return;

                    // Ctrl+Left/Right: Switch subsection (Items <-> Skills)
                    case AccessibleKey.DetailLeft:
                        _navigator.EnemyPreviousSubsection();
                        return;

                    case AccessibleKey.DetailRight:
                        _navigator.EnemyNextSubsection();
                        return;

                    // Up/Down: Same as Ctrl+Up/Down for consistency
                    case AccessibleKey.Up:
                        if (_navigator.IsInEnemySkillsSubsection)
                            _navigator.EnemySkillNext();
                        else
                            _navigator.EnemyDetailNext();
                        return;

                    case AccessibleKey.Down:
                        if (_navigator.IsInEnemySkillsSubsection)
                            _navigator.EnemySkillPrevious();
                        else
                            _navigator.EnemyDetailPrevious();
                        return;

                    // Ctrl+Up/Down: In Items = read detail lines, In Skills = navigate skills
                    case AccessibleKey.DetailUp:
                        if (_navigator.IsInEnemySkillsSubsection)
                            _navigator.EnemySkillNext();
                        else
                            _navigator.EnemyDetailNext();
                        return;

                    case AccessibleKey.DetailDown:
                        if (_navigator.IsInEnemySkillsSubsection)
                            _navigator.EnemySkillPrevious();
                        else
                            _navigator.EnemyDetailPrevious();
                        return;

                    // Enter: Read full description
                    case AccessibleKey.Confirm:
                        _navigator.ReadCurrentEnemyItemDetails();
                        return;

                    // G or F: Refresh/re-announce
                    case AccessibleKey.GoToEnemy:
                    case AccessibleKey.GoToStash:
                        _navigator.EnterOpponentBoardMode();
                        return;

                    // Backspace: Exit enemy mode
                    case AccessibleKey.Back:
                        _navigator.ExitEnemyMode();
                        TolkWrapper.Speak("Exited opponent board. Enter to continue, or G to return.");
                        return;
                }
            }

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

                case AccessibleKey.GoToStash: // G key - go to opponent board (only in recap mode)
                    if (_navigator.IsInRecapMode)
                    {
                        _navigator.EnterOpponentBoardMode();
                    }
                    else
                    {
                        TolkWrapper.Speak("Press E for Recap first, then G for opponent board.");
                    }
                    break;

                case AccessibleKey.WinsInfo:
                    // W key - wins info (also available during replay)
                    _navigator.AnnounceWins();
                    break;

                case AccessibleKey.Back:
                    // Solo recordar brevemente, no repetir el mensaje completo
                    if (_navigator.IsInRecapMode)
                    {
                        TolkWrapper.Speak("Recap mode. Enter to continue, G for opponent board.");
                    }
                    else
                    {
                        TolkWrapper.Speak("Post-combat. Enter to continue, R to replay, E for recap.");
                    }
                    break;

                // Ignorar todas las demás teclas durante replay mode
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

        // En modo combate, solo permitir V (Hero), F (Enemy) y Ctrl+flechas para navegar Hero/Enemy
        if (inCombat)
        {
            // Si estamos en modo enemigo durante combate
            if (_navigator.IsInEnemyMode)
            {
                switch (key)
                {
                    case AccessibleKey.DetailUp:
                        _navigator.EnemyNext();
                        return;

                    case AccessibleKey.DetailDown:
                        _navigator.EnemyPrevious();
                        return;

                    case AccessibleKey.Confirm:
                        _navigator.ReadCurrentEnemyItemDetails();
                        return;

                    case AccessibleKey.GoToEnemy:
                        _navigator.ReadEnemyInfo();
                        return;

                    case AccessibleKey.GoToHero:
                        _navigator.ExitEnemyMode();
                        _navigator.GoToHero();
                        return;

                    case AccessibleKey.Back:
                        _navigator.ExitEnemyMode();
                        TolkWrapper.Speak("Exited enemy view");
                        return;
                }
            }

            switch (key)
            {
                case AccessibleKey.GoToHero:
                    _navigator.GoToHero();
                    break;

                case AccessibleKey.GoToEnemy:
                    _navigator.ReadEnemyInfo();
                    break;

                case AccessibleKey.ToggleCombatMode:
                    CombatDescriber.ToggleMode();
                    break;

                // Ctrl+Up/Down para navegar stats/skills en Hero durante combate
                case AccessibleKey.DetailUp:
                    if (_navigator.IsInHeroSection)
                        _navigator.HeroNext();
                    break;

                case AccessibleKey.DetailDown:
                    if (_navigator.IsInHeroSection)
                        _navigator.HeroPrevious();
                    break;

                // Ctrl+Left/Right para cambiar subsección en Hero durante combate
                case AccessibleKey.DetailLeft:
                    if (_navigator.IsInHeroSection)
                        _navigator.HeroPreviousSubsection();
                    break;

                case AccessibleKey.DetailRight:
                    if (_navigator.IsInHeroSection)
                        _navigator.HeroNextSubsection();
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

        // Si estamos en modo enemigo, manejar navegación de items del enemigo
        if (_navigator.IsInEnemyMode)
        {
            switch (key)
            {
                case AccessibleKey.DetailUp:
                    _navigator.EnemyNext();
                    return;

                case AccessibleKey.DetailDown:
                    _navigator.EnemyPrevious();
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
                // Si el stash está cerrado, abrirlo primero
                if (!_navigator.IsStashOpen())
                {
                    _navigator.ToggleStash();
                }
                else
                {
                    _navigator.GoToStash();
                }
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

            // Ctrl+Up/Down: En Hero navega stats/skills, en otras secciones lee detalles
            case AccessibleKey.DetailUp:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroNext();  // Ctrl+Up = siguiente stat/skill
                else
                    _navigator.ReadDetailNext();  // Ctrl+Up = siguiente línea (invertido)
                break;

            case AccessibleKey.DetailDown:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroPrevious();  // Ctrl+Down = anterior stat/skill
                else
                    _navigator.ReadDetailPrevious();  // Ctrl+Down = línea anterior (invertido)
                break;

            // Ctrl+Left/Right: Cambiar subsección en Hero (Stats/Skills)
            case AccessibleKey.DetailLeft:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroPreviousSubsection();
                break;

            case AccessibleKey.DetailRight:
                if (_navigator.IsInHeroSection)
                    _navigator.HeroNextSubsection();
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

            // Espacio - abrir/cerrar stash
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

        if (ActionHelper.UpgradeItem(card))
        {
            // Refresh after successful upgrade
            Plugin.Instance.StartCoroutine(DelayedRefreshAndAnnounce());
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
        return "Left/Right: Navigate items. Up/Down: Read item details line by line. " +
               "Tab: Switch section. Space: Stash. " +
               "B: Board. V: Hero. C: Choices. F: Enemy info. I: Property info. W: Wins. " +
               "Enter: Select/Buy/Sell. E: Exit. R: Refresh. Shift+U: Upgrade. " +
               "Shift+Up/Down: Move to board/stash. Shift+Left/Right: Reorder. " +
               "Ctrl+Up/Down: Read item details or navigate Hero stats. " +
               "Ctrl+Left/Right: Switch Hero subsection (Stats/Skills). " +
               "Period/Comma: Messages.";
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
        // Solo decir el nombre, sin "Selecting"
        string name = ItemReader.GetCardName(card);
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

        // Calcular el nuevo slot donde estará el item
        int newSlot = currentSlot + direction;

        if (ActionHelper.ReorderItem(card, currentSlot, direction))
        {
            // Refrescar primero para actualizar _boardIndices
            _navigator.Refresh();
            // Ahora mover el índice del navegador al nuevo slot para seguir al item
            _navigator.GoToBoardSlot(newSlot);
            // Anunciar el item (que es el mismo que movimos)
            _navigator.AnnounceCurrentItem();
            // Activar selección visual
            _navigator.TriggerVisualSelection();
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
                TolkWrapper.Speak("Recap. Press G for opponent board.");
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
