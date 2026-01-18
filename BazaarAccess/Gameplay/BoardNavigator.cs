using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Zonas navegables del tablero basadas en el estado del juego.
/// </summary>
public enum BoardZone
{
    // Zonas de selección (del SelectionSet)
    ShopItems,      // Items disponibles para comprar (estado Choice)
    ShopSkills,     // Skills disponibles para seleccionar
    Encounters,     // Encuentros para elegir (estado Encounter)
    Rewards,        // Recompensas (estado Loot)

    // Zonas del jugador (siempre disponibles durante gameplay)
    PlayerItems,    // Items equipados del jugador
    PlayerSkills,   // Habilidades del jugador
    PlayerStash,    // Almacén del jugador
}

/// <summary>
/// Navegador del tablero para accesibilidad.
/// Consciente del estado del juego y muestra contenido contextual.
/// </summary>
public class BoardNavigator
{
    private BoardZone _currentZone = BoardZone.ShopItems;
    private int _currentIndex = 0;
    private readonly List<BoardZone> _availableZones = new List<BoardZone>();

    // Cache de items del SelectionSet por tipo
    private List<Card> _selectionItems = new List<Card>();
    private List<Card> _selectionSkills = new List<Card>();
    private List<Card> _selectionEncounters = new List<Card>();

    // Cache de items del jugador (índices de slots ocupados)
    private List<int> _boardItemIndices = new List<int>();
    private List<int> _skillIndices = new List<int>();
    private List<int> _stashItemIndices = new List<int>();

    public BoardZone CurrentZone => _currentZone;
    public int CurrentIndex => _currentIndex;

    public BoardNavigator()
    {
        UpdateAvailableZones();
    }

    /// <summary>
    /// Obtiene el estado actual del juego.
    /// </summary>
    public ERunState GetCurrentState()
    {
        try
        {
            return Data.CurrentState?.StateName ?? ERunState.Choice;
        }
        catch
        {
            return ERunState.Choice;
        }
    }

    /// <summary>
    /// Verifica si la selección actual es gratuita.
    /// </summary>
    public bool IsSelectionFree()
    {
        try
        {
            return Data.CurrentState?.SelectionContextRules?.SelectionIsFree ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene una descripción del estado actual.
    /// </summary>
    public string GetStateDescription()
    {
        var state = GetCurrentState();
        return state switch
        {
            ERunState.Choice => "Shop phase",
            ERunState.Encounter => "Choose encounter",
            ERunState.Combat => "Combat",
            ERunState.PVPCombat => "PvP Combat",
            ERunState.Loot => "Loot phase",
            ERunState.LevelUp => "Level up",
            ERunState.Pedestal => "Upgrade station",
            ERunState.EndRunVictory => "Victory",
            ERunState.EndRunDefeat => "Defeat",
            _ => state.ToString()
        };
    }

    /// <summary>
    /// Actualiza las zonas disponibles según el estado actual del juego.
    /// </summary>
    public void UpdateAvailableZones()
    {
        _availableZones.Clear();

        // Actualizar items del SelectionSet
        UpdateSelectionItems();

        // Actualizar items del jugador
        UpdateBoardItems();
        UpdateSkills();
        UpdateStashItems();

        var state = GetCurrentState();

        // Agregar zonas de selección según el estado y contenido
        if (_selectionEncounters.Count > 0)
            _availableZones.Add(BoardZone.Encounters);

        if (_selectionItems.Count > 0)
        {
            // En Loot, son recompensas; en Choice, son items de tienda
            _availableZones.Add(state == ERunState.Loot ? BoardZone.Rewards : BoardZone.ShopItems);
        }

        if (_selectionSkills.Count > 0)
            _availableZones.Add(BoardZone.ShopSkills);

        // Agregar zonas del jugador si tienen contenido
        if (_boardItemIndices.Count > 0)
            _availableZones.Add(BoardZone.PlayerItems);

        if (_skillIndices.Count > 0)
            _availableZones.Add(BoardZone.PlayerSkills);

        if (_stashItemIndices.Count > 0)
            _availableZones.Add(BoardZone.PlayerStash);

        // Si no hay zonas, agregar una por defecto basada en el estado
        if (_availableZones.Count == 0)
        {
            _availableZones.Add(state == ERunState.Encounter ? BoardZone.Encounters : BoardZone.ShopItems);
        }

        // Ajustar zona actual si ya no está disponible
        if (!_availableZones.Contains(_currentZone))
        {
            _currentZone = _availableZones[0];
            _currentIndex = 0;
        }

        // Ajustar índice si es mayor que los items disponibles
        int maxIndex = GetMaxIndexForCurrentZone();
        if (_currentIndex >= maxIndex)
            _currentIndex = maxIndex > 0 ? 0 : 0;
    }

    /// <summary>
    /// Actualiza los items del SelectionSet separándolos por tipo.
    /// </summary>
    private void UpdateSelectionItems()
    {
        _selectionItems.Clear();
        _selectionSkills.Clear();
        _selectionEncounters.Clear();

        try
        {
            var selectionSet = Data.CurrentState?.SelectionSet;
            if (selectionSet == null || selectionSet.Count == 0) return;

            foreach (var instanceId in selectionSet)
            {
                var card = Data.GetCard(instanceId);
                if (card == null) continue;

                // Clasificar por tipo de carta
                switch (card.Type)
                {
                    case ECardType.Item:
                        _selectionItems.Add(card);
                        break;
                    case ECardType.Skill:
                        _selectionSkills.Add(card);
                        break;
                    case ECardType.CombatEncounter:
                    case ECardType.EventEncounter:
                    case ECardType.PedestalEncounter:
                    case ECardType.EncounterStep:
                    case ECardType.PvpEncounter:
                        _selectionEncounters.Add(card);
                        break;
                }
            }

            Plugin.Logger.LogInfo($"SelectionSet: {_selectionItems.Count} items, {_selectionSkills.Count} skills, {_selectionEncounters.Count} encounters");
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"Error getting selection items: {ex.Message}");
        }
    }

    private void UpdateBoardItems()
    {
        _boardItemIndices.Clear();
        var boardManager = GetBoardManager();
        if (boardManager?.playerItemSockets == null) return;

        for (int i = 0; i < boardManager.playerItemSockets.Length; i++)
        {
            if (boardManager.playerItemSockets[i]?.CardController?.CardData != null)
                _boardItemIndices.Add(i);
        }
    }

    private void UpdateSkills()
    {
        _skillIndices.Clear();
        var boardManager = GetBoardManager();
        if (boardManager?.playerSkillSockets == null) return;

        for (int i = 0; i < boardManager.playerSkillSockets.Length; i++)
        {
            if (boardManager.playerSkillSockets[i]?.CardController?.CardData != null)
                _skillIndices.Add(i);
        }
    }

    private void UpdateStashItems()
    {
        _stashItemIndices.Clear();
        var boardManager = GetBoardManager();
        if (boardManager?.playerStorageSockets == null) return;

        for (int i = 0; i < boardManager.playerStorageSockets.Length; i++)
        {
            if (boardManager.playerStorageSockets[i]?.CardController?.CardData != null)
                _stashItemIndices.Add(i);
        }
    }

    // --- Navegación ---

    public void NextZone()
    {
        if (_availableZones.Count == 0) return;
        int idx = _availableZones.IndexOf(_currentZone);
        int nextIdx = (idx + 1) % _availableZones.Count;
        GoToZone(_availableZones[nextIdx]);
    }

    public void PreviousZone()
    {
        if (_availableZones.Count == 0) return;
        int idx = _availableZones.IndexOf(_currentZone);
        int prevIdx = (idx - 1 + _availableZones.Count) % _availableZones.Count;
        GoToZone(_availableZones[prevIdx]);
    }

    /// <summary>
    /// Va directamente a una zona específica.
    /// </summary>
    public void GoToZone(BoardZone zone)
    {
        // Actualizar datos antes de cambiar
        UpdateAvailableZones();

        // Verificar si la zona está disponible
        if (!_availableZones.Contains(zone))
        {
            TolkWrapper.Speak($"{GetZoneName(zone)} not available");
            return;
        }

        _currentZone = zone;
        _currentIndex = 0;
        AnnounceCurrentZone();
    }

    /// <summary>
    /// Va a la zona de encuentros si está disponible.
    /// </summary>
    public void GoToEncounters()
    {
        UpdateAvailableZones();

        if (_selectionEncounters.Count > 0)
        {
            GoToZone(BoardZone.Encounters);
        }
        else
        {
            TolkWrapper.Speak("No encounters available");
        }
    }

    /// <summary>
    /// Va a la zona de tienda (items o skills).
    /// </summary>
    public void GoToShop()
    {
        UpdateAvailableZones();

        var state = GetCurrentState();

        if (state == ERunState.Loot && _selectionItems.Count > 0)
        {
            GoToZone(BoardZone.Rewards);
        }
        else if (_selectionItems.Count > 0)
        {
            GoToZone(BoardZone.ShopItems);
        }
        else if (_selectionSkills.Count > 0)
        {
            GoToZone(BoardZone.ShopSkills);
        }
        else
        {
            TolkWrapper.Speak("Shop empty");
        }
    }

    public void NextItem()
    {
        int maxIndex = GetMaxIndexForCurrentZone();
        if (maxIndex == 0) return;

        _currentIndex = (_currentIndex + 1) % maxIndex;
        AnnounceCurrentItem();
    }

    public void PreviousItem()
    {
        int maxIndex = GetMaxIndexForCurrentZone();
        if (maxIndex == 0) return;

        _currentIndex = (_currentIndex - 1 + maxIndex) % maxIndex;
        AnnounceCurrentItem();
    }

    // --- Anuncios ---

    public void AnnounceCurrentZone()
    {
        string zoneName = GetZoneName(_currentZone);
        int count = GetMaxIndexForCurrentZone();

        string announcement = count > 0
            ? $"{zoneName}, {count} items"
            : $"{zoneName}, empty";

        TolkWrapper.Speak(announcement);

        if (count > 0)
            AnnounceCurrentItem();
    }

    public void AnnounceCurrentItem()
    {
        int maxIndex = GetMaxIndexForCurrentZone();
        if (maxIndex == 0)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        var card = GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("Empty");
            return;
        }

        string description = GetItemDescription(card);
        TolkWrapper.Speak(description);
    }

    private string GetItemDescription(Card card)
    {
        // Descripción basada en el tipo de zona
        switch (_currentZone)
        {
            case BoardZone.ShopItems:
                if (IsSelectionFree())
                    return $"{ItemReader.GetCardName(card)}, free";
                return ItemReader.GetBuyInfo(card);

            case BoardZone.ShopSkills:
                return ItemReader.GetCardName(card);

            case BoardZone.Encounters:
                return ItemReader.GetEncounterInfo(card);

            case BoardZone.Rewards:
                return $"{ItemReader.GetCardName(card)}, {ItemReader.GetTierName(card)}";

            default:
                return ItemReader.GetShortDescription(card);
        }
    }

    public void ReadDetailedInfo()
    {
        var card = GetCurrentCard();
        if (card == null)
        {
            TolkWrapper.Speak("No item");
            return;
        }

        // Para encuentros, usar descripción específica
        if (_currentZone == BoardZone.Encounters)
        {
            TolkWrapper.Speak(ItemReader.GetEncounterDetailedInfo(card));
        }
        else
        {
            TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
        }
    }

    // --- Acceso a datos ---

    public Card GetCurrentCard()
    {
        switch (_currentZone)
        {
            case BoardZone.ShopItems:
            case BoardZone.Rewards:
                if (_currentIndex < _selectionItems.Count)
                    return _selectionItems[_currentIndex];
                break;

            case BoardZone.ShopSkills:
                if (_currentIndex < _selectionSkills.Count)
                    return _selectionSkills[_currentIndex];
                break;

            case BoardZone.Encounters:
                if (_currentIndex < _selectionEncounters.Count)
                    return _selectionEncounters[_currentIndex];
                break;

            case BoardZone.PlayerItems:
                if (_currentIndex < _boardItemIndices.Count)
                {
                    int socketIdx = _boardItemIndices[_currentIndex];
                    return GetBoardManager()?.playerItemSockets[socketIdx]?.CardController?.CardData;
                }
                break;

            case BoardZone.PlayerSkills:
                if (_currentIndex < _skillIndices.Count)
                {
                    int socketIdx = _skillIndices[_currentIndex];
                    return GetBoardManager()?.playerSkillSockets[socketIdx]?.CardController?.CardData;
                }
                break;

            case BoardZone.PlayerStash:
                if (_currentIndex < _stashItemIndices.Count)
                {
                    int socketIdx = _stashItemIndices[_currentIndex];
                    return GetBoardManager()?.playerStorageSockets[socketIdx]?.CardController?.CardData;
                }
                break;
        }

        return null;
    }

    public ItemController GetCurrentItemController()
    {
        var boardManager = GetBoardManager();
        if (boardManager == null) return null;

        switch (_currentZone)
        {
            case BoardZone.PlayerItems:
                if (_currentIndex < _boardItemIndices.Count)
                {
                    int socketIdx = _boardItemIndices[_currentIndex];
                    return boardManager.playerItemSockets[socketIdx]?.CardController as ItemController;
                }
                break;

            case BoardZone.PlayerStash:
                if (_currentIndex < _stashItemIndices.Count)
                {
                    int socketIdx = _stashItemIndices[_currentIndex];
                    return boardManager.playerStorageSockets[socketIdx]?.CardController as ItemController;
                }
                break;
        }

        return null;
    }

    public bool IsInSelectionZone()
    {
        return _currentZone == BoardZone.ShopItems ||
               _currentZone == BoardZone.ShopSkills ||
               _currentZone == BoardZone.Encounters ||
               _currentZone == BoardZone.Rewards;
    }

    public bool IsInShop() => _currentZone == BoardZone.ShopItems;
    public bool IsInEncounters() => _currentZone == BoardZone.Encounters;

    // --- Helpers ---

    private int GetMaxIndexForCurrentZone()
    {
        return _currentZone switch
        {
            BoardZone.ShopItems => _selectionItems.Count,
            BoardZone.ShopSkills => _selectionSkills.Count,
            BoardZone.Encounters => _selectionEncounters.Count,
            BoardZone.Rewards => _selectionItems.Count,
            BoardZone.PlayerItems => _boardItemIndices.Count,
            BoardZone.PlayerSkills => _skillIndices.Count,
            BoardZone.PlayerStash => _stashItemIndices.Count,
            _ => 0
        };
    }

    private BoardManager GetBoardManager()
    {
        try { return Singleton<BoardManager>.Instance; }
        catch { return null; }
    }

    private string GetZoneName(BoardZone zone)
    {
        return zone switch
        {
            BoardZone.ShopItems => "Shop",
            BoardZone.ShopSkills => "Skills for sale",
            BoardZone.Encounters => "Encounters",
            BoardZone.Rewards => "Rewards",
            BoardZone.PlayerItems => "Board",
            BoardZone.PlayerSkills => "Your skills",
            BoardZone.PlayerStash => "Stash",
            _ => zone.ToString()
        };
    }
}
