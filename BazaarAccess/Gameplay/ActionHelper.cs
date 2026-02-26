using System;
using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Cards;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Utilities;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Helper para ejecutar acciones del gameplay sin drag-drop.
/// </summary>
public static class ActionHelper
{
    /// <summary>
    /// Compra un item del merchant.
    /// </summary>
    /// <param name="card">El item a comprar</param>
    /// <param name="toStash">True para comprar al stash, false para comprar al tablero</param>
    /// <param name="silent">True para no anunciar (el llamador maneja el mensaje)</param>
    /// <param name="isFree">True si el item es gratuito (loot/rewards)</param>
    /// <returns>True si la compra fue exitosa</returns>
    public static bool BuyItem(ItemCard card, bool toStash = false, bool silent = false, bool isFree = false)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("BuyItem: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("BuyItem: AppState.CurrentState is null");
            if (!silent) TolkWrapper.Speak("Cannot buy now");
            return false;
        }

        try
        {
            var section = toStash ? EInventorySection.Stash : EInventorySection.Hand;
            state.BuyItemCommand(card, section);

            string name = ItemReader.GetCardName(card);

            if (!silent)
            {
                if (isFree)
                {
                    TolkWrapper.Speak($"Acquired {name}");
                }
                else
                {
                    int price = ItemReader.GetBuyPrice(card);
                    if (price > 0)
                        TolkWrapper.Speak($"Bought {name} for {price} gold");
                    else
                        TolkWrapper.Speak($"Acquired {name}");
                }
            }

            Plugin.Logger.LogInfo($"BuyItem: {name} to {section}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"BuyItem failed: {ex.Message}");
            if (!silent) TolkWrapper.Speak("Purchase failed");
            return false;
        }
    }

    /// <summary>
    /// Vende un item del jugador.
    /// </summary>
    /// <param name="card">El item a vender</param>
    /// <returns>True si la venta fue exitosa</returns>
    public static bool SellItem(ItemCard card)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("SellItem: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("SellItem: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot sell now");
            return false;
        }

        try
        {
            string name = ItemReader.GetCardName(card);
            int price = ItemReader.GetSellPrice(card);

            state.SellCardCommand(card);

            TolkWrapper.Speak($"Sold {name} for {price} gold");

            Plugin.Logger.LogInfo($"SellItem: {name} for {price}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"SellItem failed: {ex.Message}");
            TolkWrapper.Speak("Sale failed");
            return false;
        }
    }

    /// <summary>
    /// Mueve un item entre Hand y Stash.
    /// </summary>
    /// <param name="card">El item a mover</param>
    /// <param name="toStash">True para mover al stash, false para mover al tablero</param>
    /// <returns>True si el movimiento fue exitoso</returns>
    public static bool MoveItem(ItemCard card, bool toStash)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("MoveItem: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("MoveItem: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot move now");
            return false;
        }

        // Verificar si se puede mover en el estado actual
        if (!state.CanHandleOperation(StateOps.MoveItem))
        {
            TolkWrapper.Speak("Cannot move items now");
            return false;
        }

        try
        {
            var section = toStash ? EInventorySection.Stash : EInventorySection.Hand;
            var player = Data.Run?.Player;
            if (player == null)
            {
                TolkWrapper.Speak("Player data not available");
                return false;
            }

            // Verificar si hay espacio en el destino ANTES de mover
            CardContainer targetContainer = (section == EInventorySection.Hand ?
                player.Hand : player.Stash) as CardContainer;

            if (targetContainer == null)
            {
                TolkWrapper.Speak("Cannot access destination");
                return false;
            }

            if (!targetContainer.HasSpaceForCard(card))
            {
                string destination = toStash ? "stash" : "board";
                TolkWrapper.Speak($"No space in {destination}");
                Plugin.Logger.LogInfo($"MoveItem: No space for {card.Size} size card in {destination}");
                return false;
            }

            // Obtener los sockets disponibles para este item
            var desiredSockets = CardOperationUtility.GetAvailableSockets(card, section);
            if (desiredSockets == null || desiredSockets.Count == 0)
            {
                // Fallback a sockets desde 0
                desiredSockets = new System.Collections.Generic.List<EContainerSocketId>();
                for (int i = 0; i < (int)card.Size; i++)
                {
                    desiredSockets.Add((EContainerSocketId)i);
                }
            }

            state.MoveCardCommand(card, desiredSockets, section);

            string name = ItemReader.GetCardName(card);
            string dest = toStash ? "Stash" : "Board";
            TolkWrapper.Speak($"Moved {name} to {dest}");

            Plugin.Logger.LogInfo($"MoveItem: {name} to {section}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"MoveItem failed: {ex.Message}");
            TolkWrapper.Speak("Move failed");
            return false;
        }
    }

    /// <summary>
    /// Verifica si se puede comprar el item actual.
    /// </summary>
    public static bool CanBuy(Card card)
    {
        if (card == null) return false;

        // Verificar si tenemos suficiente oro
        // TODO: Obtener el oro del jugador y comparar con el precio
        return true;
    }

    /// <summary>
    /// Verifica si se puede vender el item actual.
    /// </summary>
    public static bool CanSell(Card card)
    {
        if (card == null) return false;

        // Check for Unsellable hidden tag
        if (card.HiddenTags != null && card.HiddenTags.Contains(EHiddenTag.Unsellable))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Selecciona una skill del SelectionSet.
    /// </summary>
    public static bool SelectSkill(SkillCard card)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("SelectSkill: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("SelectSkill: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot select now");
            return false;
        }

        try
        {
            state.SelectSkillCommand(card);

            string name = ItemReader.GetCardName(card);
            TolkWrapper.Speak($"Selected {name}");

            Plugin.Logger.LogInfo($"SelectSkill: {name}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"SelectSkill failed: {ex.Message}");
            TolkWrapper.Speak("Selection failed");
            return false;
        }
    }

    /// <summary>
    /// Selecciona un encuentro del SelectionSet.
    /// </summary>
    public static bool SelectEncounter(Card card)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("SelectEncounter: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("SelectEncounter: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot select now");
            return false;
        }

        try
        {
            state.SelectEncounterCommand(card.InstanceId);

            string name = ItemReader.GetCardName(card);
            TolkWrapper.Speak($"Selected {name}");

            Plugin.Logger.LogInfo($"SelectEncounter: {card.InstanceId}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"SelectEncounter failed: {ex.Message}");
            TolkWrapper.Speak("Selection failed");
            return false;
        }
    }

    /// <summary>
    /// Reordena un item en el tablero (mueve a un slot adyacente).
    /// </summary>
    /// <param name="card">El item a mover</param>
    /// <param name="currentSlot">Slot actual del item (0-9)</param>
    /// <param name="direction">-1 para izquierda, +1 para derecha</param>
    /// <param name="silent">If true, don't announce anything (caller handles feedback)</param>
    /// <returns>True si el movimiento fue exitoso</returns>
    public static bool ReorderItem(ItemCard card, int currentSlot, int direction, bool silent = false)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("ReorderItem: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("ReorderItem: AppState.CurrentState is null");
            return false;
        }

        try
        {
            int cardSize = (int)card.Size;
            int newSlot = currentSlot + direction;

            // Verificar límites del tablero (10 slots, 0-9)
            // El item no puede ir más allá del borde
            if (newSlot < 0)
            {
                if (!silent) TolkWrapper.Speak("At left edge");
                return false;
            }
            if (newSlot + cardSize > 10)
            {
                if (!silent) TolkWrapper.Speak("At right edge");
                return false;
            }

            // Crear lista de sockets destino según el tamaño de la carta
            var desiredSockets = new System.Collections.Generic.List<EContainerSocketId>();
            for (int i = 0; i < cardSize; i++)
            {
                desiredSockets.Add((EContainerSocketId)(newSlot + i));
            }

            state.MoveCardCommand(card, desiredSockets, EInventorySection.Hand);

            Plugin.Logger.LogInfo($"ReorderItem: {ItemReader.GetCardName(card)} from slot {currentSlot} to {newSlot}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ReorderItem failed: {ex.Message}");
            if (!silent) TolkWrapper.Speak("Move failed");
            return false;
        }
    }

    /// <summary>
    /// Reorders an item in the stash (moves to an adjacent slot).
    /// </summary>
    public static bool ReorderStashItem(ItemCard card, int currentSlot, int direction, bool silent = false)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("ReorderStashItem: card is null");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("ReorderStashItem: AppState.CurrentState is null");
            return false;
        }

        try
        {
            int newSlot = currentSlot + direction;

            // Get stash size from BoardManager
            var bm = Singleton<BoardManager>.Instance;
            int stashSize = bm?.playerStorageSockets?.Length ?? 10;

            if (newSlot < 0)
            {
                if (!silent) TolkWrapper.Speak("At start");
                return false;
            }
            if (newSlot >= stashSize)
            {
                if (!silent) TolkWrapper.Speak("At end");
                return false;
            }

            var desiredSockets = new System.Collections.Generic.List<EContainerSocketId>();
            desiredSockets.Add((EContainerSocketId)newSlot);

            state.MoveCardCommand(card, desiredSockets, EInventorySection.Stash);

            Plugin.Logger.LogInfo($"ReorderStashItem: {ItemReader.GetCardName(card)} from slot {currentSlot} to {newSlot}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"ReorderStashItem failed: {ex.Message}");
            if (!silent) TolkWrapper.Speak("Move failed");
            return false;
        }
    }

}
