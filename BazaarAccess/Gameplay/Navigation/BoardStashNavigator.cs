using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Manages board and stash data: refresh, slot navigation, stash toggle, capacity announcements.
/// </summary>
public class BoardStashNavigator
{
    private List<int> _boardIndices = new List<int>();
    private List<int> _stashIndices = new List<int>();
    private bool _stashOpen = false;
    private NavigationSection _sectionBeforeStash = NavigationSection.Selection;

    // ===============================================
    // PROPERTIES
    // ===============================================

    public int BoardCount => _boardIndices.Count;
    public int StashCount => _stashIndices.Count;
    public bool IsStashOpen => _stashOpen;
    public NavigationSection SectionBeforeStash => _sectionBeforeStash;

    // ===============================================
    // REFRESH
    // ===============================================

    public void RefreshBoard()
    {
        _boardIndices.Clear();
        var bm = GetBoardManager();
        if (bm?.playerItemSockets == null) return;

        var seenItems = new HashSet<InstanceId>();

        for (int i = 0; i < bm.playerItemSockets.Length; i++)
        {
            var card = bm.playerItemSockets[i]?.CardController?.CardData;
            if (card != null)
            {
                if (!seenItems.Contains(card.InstanceId))
                {
                    seenItems.Add(card.InstanceId);
                    _boardIndices.Add(i);
                }
            }
        }
    }

    public void RefreshStash()
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

    // ===============================================
    // CARD ACCESS
    // ===============================================

    public Card GetBoardCard(int currentIndex)
    {
        var bm = GetBoardManager();
        if (currentIndex < _boardIndices.Count && bm != null)
        {
            int idx = _boardIndices[currentIndex];
            return bm.playerItemSockets[idx]?.CardController?.CardData;
        }
        return null;
    }

    public Card GetStashCard(int currentIndex)
    {
        var bm = GetBoardManager();
        if (currentIndex < _stashIndices.Count && bm != null)
        {
            int idx = _stashIndices[currentIndex];
            return GetStashSocketCard(bm, idx);
        }
        return null;
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
    // SLOT NAVIGATION
    // ===============================================

    public int GetCurrentBoardSlot(int currentIndex)
    {
        if (currentIndex < 0 || currentIndex >= _boardIndices.Count) return -1;
        return _boardIndices[currentIndex];
    }

    public bool GoToBoardSlot(int targetSlot, ref int currentIndex)
    {
        for (int i = 0; i < _boardIndices.Count; i++)
        {
            if (_boardIndices[i] == targetSlot)
            {
                currentIndex = i;
                return true;
            }
        }

        Plugin.Logger.LogWarning($"GoToBoardSlot: targetSlot {targetSlot} not found in _boardIndices. Count={_boardIndices.Count}");
        if (_boardIndices.Count > 0 && currentIndex >= _boardIndices.Count)
        {
            currentIndex = _boardIndices.Count - 1;
        }
        return false;
    }

    public int GetCurrentStashSlot(int currentIndex)
    {
        if (currentIndex < 0 || currentIndex >= _stashIndices.Count) return -1;
        return _stashIndices[currentIndex];
    }

    public bool GoToStashSlot(int targetSlot, ref int currentIndex)
    {
        for (int i = 0; i < _stashIndices.Count; i++)
        {
            if (_stashIndices[i] == targetSlot)
            {
                currentIndex = i;
                return true;
            }
        }

        Plugin.Logger.LogWarning($"GoToStashSlot: targetSlot {targetSlot} not found");
        if (_stashIndices.Count > 0 && currentIndex >= _stashIndices.Count)
        {
            currentIndex = _stashIndices.Count - 1;
        }
        return false;
    }

    public bool GoToItemById(InstanceId instanceId, NavigationSection section, ref int currentIndex)
    {
        var bm = GetBoardManager();

        if (section == NavigationSection.Board)
        {
            if (bm?.playerItemSockets == null) return false;

            for (int i = 0; i < _boardIndices.Count; i++)
            {
                int slot = _boardIndices[i];
                var card = bm.playerItemSockets[slot]?.CardController?.CardData;
                if (card != null && card.InstanceId == instanceId)
                {
                    currentIndex = i;
                    return true;
                }
            }

            Plugin.Logger.LogWarning($"GoToItemById: item with InstanceId not found on board");
            return false;
        }

        if (section == NavigationSection.Stash)
        {
            if (bm?.playerStorageSockets == null) return false;

            for (int i = 0; i < _stashIndices.Count; i++)
            {
                int slot = _stashIndices[i];
                var card = bm.playerStorageSockets[slot]?.CardController?.CardData;
                if (card != null && card.InstanceId == instanceId)
                {
                    currentIndex = i;
                    return true;
                }
            }

            Plugin.Logger.LogWarning($"GoToItemById: item with InstanceId not found in stash");
            return false;
        }

        return false;
    }

    public Card GetItemAtSlot(int slot)
    {
        if (slot < 0 || slot >= 10) return null;

        var bm = GetBoardManager();
        if (bm?.playerItemSockets == null) return null;

        if (slot < bm.playerItemSockets.Length)
        {
            return bm.playerItemSockets[slot]?.CardController?.CardData;
        }
        return null;
    }

    // ===============================================
    // STASH STATE
    // ===============================================

    public void SetStashState(bool isOpen, NavigationSection currentSection)
    {
        if (isOpen && !_stashOpen)
        {
            _sectionBeforeStash = currentSection;
        }

        _stashOpen = isOpen;

        if (isOpen)
        {
            RefreshStash();
        }
        else
        {
            _stashIndices.Clear();
        }
    }

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

            if (!bm.AllowInteraction)
            {
                TolkWrapper.Speak("Cannot open stash now");
                return;
            }

            bm.TryToggleStorage();
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ToggleStash error: {ex.Message}");
            TolkWrapper.Speak("Cannot toggle stash");
        }
    }

    // ===============================================
    // CAPACITY ANNOUNCEMENTS
    // ===============================================

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

            int unlockedCount = 0;
            for (int i = 0; i < 10; i++)
            {
                if (!container.IsSocketLocked((EContainerSocketId)i))
                {
                    unlockedCount++;
                }
            }

            var socketables = container.GetSocketableList();
            int usedCapacity = 0;
            foreach (var socketable in socketables)
            {
                usedCapacity += (int)socketable.Size;
            }

            int freeSlots = container.CountEmptySockets();
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

            int totalSlots = 10;
            var socketables = container.GetSocketableList();
            int usedCapacity = 0;
            foreach (var socketable in socketables)
            {
                usedCapacity += (int)socketable.Size;
            }

            int freeSlots = container.CountEmptySockets();
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

    // ===============================================
    // VISUAL SELECTION
    // ===============================================

    public CardController GetBoardCardController(int currentIndex)
    {
        var bm = GetBoardManager();
        if (bm == null || currentIndex >= _boardIndices.Count) return null;
        int idx = _boardIndices[currentIndex];
        return bm.playerItemSockets[idx]?.CardController;
    }

    public CardController GetStashCardController(int currentIndex)
    {
        var bm = GetBoardManager();
        if (bm == null || currentIndex >= _stashIndices.Count) return null;
        int idx = _stashIndices[currentIndex];
        return bm.playerStorageSockets?[idx]?.CardController;
    }

    // ===============================================
    // HELPERS
    // ===============================================

    internal static BoardManager GetBoardManager()
    {
        try { return Singleton<BoardManager>.Instance; }
        catch { return null; }
    }
}
