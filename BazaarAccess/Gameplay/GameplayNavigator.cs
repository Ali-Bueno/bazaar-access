using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine.EventSystems;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Sección actual de navegación.
/// </summary>
public enum NavigationSection
{
    Selection,  // Lo que el juego te ofrece (encuentros, tienda, recompensas) + acciones
    Board,      // Tus items equipados
    Stash,      // Tu almacén
    Skills,     // Tus habilidades
    Hero        // Stats del héroe
}

/// <summary>
/// Subsecciones dentro de Hero.
/// </summary>
public enum HeroSubsection
{
    Stats,      // Estadísticas del héroe (vida, oro, nivel, etc.)
    Skills      // Habilidades equipadas del héroe
}

/// <summary>
/// Tipos de items navegables (cartas o acciones).
/// </summary>
public enum NavItemType
{
    Card,       // Una carta normal
    Exit,       // Acción de salir
    Reroll      // Acción de refrescar
}

/// <summary>
/// Item navegable (puede ser carta o acción).
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
/// Navegador de gameplay fiel al flujo original del juego.
/// - Selección: lo que puedes elegir (encuentros, items de tienda)
/// - Tablero: tus items (puedes vender, mover)
/// </summary>
public class GameplayNavigator
{
    private NavigationSection _currentSection = NavigationSection.Selection;
    private int _currentIndex = 0;

    // Cache de items navegables (cartas + acciones)
    private List<NavItem> _selectionItems = new List<NavItem>();  // SelectionSet + Exit/Reroll
    private List<int> _boardIndices = new List<int>();        // Slots ocupados del tablero
    private List<int> _stashIndices = new List<int>();        // Slots ocupados del stash
    private List<SkillCard> _playerSkills = new List<SkillCard>();  // Skills equipadas del jugador

    // Stats del héroe para navegación
    private int _heroStatIndex = 0;
    private HeroSubsection _heroSubsection = HeroSubsection.Stats;
    private int _heroSkillIndex = 0;

    // Modo combate
    private bool _inCombat = false;

    // Estado del stash (abierto/cerrado)
    private bool _stashOpen = false;

    // Sección anterior antes de abrir el stash (para restaurar al cerrar)
    private NavigationSection _sectionBeforeStash = NavigationSection.Selection;

    // Modo replay (post-combate)
    private bool _inReplayMode = false;

    // Navegación detallada línea por línea
    private List<string> _detailLines = new List<string>();
    private int _detailIndex = -1;
    private Card _detailCard = null;

    // Modo enemigo (para navegar items del oponente)
    private bool _enemyMode = false;
    private List<int> _enemyItemIndices = new List<int>();
    private List<int> _enemySkillIndices = new List<int>();
    private int _enemyItemIndex = 0;
    private static readonly EPlayerAttributeType[] HeroStats = new[]
    {
        EPlayerAttributeType.Health,
        EPlayerAttributeType.HealthMax,
        EPlayerAttributeType.Gold,
        EPlayerAttributeType.Level,
        EPlayerAttributeType.Experience,
        EPlayerAttributeType.Prestige,
        EPlayerAttributeType.Shield,
        EPlayerAttributeType.Poison,
        EPlayerAttributeType.Burn,
        EPlayerAttributeType.HealthRegen,
        EPlayerAttributeType.CritChance,
        EPlayerAttributeType.Income
    };

    public NavigationSection CurrentSection => _currentSection;
    public bool IsInHeroSection => _currentSection == NavigationSection.Hero;
    public HeroSubsection CurrentHeroSubsection => _heroSubsection;
    public bool IsInEnemyMode => _enemyMode;

    public ERunState GetCurrentState()
    {
        return StateChangePatch.GetCurrentRunState();
    }

    public string GetStateDescription()
    {
        return StateChangePatch.GetStateDescription(GetCurrentState());
    }

    /// <summary>
    /// Actualiza todas las listas de cartas.
    /// </summary>
    public void Refresh()
    {
        RefreshSelection();
        RefreshBoard();
        RefreshStash();
        RefreshSkills();

        // Don't auto-switch sections when current section is empty
        // This prevents unwanted navigation changes when user sells/moves items
        // Section changes should be explicit (via AutoFocusForState, GoToSection, etc.)

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
            // Agregar cartas del SelectionSet
            var selectionSet = Data.CurrentState?.SelectionSet;
            if (selectionSet != null)
            {
                foreach (var id in selectionSet)
                {
                    var card = Data.GetCard(id);
                    if (card != null) _selectionItems.Add(NavItem.FromCard(card));
                }
            }

            // Agregar acciones disponibles al final
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

        for (int i = 0; i < bm.playerItemSockets.Length; i++)
        {
            if (bm.playerItemSockets[i]?.CardController?.CardData != null)
                _boardIndices.Add(i);
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

    private void RefreshSkills()
    {
        _playerSkills.Clear();
        try
        {
            // Usar Data.Run.Player.Skills que se actualiza inmediatamente al equipar
            var skills = Data.Run?.Player?.Skills;
            if (skills != null)
            {
                _playerSkills.AddRange(skills);
                Plugin.Logger.LogInfo($"RefreshSkills: Found {_playerSkills.Count} skills from Player.Skills");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"RefreshSkills error: {ex.Message}");
        }
    }

    // --- Navegación ---

    /// <summary>
    /// Cambia a la siguiente sección con contenido.
    /// </summary>
    public void NextSection()
    {
        var sections = GetAvailableSections();
        if (sections.Count <= 1) return;

        int idx = sections.IndexOf(_currentSection);
        int nextIdx = (idx + 1) % sections.Count;
        _currentSection = sections[nextIdx];
        _currentIndex = 0;
        _heroStatIndex = 0;
        AnnounceSection();
    }

    /// <summary>
    /// Va directamente a una sección específica.
    /// </summary>
    public void GoToSection(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        _heroStatIndex = 0;
        _heroSubsection = HeroSubsection.Stats;
        _heroSkillIndex = 0;
        AnnounceSection();
    }

    /// <summary>
    /// Cambia a una sección sin anunciar (para uso interno).
    /// </summary>
    public void SetSectionSilent(NavigationSection section)
    {
        _currentSection = section;
        _currentIndex = 0;
        _heroStatIndex = 0;
        _heroSubsection = HeroSubsection.Stats;
        _heroSkillIndex = 0;
    }

    /// <summary>
    /// Va a la sección de choices/selection.
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
    /// Va a la sección del board.
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
    /// Va a la sección del héroe.
    /// </summary>
    public void GoToHero()
    {
        GoToSection(NavigationSection.Hero);
    }

    /// <summary>
    /// Cambia a la siguiente subsección de Hero (Stats -> Skills -> Stats).
    /// </summary>
    public void HeroNextSubsection()
    {
        if (_currentSection != NavigationSection.Hero) return;

        if (_heroSubsection == HeroSubsection.Stats)
        {
            if (_playerSkills.Count > 0)
            {
                _heroSubsection = HeroSubsection.Skills;
                _heroSkillIndex = 0;
                AnnounceHeroSubsection();
            }
            else
            {
                TolkWrapper.Speak("No skills equipped");
            }
        }
        else
        {
            _heroSubsection = HeroSubsection.Stats;
            _heroStatIndex = 0;
            AnnounceHeroSubsection();
        }
    }

    /// <summary>
    /// Cambia a la subsección anterior de Hero.
    /// </summary>
    public void HeroPreviousSubsection()
    {
        if (_currentSection != NavigationSection.Hero) return;

        if (_heroSubsection == HeroSubsection.Skills)
        {
            _heroSubsection = HeroSubsection.Stats;
            _heroStatIndex = 0;
            AnnounceHeroSubsection();
        }
        else
        {
            if (_playerSkills.Count > 0)
            {
                _heroSubsection = HeroSubsection.Skills;
                _heroSkillIndex = 0;
                AnnounceHeroSubsection();
            }
            else
            {
                TolkWrapper.Speak("No skills equipped");
            }
        }
    }

    /// <summary>
    /// Anuncia la subsección actual de Hero.
    /// </summary>
    private void AnnounceHeroSubsection()
    {
        // Only announce subsection name + count, not the first item
        // User will hear the item when they press Ctrl+arrows
        if (_heroSubsection == HeroSubsection.Stats)
        {
            TolkWrapper.Speak($"Hero stats, {HeroStats.Length} stats");
        }
        else
        {
            TolkWrapper.Speak($"Hero skills, {_playerSkills.Count} skills");
        }
    }

    /// <summary>
    /// Anuncia la skill actual del héroe con su descripción.
    /// </summary>
    private void AnnounceHeroSkill()
    {
        if (_heroSkillIndex < 0 || _heroSkillIndex >= _playerSkills.Count)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        var skill = _playerSkills[_heroSkillIndex];
        if (skill == null)
        {
            TolkWrapper.Speak("Empty slot");
            return;
        }

        string name = ItemReader.GetCardName(skill);
        string desc = ItemReader.GetFullDescription(skill);

        if (!string.IsNullOrEmpty(desc))
        {
            TolkWrapper.Speak($"{name}: {desc}, {_heroSkillIndex + 1} of {_playerSkills.Count}");
        }
        else
        {
            TolkWrapper.Speak($"{name}, {_heroSkillIndex + 1} of {_playerSkills.Count}");
        }

        // Activar selección visual de la skill
        TriggerVisualSelectionForHeroSkill();
    }

    /// <summary>
    /// Activa la selección visual para la skill actual del héroe.
    /// </summary>
    private void TriggerVisualSelectionForHeroSkill()
    {
        try
        {
            if (_heroSubsection != HeroSubsection.Skills) return;

            var bm = GetBoardManager();
            if (bm?.playerSkillSockets == null) return;

            // Resetear todas las selecciones
            ResetAllCardSelections(bm);

            // Encontrar el socket de skill que corresponde al índice actual
            if (_heroSkillIndex >= 0 && _heroSkillIndex < bm.playerSkillSockets.Length)
            {
                var controller = bm.playerSkillSockets[_heroSkillIndex]?.CardController;
                if (controller != null)
                {
                    // Simular evento de puntero para sonidos y tooltips
                    var eventSystem = EventSystem.current;
                    var pointerData = new PointerEventData(eventSystem)
                    {
                        position = UnityEngine.Input.mousePosition
                    };
                    controller.OnPointerEnter(pointerData);
                    controller.HoverMove();
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerVisualSelectionForHeroSkill error: {ex.Message}");
        }
    }

    /// <summary>
    /// Lee información detallada de la skill actual del héroe.
    /// </summary>
    public void ReadHeroSkillDetails()
    {
        if (_heroSubsection != HeroSubsection.Skills) return;
        if (_heroSkillIndex < 0 || _heroSkillIndex >= _playerSkills.Count) return;

        var skill = _playerSkills[_heroSkillIndex];
        if (skill == null) return;

        TolkWrapper.Speak(ItemReader.GetDetailedDescription(skill));
    }

    /// <summary>
    /// Navega al siguiente stat/skill en Hero (Ctrl+Up).
    /// </summary>
    public void HeroNext()
    {
        if (_currentSection != NavigationSection.Hero) return;

        if (_heroSubsection == HeroSubsection.Stats)
        {
            _heroStatIndex = (_heroStatIndex + 1) % HeroStats.Length;
            AnnounceHeroStat();
        }
        else
        {
            if (_playerSkills.Count == 0) return;
            _heroSkillIndex = (_heroSkillIndex + 1) % _playerSkills.Count;
            AnnounceHeroSkill();
        }
    }

    /// <summary>
    /// Navega al stat/skill anterior en Hero (Ctrl+Down).
    /// </summary>
    public void HeroPrevious()
    {
        if (_currentSection != NavigationSection.Hero) return;

        if (_heroSubsection == HeroSubsection.Stats)
        {
            _heroStatIndex = (_heroStatIndex - 1 + HeroStats.Length) % HeroStats.Length;
            AnnounceHeroStat();
        }
        else
        {
            if (_playerSkills.Count == 0) return;
            _heroSkillIndex = (_heroSkillIndex - 1 + _playerSkills.Count) % _playerSkills.Count;
            AnnounceHeroSkill();
        }
    }

    /// <summary>
    /// Obtiene la skill actual del héroe para leer detalles.
    /// </summary>
    public Card GetCurrentHeroSkill()
    {
        if (_heroSubsection != HeroSubsection.Skills) return null;
        if (_heroSkillIndex < 0 || _heroSkillIndex >= _playerSkills.Count) return null;

        return _playerSkills[_heroSkillIndex];
    }

    /// <summary>
    /// Activa o desactiva el modo combate.
    /// En combate solo se permite navegar al Hero.
    /// </summary>
    public void SetCombatMode(bool inCombat)
    {
        _inCombat = inCombat;
        if (inCombat)
        {
            // En combate, forzar ir a Hero
            GoToSection(NavigationSection.Hero);
        }
    }

    /// <summary>
    /// Indica si estamos en modo combate.
    /// </summary>
    public bool IsInCombat => _inCombat;

    /// <summary>
    /// Indica si estamos en modo replay (post-combate).
    /// </summary>
    public bool IsInReplayMode => _inReplayMode;

    /// <summary>
    /// Activa o desactiva el modo replay (post-combate).
    /// </summary>
    public void SetReplayMode(bool inReplayMode)
    {
        _inReplayMode = inReplayMode;
        if (inReplayMode)
        {
            // Salir del modo combate si entramos en replay
            _inCombat = false;
        }
    }

    /// <summary>
    /// Actualiza el estado del stash (abierto/cerrado).
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
            // Refrescar el stash cuando se abre
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
    /// Lee la información del enemigo/NPC actual.
    /// Si no estamos en combate, entra en modo enemigo para navegar items.
    /// Durante combate, solo lee stats.
    /// </summary>
    public void ReadEnemyInfo()
    {
        try
        {
            var opponent = Data.Run?.Opponent;
            if (opponent == null)
            {
                TolkWrapper.Speak("No enemy");
                _enemyMode = false;
                return;
            }

            var parts = new List<string>();

            // Nombre del oponente (si es PvP tendrá nombre, si es NPC será el encuentro)
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                parts.Add($"Opponent: {pvpOpponent.Name}");
            }
            else
            {
                parts.Add("Enemy");
            }

            // Vida
            if (opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int health))
            {
                parts.Add($"Health: {health}");
            }
            if (opponent.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out int maxHealth))
            {
                parts.Add($"of {maxHealth}");
            }

            // Escudo
            if (opponent.Attributes.TryGetValue(EPlayerAttributeType.Shield, out int shield) && shield > 0)
            {
                parts.Add($"Shield: {shield}");
            }

            // Solo permitir navegación de items fuera de combate
            if (!_inCombat)
            {
                _enemyMode = true;
                RefreshEnemyItems();

                // Items del enemigo
                if (_enemyItemIndices.Count > 0)
                {
                    parts.Add($"{_enemyItemIndices.Count} items");
                }
                if (_enemySkillIndices.Count > 0)
                {
                    parts.Add($"{_enemySkillIndices.Count} skills");
                }
            }

            TolkWrapper.Speak(string.Join(", ", parts));
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ReadEnemyInfo error: {ex.Message}");
            TolkWrapper.Speak("Cannot read enemy info");
            _enemyMode = false;
        }
    }

    /// <summary>
    /// Refresca la lista de items del enemigo.
    /// </summary>
    private void RefreshEnemyItems()
    {
        _enemyItemIndices.Clear();
        _enemySkillIndices.Clear();
        _enemyItemIndex = 0;

        var bm = GetBoardManager();
        if (bm == null) return;

        // Items del enemigo
        if (bm.opponentItemSockets != null)
        {
            for (int i = 0; i < bm.opponentItemSockets.Length; i++)
            {
                if (bm.opponentItemSockets[i]?.CardController?.CardData != null)
                    _enemyItemIndices.Add(i);
            }
        }

        // Skills del enemigo
        if (bm.opponentSkillSockets != null)
        {
            for (int i = 0; i < bm.opponentSkillSockets.Length; i++)
            {
                if (bm.opponentSkillSockets[i]?.CardController?.CardData != null)
                    _enemySkillIndices.Add(i);
            }
        }

        Plugin.Logger.LogInfo($"RefreshEnemyItems: {_enemyItemIndices.Count} items, {_enemySkillIndices.Count} skills");
    }

    /// <summary>
    /// Sale del modo enemigo.
    /// </summary>
    public void ExitEnemyMode()
    {
        _enemyMode = false;
        _enemyItemIndex = 0;
    }

    /// <summary>
    /// Navega al siguiente item del enemigo (Ctrl+Up en modo enemigo).
    /// </summary>
    public void EnemyNext()
    {
        if (!_enemyMode) return;

        int totalItems = _enemyItemIndices.Count + _enemySkillIndices.Count;
        if (totalItems == 0)
        {
            TolkWrapper.Speak("No enemy items");
            return;
        }

        _enemyItemIndex = (_enemyItemIndex + 1) % totalItems;
        AnnounceCurrentEnemyItem();
    }

    /// <summary>
    /// Navega al item anterior del enemigo (Ctrl+Down en modo enemigo).
    /// </summary>
    public void EnemyPrevious()
    {
        if (!_enemyMode) return;

        int totalItems = _enemyItemIndices.Count + _enemySkillIndices.Count;
        if (totalItems == 0)
        {
            TolkWrapper.Speak("No enemy items");
            return;
        }

        _enemyItemIndex = (_enemyItemIndex - 1 + totalItems) % totalItems;
        AnnounceCurrentEnemyItem();
    }

    /// <summary>
    /// Anuncia el item actual del enemigo.
    /// </summary>
    private void AnnounceCurrentEnemyItem()
    {
        var bm = GetBoardManager();
        if (bm == null) return;

        int totalItems = _enemyItemIndices.Count + _enemySkillIndices.Count;
        int pos = _enemyItemIndex + 1;

        CardController controller = null;
        Card card = null;
        string prefix = "";

        // Primero items, luego skills
        if (_enemyItemIndex < _enemyItemIndices.Count)
        {
            int idx = _enemyItemIndices[_enemyItemIndex];
            controller = bm.opponentItemSockets[idx]?.CardController;
            card = controller?.CardData;
        }
        else
        {
            int skillIdx = _enemyItemIndex - _enemyItemIndices.Count;
            if (skillIdx < _enemySkillIndices.Count)
            {
                int idx = _enemySkillIndices[skillIdx];
                controller = bm.opponentSkillSockets[idx]?.CardController;
                card = controller?.CardData;
                prefix = "Skill: ";
            }
        }

        if (card == null)
        {
            TolkWrapper.Speak($"Empty, {pos} of {totalItems}");
            return;
        }

        string name = ItemReader.GetCardName(card);
        TolkWrapper.Speak($"{prefix}{name}, {pos} of {totalItems}");

        // Activar selección visual del item del enemigo
        if (controller != null)
        {
            ResetAllCardSelections(bm);
            // Simular evento de puntero para sonidos y tooltips
            var eventSystem = EventSystem.current;
            var pointerData = new PointerEventData(eventSystem)
            {
                position = UnityEngine.Input.mousePosition
            };
            controller.OnPointerEnter(pointerData);
            controller.HoverMove();
        }
    }

    /// <summary>
    /// Lee información detallada del item actual del enemigo.
    /// </summary>
    public void ReadCurrentEnemyItemDetails()
    {
        if (!_enemyMode) return;

        var bm = GetBoardManager();
        if (bm == null) return;

        Card card = null;

        if (_enemyItemIndex < _enemyItemIndices.Count)
        {
            int idx = _enemyItemIndices[_enemyItemIndex];
            card = bm.opponentItemSockets[idx]?.CardController?.CardData;
        }
        else
        {
            int skillIdx = _enemyItemIndex - _enemyItemIndices.Count;
            if (skillIdx < _enemySkillIndices.Count)
            {
                int idx = _enemySkillIndices[skillIdx];
                card = bm.opponentSkillSockets[idx]?.CardController?.CardData;
            }
        }

        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
    }

    /// <summary>
    /// Abre/cierra el stash y navega a él si está abierto.
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

            // Verificar si podemos interactuar
            if (!bm.AllowInteraction)
            {
                TolkWrapper.Speak("Cannot open stash now");
                return;
            }

            // Llamar al método del juego para abrir/cerrar el stash
            bm.TryToggleStorage();
            // El evento StorageToggled se disparará y OnStorageToggled se encargará del resto
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ToggleStash error: {ex.Message}");
            TolkWrapper.Speak("Cannot toggle stash");
        }
    }

    /// <summary>
    /// Va a la sección del stash (sin abrir/cerrar).
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

        // En combate solo permitir Hero
        if (_inCombat)
        {
            list.Add(NavigationSection.Hero);
            return list;
        }

        if (_selectionItems.Count > 0) list.Add(NavigationSection.Selection);
        if (_boardIndices.Count > 0) list.Add(NavigationSection.Board);
        // Solo incluir Stash si está abierto
        if (_stashOpen && _stashIndices.Count > 0) list.Add(NavigationSection.Stash);
        if (_playerSkills.Count > 0) list.Add(NavigationSection.Skills);
        list.Add(NavigationSection.Hero); // Hero siempre disponible
        return list;
    }

    public void Next()
    {
        // En Hero, no usar flechas normales - usar Ctrl+Up/Down
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;
        _currentIndex = (_currentIndex + 1) % count;
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void Previous()
    {
        // En Hero, no usar flechas normales - usar Ctrl+Up/Down
        if (_currentSection == NavigationSection.Hero) return;

        int count = GetCurrentSectionCount();
        if (count == 0) return;
        _currentIndex = (_currentIndex - 1 + count) % count;
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    // --- Anuncios ---

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
    /// Anuncia el estado actual de forma muy simple.
    /// Solo dice el nombre del estado, sin detalles extras.
    /// </summary>
    public void AnnounceState()
    {
        Refresh();

        var runState = GetCurrentState();

        // Verify state matches actual content - fix for incorrect "Shop" announcements
        runState = VerifyStateMatchesContent(runState);

        // Anuncio simplificado con información relevante
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
                // Para level up, incluir el nivel actual y número de skills disponibles
                int level = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Level) ?? 0;
                int skillCount = GetSelectionCardCount();
                announcement = skillCount > 0
                    ? $"Level up to {level}! Choose a skill, {skillCount} available"
                    : $"Level up to {level}!";
                break;
            case ERunState.Pedestal:
                announcement = "Upgrade";
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

        // Activar selección visual del primer item (si no estamos en combate)
        if (runState != ERunState.Combat && runState != ERunState.PVPCombat)
        {
            TriggerVisualSelection();
        }
    }

    private string GetSelectionTypeName()
    {
        // Contar solo las cartas (no Exit/Reroll)
        var cards = _selectionItems.Where(i => i.Type == NavItemType.Card).ToList();
        if (cards.Count == 0) return "options";

        // En estado de Loot, son recompensas
        var state = GetCurrentState();
        if (state == ERunState.Loot) return "rewards";

        var firstCard = cards[0].Card;
        if (IsEncounterCard(firstCard)) return "encounters";
        if (firstCard.Type == ECardType.Skill) return "skills";
        return "items";
    }

    /// <summary>
    /// Verifica si el estado actual sale automáticamente después de seleccionar.
    /// </summary>
    public bool WillAutoExit()
    {
        try
        {
            // Si no se puede salir manualmente, es porque auto-sale
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
            AnnounceHeroSubsection();
            return;
        }

        int count = GetCurrentSectionCount();

        // No anunciar secciones vacías
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

        // Si estamos en Hero, anunciar stat
        if (_currentSection == NavigationSection.Hero)
        {
            AnnounceHeroStat();
            return;
        }

        // Si estamos en Selection, puede ser carta o acción
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

            TolkWrapper.Speak($"{desc}, {pos} of {total}");
            return;
        }

        // Para otras secciones (Board, Stash, Skills)
        var card = GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        string cardDesc = GetCardDescription(card);
        TolkWrapper.Speak($"{cardDesc}, {pos} of {total}");
    }

    public void ReadDetailedInfo()
    {
        if (_currentSection == NavigationSection.Hero)
        {
            ReadAllHeroStats();
            return;
        }

        // Si estamos en Selection, puede ser NavItem
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

        // Para otras secciones
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

    // --- Acciones del juego ---

    /// <summary>
    /// Verifica si se puede salir del estado actual.
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
    /// Ejecuta el comando de salir del estado actual.
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
    /// Verifica si se puede hacer reroll.
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
    /// Obtiene el costo de reroll.
    /// </summary>
    public int GetRerollCost()
    {
        return (int)(Data.CurrentState?.RerollCost ?? 0);
    }

    /// <summary>
    /// Ejecuta el comando de reroll.
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
                // Refrescar después del reroll
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
        yield return new UnityEngine.WaitForSeconds(0.5f);
        Refresh();
        AnnounceState();
    }

    /// <summary>
    /// Anuncia las acciones disponibles (Exit, Reroll).
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

    private void AnnounceHeroStat()
    {
        var player = Data.Run?.Player;
        if (player == null) { TolkWrapper.Speak("No hero data"); return; }

        var type = HeroStats[_heroStatIndex];
        var value = player.GetAttributeValue(type);
        string name = GetStatName(type);

        TolkWrapper.Speak(value.HasValue ? $"{name}: {value.Value}" : $"{name}: none");
    }

    public void ReadAllHeroStats()
    {
        var player = Data.Run?.Player;
        if (player == null) { TolkWrapper.Speak("No hero data"); return; }

        var parts = new List<string>();

        var health = player.GetAttributeValue(EPlayerAttributeType.Health);
        var maxHealth = player.GetAttributeValue(EPlayerAttributeType.HealthMax);
        if (health.HasValue && maxHealth.HasValue)
            parts.Add($"Health {health.Value} of {maxHealth.Value}");

        var gold = player.GetAttributeValue(EPlayerAttributeType.Gold);
        if (gold.HasValue) parts.Add($"Gold {gold.Value}");

        var level = player.GetAttributeValue(EPlayerAttributeType.Level);
        if (level.HasValue) parts.Add($"Level {level.Value}");

        var shield = player.GetAttributeValue(EPlayerAttributeType.Shield);
        if (shield.HasValue && shield.Value > 0) parts.Add($"Shield {shield.Value}");

        TolkWrapper.Speak(string.Join(", ", parts));
    }

    private string GetStatName(EPlayerAttributeType type) => type switch
    {
        EPlayerAttributeType.Health => "Health",
        EPlayerAttributeType.HealthMax => "Max Health",
        EPlayerAttributeType.Gold => "Gold",
        EPlayerAttributeType.Level => "Level",
        EPlayerAttributeType.Experience => "Experience",
        EPlayerAttributeType.Prestige => "Prestige",
        EPlayerAttributeType.Shield => "Shield",
        EPlayerAttributeType.Poison => "Poison",
        EPlayerAttributeType.Burn => "Burn",
        EPlayerAttributeType.HealthRegen => "Regeneration",
        EPlayerAttributeType.CritChance => "Crit Chance",
        EPlayerAttributeType.Income => "Income",
        _ => type.ToString()
    };

    // --- Acceso a datos ---

    /// <summary>
    /// Obtiene el NavItem actual en la sección Selection.
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
                if (_currentIndex < _playerSkills.Count)
                {
                    return _playerSkills[_currentIndex];
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
    /// Obtiene el índice del slot actual en el tablero.
    /// </summary>
    public int GetCurrentBoardSlot()
    {
        if (_currentSection != NavigationSection.Board) return -1;
        if (_currentIndex < 0 || _currentIndex >= _boardIndices.Count) return -1;
        return _boardIndices[_currentIndex];
    }

    /// <summary>
    /// Ajusta el índice del navegador para apuntar al slot especificado del board.
    /// Usado después de reordenar para seguir al item movido.
    /// </summary>
    /// <param name="targetSlot">El slot al que queremos navegar</param>
    /// <returns>True si se encontró el slot</returns>
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
        return false;
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

    // --- Helpers ---

    private int GetCurrentSectionCount() => _currentSection switch
    {
        NavigationSection.Selection => _selectionItems.Count,
        NavigationSection.Board => _boardIndices.Count,
        NavigationSection.Stash => _stashIndices.Count,
        NavigationSection.Skills => _playerSkills.Count,
        NavigationSection.Hero => HeroStats.Length,
        _ => 0
    };

    private string GetCardDescription(Card card)
    {
        // En selección
        if (_currentSection == NavigationSection.Selection)
        {
            if (IsEncounterCard(card))
                return ItemReader.GetEncounterInfo(card);

            if (IsSelectionFree())
                return ItemReader.GetCardName(card);

            int price = ItemReader.GetBuyPrice(card);
            return price > 0 ? $"{ItemReader.GetCardName(card)}, {price} gold" : ItemReader.GetCardName(card);
        }

        // En tablero/stash - mostrar precio de venta
        string name = ItemReader.GetCardName(card);
        int sellPrice = ItemReader.GetSellPrice(card);
        return sellPrice > 0 ? $"{name}, sell {sellPrice}" : name;
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
    /// Obtiene el CardController de la carta actual y activa su efecto de hover visual.
    /// Esto hace que la carta se destaque visualmente como si el ratón estuviera sobre ella.
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
                    // Los items en selección están en los sockets del merchant
                    // Obtener el CardController del SelectionSet
                    var navItem = GetCurrentNavItem();
                    if (navItem?.Type == NavItemType.Card && navItem.Card != null)
                    {
                        controller = FindCardController(navItem.Card, bm);
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
                    if (_currentIndex < _playerSkills.Count)
                    {
                        // Skills del jugador están en playerSkillSockets
                        if (bm.playerSkillSockets != null && _currentIndex < bm.playerSkillSockets.Length)
                        {
                            controller = bm.playerSkillSockets[_currentIndex]?.CardController;
                        }
                    }
                    break;
            }

            if (controller != null)
            {
                // Primero resetear cualquier carta previamente seleccionada
                ResetAllCardSelections(bm);

                // Simular un evento de puntero para activar la selección completa
                // Esto dispara: animación de hover + sonidos + tooltips
                var eventSystem = EventSystem.current;
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = UnityEngine.Input.mousePosition
                };

                // Llamar a OnPointerEnter que activa Select() internamente
                // Esto incluye sonidos y tooltips
                controller.OnPointerEnter(pointerData);

                // Asegurarnos de que la animación de hover se ejecute
                controller.HoverMove();
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerVisualSelection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Busca el CardController de una carta usando el lookup del juego.
    /// Funciona para todos los tipos de cartas: items, skills, encounters.
    /// </summary>
    private CardController FindCardController(Card card, BoardManager bm)
    {
        if (card == null) return null;

        try
        {
            // Usar el lookup nativo del juego que funciona para todos los tipos de cartas
            var lookup = Data.CardAndSkillLookup;
            if (lookup != null)
            {
                var controller = lookup.GetCardController(card);
                if (controller != null) return controller;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"FindCardController lookup failed: {ex.Message}");
        }

        // Fallback: buscar manualmente en los sockets
        if (bm == null) return null;

        // Buscar en sockets del oponente (donde están los items a la venta)
        if (bm.opponentItemSockets != null)
        {
            foreach (var socket in bm.opponentItemSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        // Buscar en skill sockets del oponente (skills disponibles para elegir)
        if (bm.opponentSkillSockets != null)
        {
            foreach (var socket in bm.opponentSkillSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        // También buscar en sockets del jugador por si acaso
        if (bm.playerItemSockets != null)
        {
            foreach (var socket in bm.playerItemSockets)
            {
                if (socket?.CardController?.CardData == card)
                    return socket.CardController;
            }
        }

        return null;
    }

    /// <summary>
    /// Resetea el estado de hover de todas las cartas.
    /// </summary>
    private void ResetAllCardSelections(BoardManager bm)
    {
        try
        {
            // Resetear cartas del jugador
            if (bm.playerItemSockets != null)
            {
                foreach (var socket in bm.playerItemSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                }
            }

            // Resetear cartas del oponente
            if (bm.opponentItemSockets != null)
            {
                foreach (var socket in bm.opponentItemSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                }
            }

            // Resetear skills
            if (bm.playerSkillSockets != null)
            {
                foreach (var socket in bm.playerSkillSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                }
            }

            if (bm.opponentSkillSockets != null)
            {
                foreach (var socket in bm.opponentSkillSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                }
            }

            // Resetear stash
            if (bm.playerStorageSockets != null)
            {
                foreach (var socket in bm.playerStorageSockets)
                {
                    socket?.CardController?.ResetPosition(hideTooltips: true);
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"ResetAllCardSelections error: {ex.Message}");
        }
    }

    // --- Verificación de contenido ---

    /// <summary>
    /// Verifica si hay items en el tablero.
    /// </summary>
    public bool HasBoardContent() => _boardIndices.Count > 0;

    /// <summary>
    /// Verifica si hay items en la selección.
    /// </summary>
    public bool HasSelectionContent() => _selectionItems.Count > 0;

    /// <summary>
    /// Obtiene el número de items en el stash.
    /// </summary>
    public int GetStashItemCount() => _stashIndices.Count;

    /// <summary>
    /// Indica si el stash está abierto.
    /// </summary>
    public bool IsStashOpen() => _stashOpen;

    /// <summary>
    /// Obtiene el número de cartas (no acciones) en la selección.
    /// </summary>
    public int GetSelectionCardCount() =>
        _selectionItems.Count(i => i.Type == NavItemType.Card);

    // --- Navegación detallada línea por línea ---

    /// <summary>
    /// Inicializa las líneas de detalle para el item actual.
    /// </summary>
    private void InitDetailLines()
    {
        var card = GetCurrentCard();

        // Si es el mismo item, no reinicializar
        if (card == _detailCard && _detailLines.Count > 0) return;

        _detailCard = card;
        _detailLines.Clear();
        _detailIndex = -1;

        if (card == null)
        {
            // Para Exit/Reroll
            var navItem = GetCurrentNavItem();
            if (navItem != null)
            {
                switch (navItem.Type)
                {
                    case NavItemType.Exit:
                        _detailLines.Add("Exit");
                        _detailLines.Add("Leave the current state and continue");
                        break;
                    case NavItemType.Reroll:
                        int gold = Data.Run?.Player?.GetAttributeValue(EPlayerAttributeType.Gold) ?? 0;
                        _detailLines.Add($"Refresh: {navItem.RerollCost} gold");
                        _detailLines.Add($"Your gold: {gold}");
                        break;
                }
            }
            return;
        }

        // Obtener líneas de detalle del item
        _detailLines = ItemReader.GetDetailLines(card);
    }

    /// <summary>
    /// Lee la línea de detalle anterior (Ctrl+Up).
    /// </summary>
    public void ReadDetailPrevious()
    {
        InitDetailLines();

        if (_detailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        if (_detailIndex <= 0)
        {
            _detailIndex = 0;
            TolkWrapper.Speak($"{_detailLines[0]}, 1 of {_detailLines.Count}");
        }
        else
        {
            _detailIndex--;
            TolkWrapper.Speak($"{_detailLines[_detailIndex]}, {_detailIndex + 1} of {_detailLines.Count}");
        }
    }

    /// <summary>
    /// Lee la siguiente línea de detalle (Ctrl+Down).
    /// </summary>
    public void ReadDetailNext()
    {
        InitDetailLines();

        if (_detailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        if (_detailIndex < 0)
        {
            _detailIndex = 0;
        }
        else if (_detailIndex < _detailLines.Count - 1)
        {
            _detailIndex++;
        }

        TolkWrapper.Speak($"{_detailLines[_detailIndex]}, {_detailIndex + 1} of {_detailLines.Count}");
    }

    /// <summary>
    /// Limpia el cache de detalles (llamar cuando cambia el item seleccionado).
    /// </summary>
    public void ClearDetailCache()
    {
        _detailCard = null;
        _detailLines.Clear();
        _detailIndex = -1;
    }
}
