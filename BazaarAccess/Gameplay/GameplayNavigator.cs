using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;
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
/// Subsecciones dentro del modo enemigo.
/// </summary>
public enum EnemySubsection
{
    Items,      // Items del enemigo
    Skills      // Skills del enemigo
}

/// <summary>
/// Secciones en modo recap (post-combate con E).
/// </summary>
public enum RecapSection
{
    None,           // No estamos en ninguna sección de recap
    HeroStats,      // Stats del héroe propio (V)
    HeroSkills,     // Skills del héroe propio (V + Right)
    EnemyStats,     // Stats del héroe enemigo (F)
    EnemySkills,    // Skills del héroe enemigo (F + Right)
    EnemyBoard,     // Tablero del enemigo (G)
    PlayerBoard,    // Tablero propio (B)
    CombatStats     // Per-card combat stats (H)
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
    private bool _inRecapMode = false;

    // Navegación detallada línea por línea
    private List<string> _detailLines = new List<string>();
    private int _detailIndex = -1;
    private Card _detailCard = null;

    // Modo enemigo (para navegar items del oponente)
    private bool _enemyMode = false;
    private List<int> _enemyItemIndices = new List<int>();
    private List<int> _enemySkillIndices = new List<int>();
    private List<SkillCard> _enemySkills = new List<SkillCard>(); // Alternative source when sockets unavailable
    private int _enemyItemIndex = 0;
    private int _enemySkillIndex = 0;
    private EnemySubsection _enemySubsection = EnemySubsection.Items;

    // Detail reading for enemy items
    private List<string> _enemyDetailLines = new List<string>();
    private int _enemyDetailIndex = -1;
    private Card _enemyDetailCard = null;

    // Recap mode navigation
    private RecapSection _currentRecapSection = RecapSection.None;
    private int _enemyStatIndex = 0;  // For navigating enemy hero stats
    private int _enemyHeroSkillIndex = 0;  // For navigating enemy hero skills (separate from enemy board skills)

    // Combat stats recap navigation
    private List<string> _combatStatsLines = new List<string>();
    private int _combatStatsIndex = 0;

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
    public EnemySubsection CurrentEnemySubsection => _enemySubsection;
    public RecapSection CurrentRecapSection => _currentRecapSection;

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

        // Clear detail cache to prevent stale card references
        // This is important after items are moved, sold, or upgraded
        ClearDetailCache();

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
        ClearDetailCache();
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
        ClearDetailCache();
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
            int count = GetHeroStatCount();
            string msg = $"Hero stats, {count} stats";
            // Include rank in announcement when in ranked mode
            string rank = ItemReader.GetPlayerRank();
            if (!string.IsNullOrEmpty(rank) && ItemReader.IsRankedMode())
                msg += $". Rank: {rank}";
            TolkWrapper.Speak(msg);
        }
        else
        {
            TolkWrapper.Speak($"Hero skills, {_playerSkills.Count} skills");
        }
    }

    /// <summary>
    /// Returns the total number of hero stats including rank if in ranked mode.
    /// </summary>
    private int GetHeroStatCount()
    {
        int count = HeroStats.Length;
        if (ItemReader.IsRankedMode()) count++; // Extra slot for rank
        return count;
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
            TolkWrapper.Speak($"{name}: {desc}");
        }
        else
        {
            TolkWrapper.Speak(name);
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
                        position = Vector2.zero
                    };
                    controller.OnPointerEnter(pointerData);
                    controller.HoverMove();
                    TriggerHoverSound(controller);
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
            int maxIndex = GetHeroStatCount() - 1;
            // No wrap - stay at end
            if (_heroStatIndex >= maxIndex)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            _heroStatIndex++;
            AnnounceHeroStat();
        }
        else
        {
            if (_playerSkills.Count == 0) return;
            // No wrap - stay at end
            if (_heroSkillIndex >= _playerSkills.Count - 1)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            _heroSkillIndex++;
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
            // No wrap - stay at start
            if (_heroStatIndex <= 0)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            _heroStatIndex--;
            AnnounceHeroStat();
        }
        else
        {
            if (_playerSkills.Count == 0) return;
            // No wrap - stay at start
            if (_heroSkillIndex <= 0)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            _heroSkillIndex--;
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
    /// Indica si estamos en modo recap (después de pulsar E en ReplayState).
    /// </summary>
    public bool IsInRecapMode => _inRecapMode;

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
        else
        {
            // Al salir de replay, también salir de recap
            _inRecapMode = false;
        }
    }

    /// <summary>
    /// Activa el modo recap (después de pulsar E en ReplayState).
    /// </summary>
    public void SetRecapMode(bool inRecapMode)
    {
        _inRecapMode = inRecapMode;
        if (!inRecapMode)
        {
            _currentRecapSection = RecapSection.None;
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

            // Nombre del oponente: solo usar SimPvpOpponent si estamos realmente en PvP combat
            var currentState = Data.CurrentState?.StateName;
            bool isPvpCombat = currentState == ERunState.PVPCombat;

            if (isPvpCombat)
            {
                var pvpOpponent = Data.SimPvpOpponent;
                if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
                {
                    parts.Add($"Opponent: {pvpOpponent.Name}");
                }
                else
                {
                    parts.Add("Enemy");
                }
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
        _enemySkills.Clear();
        _enemyItemIndex = 0;
        _enemySkillIndex = 0;

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

        // Skills del enemigo - primero intentar desde los sockets
        if (bm.opponentSkillSockets != null)
        {
            for (int i = 0; i < bm.opponentSkillSockets.Length; i++)
            {
                if (bm.opponentSkillSockets[i]?.CardController?.CardData != null)
                    _enemySkillIndices.Add(i);
            }
        }

        // Siempre intentar cargar skills desde Data.Run.Opponent.Skills como respaldo
        try
        {
            var opponent = Data.Run?.Opponent;
            if (opponent?.Skills != null)
            {
                _enemySkills.AddRange(opponent.Skills);
                Plugin.Logger.LogInfo($"RefreshEnemyItems: Got {_enemySkills.Count} skills from Opponent.Skills");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"RefreshEnemyItems: Failed to get Opponent.Skills: {ex.Message}");
        }

        Plugin.Logger.LogInfo($"RefreshEnemyItems: {_enemyItemIndices.Count} items, {_enemySkillIndices.Count} socket skills, {_enemySkills.Count} data skills");
    }

    /// <summary>
    /// Sale del modo enemigo.
    /// </summary>
    public void ExitEnemyMode()
    {
        _enemyMode = false;
        _enemyItemIndex = 0;
        _enemySkillIndex = 0;
        _enemySubsection = EnemySubsection.Items;
        ClearEnemyDetailReading();
    }

    /// <summary>
    /// Clears the detail reading state for enemy items.
    /// </summary>
    private void ClearEnemyDetailReading()
    {
        _enemyDetailLines.Clear();
        _enemyDetailIndex = -1;
        _enemyDetailCard = null;
    }

    /// <summary>
    /// Entra directamente al modo de navegación del tablero del oponente.
    /// Usado durante ReplayMode con la tecla G.
    /// Best used during Recap (E) for static view without Combat Describer.
    /// </summary>
    public void EnterOpponentBoardMode()
    {
        try
        {
            // Check if Replay is actively running (combat animation playing)
            var bm = GetBoardManager();
            bool isReplayRunning = IsReplayAnimationActive();

            if (isReplayRunning)
            {
                TolkWrapper.Speak("Replay in progress. Press E for Recap to view opponent board without interruptions.");
                return;
            }

            RefreshEnemyItems();

            int skillCount = GetEnemySkillCount();
            if (_enemyItemIndices.Count == 0 && skillCount == 0)
            {
                TolkWrapper.Speak("No opponent items");
                return;
            }

            _enemyMode = true;
            _enemyItemIndex = 0;
            _enemySkillIndex = 0;
            _enemySubsection = EnemySubsection.Items;
            ClearEnemyDetailReading();

            // Set recap section if in recap mode
            if (_inRecapMode)
            {
                _currentRecapSection = RecapSection.EnemyBoard;
            }

            // Get opponent name if available
            string opponentName = "Opponent";
            var pvpOpponent = Data.SimPvpOpponent;
            if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
            {
                opponentName = pvpOpponent.Name;
            }

            // Announce board
            TolkWrapper.Speak($"{opponentName}'s board, {_enemyItemIndices.Count} items");
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"EnterOpponentBoardMode error: {ex.Message}");
            TolkWrapper.Speak("Cannot access opponent board");
        }
    }

    /// <summary>
    /// Checks if the replay animation is currently playing.
    /// </summary>
    private bool IsReplayAnimationActive()
    {
        try
        {
            var currentState = AppState.CurrentState;
            if (currentState == null) return false;

            var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
            if (replayStateType == null || !replayStateType.IsInstanceOfType(currentState))
                return false;

            // Check IsReplaying property
            var isReplayingProp = replayStateType.GetProperty("IsReplaying");
            if (isReplayingProp != null)
            {
                return (bool)isReplayingProp.GetValue(currentState);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"IsReplayAnimationActive error: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Navigate to next item in current enemy subsection (Right arrow).
    /// </summary>
    public void EnemyNavigateRight()
    {
        if (!_enemyMode) return;
        ClearEnemyDetailReading();

        if (_enemySubsection == EnemySubsection.Items)
        {
            if (_enemyItemIndices.Count == 0)
            {
                TolkWrapper.Speak("No items");
                return;
            }
            if (_enemyItemIndex >= _enemyItemIndices.Count - 1)
            {
                AnnounceCurrentEnemyItem(); // Read current at limit
                return;
            }
            _enemyItemIndex++;
        }
        else // Skills
        {
            int skillCount = GetEnemySkillCount();
            if (skillCount == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            if (_enemySkillIndex >= skillCount - 1)
            {
                AnnounceCurrentEnemyItem();
                return;
            }
            _enemySkillIndex++;
        }
        AnnounceCurrentEnemyItem();
    }

    /// <summary>
    /// Navigate to previous item in current enemy subsection (Left arrow).
    /// </summary>
    public void EnemyNavigateLeft()
    {
        if (!_enemyMode) return;
        ClearEnemyDetailReading();

        if (_enemySubsection == EnemySubsection.Items)
        {
            if (_enemyItemIndices.Count == 0)
            {
                TolkWrapper.Speak("No items");
                return;
            }
            if (_enemyItemIndex <= 0)
            {
                AnnounceCurrentEnemyItem();
                return;
            }
            _enemyItemIndex--;
        }
        else // Skills
        {
            int skillCount = GetEnemySkillCount();
            if (skillCount == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            if (_enemySkillIndex <= 0)
            {
                AnnounceCurrentEnemyItem();
                return;
            }
            _enemySkillIndex--;
        }
        AnnounceCurrentEnemyItem();
    }

    /// <summary>
    /// Switch to next enemy subsection (Ctrl+Right: Items -> Skills).
    /// </summary>
    public void EnemyNextSubsection()
    {
        if (!_enemyMode) return;
        ClearEnemyDetailReading();

        int skillCount = GetEnemySkillCount();
        if (_enemySubsection == EnemySubsection.Items && skillCount > 0)
        {
            _enemySubsection = EnemySubsection.Skills;
            _enemySkillIndex = 0;
            // Announce first skill with description (like hero skills)
            AnnounceEnemySkill();
        }
        else if (_enemySubsection == EnemySubsection.Items)
        {
            TolkWrapper.Speak("No skills");
        }
        else
        {
            // Already in Skills, re-announce current skill
            AnnounceEnemySkill();
        }
    }

    /// <summary>
    /// Switch to previous enemy subsection (Ctrl+Left: Skills -> Items).
    /// </summary>
    public void EnemyPreviousSubsection()
    {
        if (!_enemyMode) return;
        ClearEnemyDetailReading();

        if (_enemySubsection == EnemySubsection.Skills && _enemyItemIndices.Count > 0)
        {
            _enemySubsection = EnemySubsection.Items;
            _enemyItemIndex = 0;
            TolkWrapper.Speak($"Items, {_enemyItemIndices.Count}");
        }
        else
        {
            TolkWrapper.Speak(_enemySubsection == EnemySubsection.Items ? "Items" : "Skills");
        }
    }

    /// <summary>
    /// Navigate to next enemy skill (Ctrl+Up in Skills subsection).
    /// Similar to HeroNext() for hero skills.
    /// </summary>
    public void EnemySkillNext()
    {
        if (!_enemyMode || _enemySubsection != EnemySubsection.Skills) return;

        int skillCount = GetEnemySkillCount();
        if (skillCount == 0) return;

        if (_enemySkillIndex >= skillCount - 1)
        {
            // Already at end, just re-announce current skill
            AnnounceEnemySkill();
            return;
        }

        _enemySkillIndex++;
        AnnounceEnemySkill();
    }

    /// <summary>
    /// Navigate to previous enemy skill (Ctrl+Down in Skills subsection).
    /// Similar to HeroPrevious() for hero skills.
    /// </summary>
    public void EnemySkillPrevious()
    {
        if (!_enemyMode || _enemySubsection != EnemySubsection.Skills) return;

        int skillCount = GetEnemySkillCount();
        if (skillCount == 0) return;

        if (_enemySkillIndex <= 0)
        {
            // Already at start, just re-announce current skill
            AnnounceEnemySkill();
            return;
        }

        _enemySkillIndex--;
        AnnounceEnemySkill();
    }

    /// <summary>
    /// Announces the current enemy skill with name and description.
    /// Similar to AnnounceHeroSkill().
    /// </summary>
    private void AnnounceEnemySkill()
    {
        int skillCount = GetEnemySkillCount();
        if (_enemySkillIndex < 0 || _enemySkillIndex >= skillCount)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        var card = GetCurrentEnemyCard();
        if (card == null)
        {
            TolkWrapper.Speak("Empty slot");
            return;
        }

        string name = ItemReader.GetCardName(card);
        string desc = ItemReader.GetFullDescription(card);

        if (!string.IsNullOrEmpty(desc))
        {
            TolkWrapper.Speak($"{name}: {desc}");
        }
        else
        {
            TolkWrapper.Speak(name);
        }
    }

    /// <summary>
    /// Returns true if currently in enemy Skills subsection.
    /// </summary>
    public bool IsInEnemySkillsSubsection => _enemyMode && _enemySubsection == EnemySubsection.Skills;

    /// <summary>
    /// Read next detail line of current enemy item (Ctrl+Up).
    /// </summary>
    public void EnemyDetailNext()
    {
        if (!_enemyMode) return;

        var card = GetCurrentEnemyCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        // Refresh detail lines if card changed - use enemy-focused order
        if (_enemyDetailCard != card)
        {
            _enemyDetailLines = ItemReader.GetEnemyDetailLines(card);
            _enemyDetailIndex = -1;
            _enemyDetailCard = card;
        }

        if (_enemyDetailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        // Advance to next line (no wrap)
        if (_enemyDetailIndex >= _enemyDetailLines.Count - 1)
        {
            _enemyDetailIndex = _enemyDetailLines.Count - 1;
            TolkWrapper.Speak(_enemyDetailLines[_enemyDetailIndex]);
            return;
        }

        _enemyDetailIndex++;
        TolkWrapper.Speak(_enemyDetailLines[_enemyDetailIndex]);
    }

    /// <summary>
    /// Read previous detail line of current enemy item (Ctrl+Down).
    /// </summary>
    public void EnemyDetailPrevious()
    {
        if (!_enemyMode) return;

        var card = GetCurrentEnemyCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        // Refresh detail lines if card changed - use enemy-focused order
        if (_enemyDetailCard != card)
        {
            _enemyDetailLines = ItemReader.GetEnemyDetailLines(card);
            _enemyDetailIndex = -1;
            _enemyDetailCard = card;
        }

        if (_enemyDetailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        // On first press or at start, read first line
        if (_enemyDetailIndex <= 0)
        {
            _enemyDetailIndex = 0;
            TolkWrapper.Speak(_enemyDetailLines[0]);
            return;
        }

        _enemyDetailIndex--;
        TolkWrapper.Speak(_enemyDetailLines[_enemyDetailIndex]);
    }

    /// <summary>
    /// Gets the total count of enemy skills from either sockets or data.
    /// </summary>
    private int GetEnemySkillCount()
    {
        // Return the max of both sources since we fall back between them
        return System.Math.Max(_enemySkillIndices.Count, _enemySkills.Count);
    }

    /// <summary>
    /// Gets the current enemy card based on subsection and index.
    /// </summary>
    private Card GetCurrentEnemyCard()
    {
        var bm = GetBoardManager();

        if (_enemySubsection == EnemySubsection.Items)
        {
            if (bm == null) return null;
            if (_enemyItemIndex < _enemyItemIndices.Count)
            {
                int idx = _enemyItemIndices[_enemyItemIndex];
                return bm.opponentItemSockets[idx]?.CardController?.CardData;
            }
        }
        else // Skills
        {
            // Try from sockets first
            if (_enemySkillIndices.Count > 0 && bm != null)
            {
                if (_enemySkillIndex < _enemySkillIndices.Count)
                {
                    int idx = _enemySkillIndices[_enemySkillIndex];
                    var card = bm.opponentSkillSockets[idx]?.CardController?.CardData;
                    if (card != null) return card;
                }
            }
            // Fallback to data
            if (_enemySkillIndex < _enemySkills.Count)
            {
                return _enemySkills[_enemySkillIndex];
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the current enemy card controller based on subsection and index.
    /// </summary>
    private CardController GetCurrentEnemyCardController()
    {
        var bm = GetBoardManager();
        if (bm == null) return null;

        if (_enemySubsection == EnemySubsection.Items)
        {
            if (_enemyItemIndex < _enemyItemIndices.Count)
            {
                int idx = _enemyItemIndices[_enemyItemIndex];
                return bm.opponentItemSockets[idx]?.CardController;
            }
        }
        else // Skills
        {
            if (_enemySkillIndex < _enemySkillIndices.Count)
            {
                int idx = _enemySkillIndices[_enemySkillIndex];
                return bm.opponentSkillSockets[idx]?.CardController;
            }
        }
        return null;
    }

    /// <summary>
    /// Announces the current enemy item with position.
    /// For skills, includes description like player's own skills.
    /// </summary>
    private void AnnounceCurrentEnemyItem()
    {
        var bm = GetBoardManager();
        CardController controller = GetCurrentEnemyCardController();
        Card card = controller?.CardData ?? GetCurrentEnemyCard(); // Fallback to data if no controller

        int total, pos;
        if (_enemySubsection == EnemySubsection.Items)
        {
            total = _enemyItemIndices.Count;
            pos = _enemyItemIndex + 1;
        }
        else
        {
            total = GetEnemySkillCount();
            pos = _enemySkillIndex + 1;
        }

        if (card == null)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        string name = ItemReader.GetCardName(card);

        // For skills, include description like player's own skills
        if (_enemySubsection == EnemySubsection.Skills)
        {
            string desc = ItemReader.GetFullDescription(card);
            if (!string.IsNullOrEmpty(desc))
            {
                TolkWrapper.Speak($"{name}: {desc}");
            }
            else
            {
                TolkWrapper.Speak(name);
            }
        }
        else
        {
            // Use compact combat-focused description for enemy items
            TolkWrapper.Speak(ItemReader.GetEnemyCompactDescription(card));
        }

        // Visual feedback (only if controller is available)
        if (controller != null && bm != null)
        {
            ResetAllCardSelections(bm);
            var eventSystem = EventSystem.current;
            var pointerData = new PointerEventData(eventSystem) { position = Vector2.zero };
            controller.OnPointerEnter(pointerData);
            controller.HoverMove();
            TriggerHoverSound(controller);
        }
    }

    /// <summary>
    /// Reads detailed description of current enemy item (Enter key).
    /// </summary>
    public void ReadCurrentEnemyItemDetails()
    {
        if (!_enemyMode) return;

        var card = GetCurrentEnemyCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
    }

    // Keep old methods for backward compatibility during combat (F key)
    public void EnemyNext() => EnemyNavigateRight();
    public void EnemyPrevious() => EnemyNavigateLeft();

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
        ClearDetailCache();
        AnnounceCurrentItem();
        TriggerVisualSelection();
    }

    public void Previous()
    {
        // En Hero, no usar flechas normales - usar Ctrl+Up/Down
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
        ClearDetailCache();
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
        ClearDetailCache();
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
        ClearDetailCache();
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
        ClearDetailCache();
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

            TolkWrapper.Speak(desc);
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
        TolkWrapper.Speak(cardDesc);
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
        // Check if this is the rank slot (last slot in ranked mode)
        if (ItemReader.IsRankedMode() && _heroStatIndex >= HeroStats.Length)
        {
            string rank = ItemReader.GetPlayerRank();
            TolkWrapper.Speak(!string.IsNullOrEmpty(rank) ? $"Rank: {rank}" : "Rank: unranked");
            return;
        }

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

    // --- Helpers ---

    private int GetCurrentSectionCount() => _currentSection switch
    {
        NavigationSection.Selection => _selectionItems.Count,
        NavigationSection.Board => _boardIndices.Count,
        NavigationSection.Stash => _stashIndices.Count,
        NavigationSection.Skills => _playerSkills.Count,
        NavigationSection.Hero => GetHeroStatCount(),
        _ => 0
    };

    private string GetCardDescription(Card card)
    {
        // En selección (shop/rewards)
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

        // En tablero/stash/skills
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
                var eventSystem = EventSystem.current;
                var pointerData = new PointerEventData(eventSystem)
                {
                    position = Vector2.zero
                };

                // Llamar a OnPointerEnter que activa Select() internamente
                controller.OnPointerEnter(pointerData);

                // Asegurarnos de que la animación de hover se ejecute
                controller.HoverMove();

                // Reproducir sonido de hover según el tipo de controller
                TriggerHoverSound(controller);
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerVisualSelection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reproduce el sonido de hover para tipos de controller que no lo hacen en HoverMove().
    /// Nota: EncounterController ya reproduce sonido en HoverMove(), solo necesitamos esto para ItemController.
    /// </summary>
    private void TriggerHoverSound(CardController controller)
    {
        try
        {
            // EncounterController ya reproduce sonido en su override de HoverMove()
            // Solo necesitamos manejar ItemController que no sobreescribe HoverMove()
            if (controller is ItemController itemController)
            {
                Vector3 position = controller.transform.position;
                var handlerField = typeof(ItemController).GetField("soundCardHandler",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (handlerField != null)
                {
                    var handler = handlerField.GetValue(itemController);
                    if (handler != null)
                    {
                        var method = handler.GetType().GetMethod("SoundCardRaise",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        method?.Invoke(handler, new object[] { position });
                    }
                }
            }
            // SkillController tampoco sobreescribe HoverMove() con sonido
            else if (controller is SkillController skillController)
            {
                // Skills no tienen sonido de hover específico en el juego vanilla
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogWarning($"TriggerHoverSound error: {ex.Message}");
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
        _detailLines.Reverse();
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

    /// <summary>
    /// Read next detail line (Down key). Starts at first line.
    /// </summary>
    public void ReadDetailLineDown()
    {
        InitDetailLines();

        if (_detailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        // First press: start at index 0
        if (_detailIndex < 0)
        {
            _detailIndex = 0;
            TolkWrapper.Speak(_detailLines[_detailIndex]);
            return;
        }

        // Already at last line
        if (_detailIndex >= _detailLines.Count - 1)
        {
            TolkWrapper.Speak($"Last line. {_detailLines[_detailIndex]}");
            return;
        }

        // Move to next line
        _detailIndex++;
        TolkWrapper.Speak(_detailLines[_detailIndex]);
    }

    /// <summary>
    /// Read previous detail line (Up key).
    /// </summary>
    public void ReadDetailLineUp()
    {
        InitDetailLines();

        if (_detailLines.Count == 0)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        // If not started, start at first line
        if (_detailIndex < 0)
        {
            _detailIndex = 0;
            TolkWrapper.Speak(_detailLines[_detailIndex]);
            return;
        }

        // Already at first line
        if (_detailIndex <= 0)
        {
            TolkWrapper.Speak($"First line. {_detailLines[_detailIndex]}");
            return;
        }

        // Move to previous line
        _detailIndex--;
        TolkWrapper.Speak(_detailLines[_detailIndex]);
    }

    // ===============================================
    // RECAP MODE NAVIGATION (post-combat with E key)
    // ===============================================

    /// <summary>
    /// Enter Hero stats mode in recap (V key).
    /// </summary>
    public void EnterRecapHeroMode()
    {
        _currentRecapSection = RecapSection.HeroStats;
        _heroStatIndex = 0;
        _currentSection = NavigationSection.Hero;
        _heroSubsection = HeroSubsection.Stats;

        int statCount = GetHeroStatCount();
        int skillCount = _playerSkills.Count;

        string msg = $"Your hero. Stats: {statCount}";
        if (skillCount > 0)
            msg += $", Skills: {skillCount}. Right arrow for skills.";

        // Include rank in recap hero announcement
        string rank = ItemReader.GetPlayerRank();
        if (!string.IsNullOrEmpty(rank) && ItemReader.IsRankedMode())
            msg += $" Rank: {rank}";

        TolkWrapper.Speak(msg);
    }

    /// <summary>
    /// Navigate to previous hero stat/skill in recap (Up key).
    /// </summary>
    public void RecapHeroPrevious()
    {
        if (_currentRecapSection == RecapSection.HeroStats)
        {
            if (_heroStatIndex <= 0)
            {
                _heroStatIndex = 0;
                AnnounceHeroStat();
                return;
            }
            _heroStatIndex--;
            AnnounceHeroStat();
        }
        else if (_currentRecapSection == RecapSection.HeroSkills)
        {
            if (_heroSkillIndex <= 0)
            {
                _heroSkillIndex = 0;
                AnnounceHeroSkill();
                return;
            }
            _heroSkillIndex--;
            AnnounceHeroSkill();
        }
    }

    /// <summary>
    /// Navigate to next hero stat/skill in recap (Down key).
    /// </summary>
    public void RecapHeroNext()
    {
        if (_currentRecapSection == RecapSection.HeroStats)
        {
            int maxIndex = GetHeroStatCount() - 1;
            if (_heroStatIndex >= maxIndex)
            {
                AnnounceHeroStat();
                return;
            }
            _heroStatIndex++;
            AnnounceHeroStat();
        }
        else if (_currentRecapSection == RecapSection.HeroSkills)
        {
            if (_heroSkillIndex >= _playerSkills.Count - 1)
            {
                AnnounceHeroSkill();
                return;
            }
            _heroSkillIndex++;
            AnnounceHeroSkill();
        }
    }

    /// <summary>
    /// Switch to hero stats in recap (Left key).
    /// </summary>
    public void RecapHeroToStats()
    {
        if (_currentRecapSection == RecapSection.HeroSkills)
        {
            _currentRecapSection = RecapSection.HeroStats;
            _heroStatIndex = 0;
            TolkWrapper.Speak($"Stats, {GetHeroStatCount()}");
        }
    }

    /// <summary>
    /// Switch to hero skills in recap (Right key).
    /// </summary>
    public void RecapHeroToSkills()
    {
        if (_currentRecapSection == RecapSection.HeroStats)
        {
            if (_playerSkills.Count == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            _currentRecapSection = RecapSection.HeroSkills;
            _heroSkillIndex = 0;
            TolkWrapper.Speak($"Skills, {_playerSkills.Count}");
        }
    }

    /// <summary>
    /// Enter Enemy stats mode in recap (F key).
    /// </summary>
    public void EnterRecapEnemyStatsMode()
    {
        _currentRecapSection = RecapSection.EnemyStats;
        _enemyStatIndex = 0;
        _enemyHeroSkillIndex = 0;

        RefreshEnemyItems(); // Also loads enemy skills

        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy");
            _currentRecapSection = RecapSection.None;
            return;
        }

        TolkWrapper.Speak("Enemy stats");
    }

    /// <summary>
    /// Enter Enemy stats mode during combat (F key).
    /// Allows navigation of enemy stats and skills with arrow keys.
    /// </summary>
    public void EnterCombatEnemyStatsMode()
    {
        _currentRecapSection = RecapSection.EnemyStats;
        _enemyStatIndex = 0;
        _enemyHeroSkillIndex = 0;

        RefreshEnemyItems(); // Also loads enemy skills

        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy");
            _currentRecapSection = RecapSection.None;
            return;
        }

        TolkWrapper.Speak("Enemy stats");
    }

    /// <summary>
    /// Navigate to previous enemy stat/skill in recap (Up key).
    /// </summary>
    public void RecapEnemyStatsPrevious()
    {
        if (_currentRecapSection == RecapSection.EnemyStats)
        {
            if (_enemyStatIndex <= 0)
            {
                _enemyStatIndex = 0;
                AnnounceEnemyStat();
                return;
            }
            _enemyStatIndex--;
            AnnounceEnemyStat();
        }
        else if (_currentRecapSection == RecapSection.EnemySkills)
        {
            if (_enemyHeroSkillIndex <= 0)
            {
                _enemyHeroSkillIndex = 0;
                AnnounceEnemyHeroSkill();
                return;
            }
            _enemyHeroSkillIndex--;
            AnnounceEnemyHeroSkill();
        }
    }

    /// <summary>
    /// Navigate to next enemy stat/skill in recap (Down key).
    /// </summary>
    public void RecapEnemyStatsNext()
    {
        if (_currentRecapSection == RecapSection.EnemyStats)
        {
            if (_enemyStatIndex >= HeroStats.Length - 1)
            {
                AnnounceEnemyStat();
                return;
            }
            _enemyStatIndex++;
            AnnounceEnemyStat();
        }
        else if (_currentRecapSection == RecapSection.EnemySkills)
        {
            if (_enemyHeroSkillIndex >= _enemySkills.Count - 1)
            {
                AnnounceEnemyHeroSkill();
                return;
            }
            _enemyHeroSkillIndex++;
            AnnounceEnemyHeroSkill();
        }
    }

    /// <summary>
    /// Switch to enemy stats in recap (Left key).
    /// </summary>
    public void RecapEnemyToStats()
    {
        if (_currentRecapSection == RecapSection.EnemySkills)
        {
            _currentRecapSection = RecapSection.EnemyStats;
            _enemyStatIndex = 0;
            TolkWrapper.Speak("Stats");
        }
    }

    /// <summary>
    /// Switch to enemy skills in recap (Right key).
    /// </summary>
    public void RecapEnemyToSkills()
    {
        if (_currentRecapSection == RecapSection.EnemyStats)
        {
            if (_enemySkills.Count == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            _currentRecapSection = RecapSection.EnemySkills;
            _enemyHeroSkillIndex = 0;
            TolkWrapper.Speak($"Skills, {_enemySkills.Count}");
        }
    }

    /// <summary>
    /// Announce current enemy hero stat.
    /// </summary>
    private void AnnounceEnemyStat()
    {
        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy data");
            return;
        }

        var type = HeroStats[_enemyStatIndex];
        string name = GetStatName(type);

        if (opponent.Attributes.TryGetValue(type, out int value))
        {
            TolkWrapper.Speak($"{name}: {value}");
        }
        else
        {
            TolkWrapper.Speak($"{name}: none");
        }
    }

    /// <summary>
    /// Announce current enemy hero skill.
    /// </summary>
    private void AnnounceEnemyHeroSkill()
    {
        if (_enemyHeroSkillIndex < 0 || _enemyHeroSkillIndex >= _enemySkills.Count)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        var skill = _enemySkills[_enemyHeroSkillIndex];
        if (skill == null)
        {
            TolkWrapper.Speak("Empty slot");
            return;
        }

        string name = ItemReader.GetCardName(skill);
        string desc = ItemReader.GetFullDescription(skill);

        if (!string.IsNullOrEmpty(desc))
        {
            TolkWrapper.Speak($"{name}: {desc}");
        }
        else
        {
            TolkWrapper.Speak(name);
        }
    }

    /// <summary>
    /// Enter player board mode in recap (B key).
    /// </summary>
    public void EnterRecapPlayerBoardMode()
    {
        _currentRecapSection = RecapSection.PlayerBoard;
        _currentSection = NavigationSection.Board;
        _currentIndex = 0;
        ClearDetailCache();

        RefreshBoard();

        if (_boardIndices.Count == 0)
        {
            TolkWrapper.Speak("Your board is empty");
            return;
        }

        TolkWrapper.Speak($"Your board, {_boardIndices.Count} items");
    }

    // COMBAT STATS RECAP NAVIGATION (H key in recap)
    // ================================================

    /// <summary>
    /// Enter combat stats mode in recap (H key).
    /// </summary>
    public void EnterRecapCombatStatsMode()
    {
        _combatStatsLines = CombatDescriber.GetCombatStatsLines();
        _combatStatsIndex = 0;
        _currentRecapSection = RecapSection.CombatStats;

        if (_combatStatsLines.Count > 0)
        {
            TolkWrapper.Speak(_combatStatsLines[0]);
        }
        else
        {
            TolkWrapper.Speak("No combat data");
        }
    }

    /// <summary>
    /// Navigate to previous combat stat line (Up key).
    /// </summary>
    public void RecapCombatStatsPrevious()
    {
        if (_combatStatsLines.Count == 0) return;

        if (_combatStatsIndex <= 0)
        {
            _combatStatsIndex = 0;
            TolkWrapper.Speak(_combatStatsLines[0]);
            return;
        }

        _combatStatsIndex--;
        TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
    }

    /// <summary>
    /// Navigate to next combat stat line (Down key).
    /// </summary>
    public void RecapCombatStatsNext()
    {
        if (_combatStatsLines.Count == 0) return;

        if (_combatStatsIndex >= _combatStatsLines.Count - 1)
        {
            TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
            return;
        }

        _combatStatsIndex++;
        TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
    }

}
