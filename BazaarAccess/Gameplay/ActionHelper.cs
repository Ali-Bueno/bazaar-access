using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Core;
using BazaarAccess.Patches;
using BazaarGameClient.Domain.Cards;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Cards.Encounter.Pedestal;
using BazaarGameShared.Domain.Cards.Encounter.Pedestal.Behaviors;
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

    /// <summary>
    /// Upgrades an item at the pedestal.
    /// Only works when in Pedestal state.
    /// </summary>
    /// <param name="card">The item to upgrade</param>
    /// <returns>True if the upgrade was initiated</returns>
    public static bool UpgradeItem(Card card)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("UpgradeItem: card is null");
            return false;
        }

        // Check if we're in Pedestal state
        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState != ERunState.Pedestal)
        {
            TolkWrapper.Speak("Can only upgrade at a pedestal");
            return false;
        }

        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("UpgradeItem: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot upgrade now");
            return false;
        }

        // Check if CommitToPedestal operation is allowed
        if (!state.CanHandleOperation(StateOps.CommitToPedestal))
        {
            TolkWrapper.Speak("Cannot upgrade this item");
            return false;
        }

        // Check if the card can be upgraded (not already at max tier)
        if (card.Tier == ETier.Legendary)
        {
            TolkWrapper.Speak("Item is already at maximum tier");
            return false;
        }

        try
        {
            string name = ItemReader.GetCardName(card);
            string currentTier = ItemReader.GetTierName(card);
            var pedestalInfo = GetCurrentPedestalInfo();
            string nextTier;
            if (pedestalInfo.TargetTier.HasValue && pedestalInfo.TargetTier.Value != card.Tier)
            {
                nextTier = GetTierDisplayName(pedestalInfo.TargetTier.Value);
            }
            else if (pedestalInfo.TargetTier.HasValue && pedestalInfo.TargetTier.Value == card.Tier)
            {
                nextTier = null; // stats-only upgrade
            }
            else
            {
                nextTier = GetNextTierName(card.Tier);
            }

            // Trigger the same events as mouse drag-drop for full visual/audio feedback
            var controller = Data.CardAndSkillLookup?.GetCardController(card) as ItemController;
            if (controller != null)
            {
                TriggerItemDroppedOnPedestalEvent(controller);
            }

            // Mark that an upgrade process is starting
            if (Singleton<BoardManager>.Instance != null)
            {
                Singleton<BoardManager>.Instance.MarkUpgradeOrFuseOrEnchantProcessing();
            }

            state.CommitToPedestalCommand(card.InstanceId);

            if (nextTier != null)
            {
                TolkWrapper.Speak($"Upgrading {name} from {currentTier} to {nextTier}");
                Plugin.Logger.LogInfo($"UpgradeItem: {name} ({currentTier} -> {nextTier})");
            }
            else
            {
                TolkWrapper.Speak($"Upgrading {name} stats");
                Plugin.Logger.LogInfo($"UpgradeItem: {name} (stats upgrade, stays {currentTier})");
            }
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"UpgradeItem failed: {ex.Message}");
            TolkWrapper.Speak("Upgrade failed");
            return false;
        }
    }

    /// <summary>
    /// Gets the name of the next tier.
    /// </summary>
    /// <summary>
    /// Gets a display-friendly name for a tier enum value.
    /// </summary>
    private static string GetTierDisplayName(ETier tier)
    {
        return tier switch
        {
            ETier.Bronze => "Bronze",
            ETier.Silver => "Silver",
            ETier.Gold => "Gold",
            ETier.Diamond => "Diamond",
            ETier.Legendary => "Legendary",
            _ => tier.ToString()
        };
    }

    private static string GetNextTierName(ETier currentTier)
    {
        switch (currentTier)
        {
            case ETier.Bronze: return "Silver";
            case ETier.Silver: return "Gold";
            case ETier.Gold: return "Diamond";
            case ETier.Diamond: return "Legendary";
            default: return "max";
        }
    }

    /// <summary>
    /// Checks if the current state allows upgrading items.
    /// </summary>
    public static bool CanUpgrade()
    {
        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState != ERunState.Pedestal)
            return false;

        var state = AppState.CurrentState;
        if (state == null)
            return false;

        return state.CanHandleOperation(StateOps.CommitToPedestal);
    }

    /// <summary>
    /// Triggers Events.ItemDroppedOnPedestal via reflection since Events is internal.
    /// </summary>
    private static void TriggerItemDroppedOnPedestalEvent(ItemController controller)
    {
        try
        {
            // Get the Events class from TheBazaar assembly
            var eventsType = typeof(Data).Assembly.GetType("TheBazaar.Events");
            if (eventsType == null)
            {
                Plugin.Logger.LogWarning("TriggerItemDroppedOnPedestalEvent: Events type not found");
                return;
            }

            // Get the ItemDroppedOnPedestal field
            var eventField = eventsType.GetField("ItemDroppedOnPedestal",
                BindingFlags.Public | BindingFlags.Static);
            if (eventField == null)
            {
                Plugin.Logger.LogWarning("TriggerItemDroppedOnPedestalEvent: ItemDroppedOnPedestal field not found");
                return;
            }

            // Get the event instance
            var eventInstance = eventField.GetValue(null);
            if (eventInstance == null)
            {
                Plugin.Logger.LogWarning("TriggerItemDroppedOnPedestalEvent: Event instance is null");
                return;
            }

            // Call Trigger method
            var triggerMethod = eventInstance.GetType().GetMethod("Trigger",
                BindingFlags.Public | BindingFlags.Instance);
            if (triggerMethod != null)
            {
                triggerMethod.Invoke(eventInstance, new object[] { controller });
                Plugin.Logger.LogInfo("TriggerItemDroppedOnPedestalEvent: Event triggered successfully");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"TriggerItemDroppedOnPedestalEvent failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pedestal/altar type information.
    /// </summary>
    public enum PedestalType
    {
        None,
        Upgrade,
        Enchant,
        EnchantRandom
    }

    /// <summary>
    /// Information about the current pedestal/altar.
    /// </summary>
    public class PedestalInfo
    {
        public PedestalType Type { get; set; } = PedestalType.None;
        public string EnchantmentName { get; set; }
        public ETier? TargetTier { get; set; }
    }

    // Cached pedestal info - set once when entering Pedestal state, cleared on exit
    private static PedestalInfo _cachedPedestalInfo;

    /// <summary>
    /// Caches pedestal info when entering Pedestal state.
    /// Called from StateChangePatch on state transition.
    /// </summary>
    public static void CachePedestalInfo()
    {
        _cachedPedestalInfo = DetectPedestalInfo();
        Plugin.Logger.LogInfo($"CachePedestalInfo: Type={_cachedPedestalInfo.Type}, Enchant={_cachedPedestalInfo.EnchantmentName}, TargetTier={_cachedPedestalInfo.TargetTier}");
    }

    /// <summary>
    /// Clears cached pedestal info when leaving Pedestal state.
    /// </summary>
    public static void ClearPedestalCache()
    {
        _cachedPedestalInfo = null;
    }

    /// <summary>
    /// Gets information about the current pedestal/altar.
    /// Returns cached info if available, otherwise re-detects.
    /// </summary>
    public static PedestalInfo GetCurrentPedestalInfo()
    {
        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState != ERunState.Pedestal)
        {
            return new PedestalInfo();
        }

        // Return cached info if available
        if (_cachedPedestalInfo != null && _cachedPedestalInfo.Type != PedestalType.None)
        {
            return _cachedPedestalInfo;
        }

        // Cache miss - try to detect now (may happen if state transition was missed)
        var info = DetectPedestalInfo();
        if (info.Type != PedestalType.None)
        {
            _cachedPedestalInfo = info;
        }
        return info;
    }

    /// <summary>
    /// Detects pedestal type using two strategies:
    /// 1. Public Data API (Data.GetStatic().GetCardById(CurrentEncounterId))
    /// 2. Fallback: reflection on PedestalState._pedestalTemplate from AppState.CurrentState
    /// </summary>
    private static PedestalInfo DetectPedestalInfo()
    {
        var info = new PedestalInfo();

        // Strategy 1: Public Data API
        info = DetectViaDataApi();
        if (info.Type != PedestalType.None)
        {
            Plugin.Logger.LogInfo($"DetectPedestalInfo: Data API succeeded - Type={info.Type}");
            return info;
        }

        // Strategy 2: Reflection on PedestalState._pedestalTemplate
        info = DetectViaPedestalStateReflection();
        if (info.Type != PedestalType.None)
        {
            Plugin.Logger.LogInfo($"DetectPedestalInfo: PedestalState reflection succeeded - Type={info.Type}");
            return info;
        }

        Plugin.Logger.LogWarning("DetectPedestalInfo: Both detection strategies failed");
        return info;
    }

    /// <summary>
    /// Detects pedestal type via Data.GetStatic().GetCardById(CurrentEncounterId).
    /// </summary>
    private static PedestalInfo DetectViaDataApi()
    {
        var info = new PedestalInfo();

        try
        {
            var encounterId = Data.CurrentEncounterId;
            if (encounterId == null || encounterId.Value == Guid.Empty)
            {
                Plugin.Logger.LogWarning("DetectViaDataApi: CurrentEncounterId is null/empty");
                return info;
            }

            var staticData = Data.GetStatic().Result;
            if (staticData == null)
            {
                Plugin.Logger.LogWarning("DetectViaDataApi: static data manager is null");
                return info;
            }

            var encounterCard = staticData.GetCardById(encounterId.Value);
            if (encounterCard == null)
            {
                Plugin.Logger.LogWarning("DetectViaDataApi: encounter card not found");
                return info;
            }

            var pedestal = encounterCard as TCardEncounterPedestal;
            if (pedestal == null)
            {
                Plugin.Logger.LogWarning($"DetectViaDataApi: encounter card is {encounterCard.GetType().Name}, not TCardEncounterPedestal");
                return info;
            }

            ExtractBehaviorInfo(pedestal.Behavior, info);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"DetectViaDataApi error: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Fallback: Detects pedestal type via reflection on AppState.CurrentState (PedestalState._pedestalTemplate).
    /// </summary>
    private static PedestalInfo DetectViaPedestalStateReflection()
    {
        var info = new PedestalInfo();

        try
        {
            var currentState = AppState.CurrentState;
            if (currentState == null)
            {
                Plugin.Logger.LogWarning("DetectViaPedestalStateReflection: AppState.CurrentState is null");
                return info;
            }

            // Check if currentState is PedestalState
            var stateType = currentState.GetType();
            if (!stateType.Name.Contains("Pedestal"))
            {
                Plugin.Logger.LogWarning($"DetectViaPedestalStateReflection: CurrentState is {stateType.Name}, not PedestalState");
                return info;
            }

            // Access _pedestalTemplate private field
            var templateField = stateType.GetField("_pedestalTemplate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (templateField == null)
            {
                Plugin.Logger.LogWarning("DetectViaPedestalStateReflection: _pedestalTemplate field not found");
                return info;
            }

            var pedestal = templateField.GetValue(currentState) as TCardEncounterPedestal;
            if (pedestal == null)
            {
                Plugin.Logger.LogWarning("DetectViaPedestalStateReflection: _pedestalTemplate is null or wrong type");
                return info;
            }

            ExtractBehaviorInfo(pedestal.Behavior, info);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"DetectViaPedestalStateReflection error: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Extracts pedestal type info from a behavior object into PedestalInfo.
    /// </summary>
    private static void ExtractBehaviorInfo(object behavior, PedestalInfo info)
    {
        if (behavior == null)
        {
            Plugin.Logger.LogWarning("ExtractBehaviorInfo: Behavior is null");
            return;
        }

        if (behavior is TPedestalBehaviorUpgrade upgradeBehavior)
        {
            info.Type = PedestalType.Upgrade;
            info.TargetTier = upgradeBehavior.TargetTier;
        }
        else if (behavior is TPedestalBehaviorEnchantRandom)
        {
            info.Type = PedestalType.EnchantRandom;
            info.EnchantmentName = "Random";
        }
        else if (behavior is TPedestalBehaviorEnchant enchantBehavior)
        {
            info.Type = PedestalType.Enchant;
            info.EnchantmentName = enchantBehavior.Enchantment.ToString();
        }
    }

    /// <summary>
    /// Gets a description of what will happen when using the current pedestal.
    /// </summary>
    public static string GetPedestalActionDescription(Card card)
    {
        var pedestalInfo = GetCurrentPedestalInfo();

        string cardName = ItemReader.GetCardName(card);
        string currentTier = ItemReader.GetTierName(card);

        switch (pedestalInfo.Type)
        {
            case PedestalType.Upgrade:
                string nextTier = GetNextTierName(card.Tier);
                if (pedestalInfo.TargetTier.HasValue)
                {
                    nextTier = GetTierDisplayName(pedestalInfo.TargetTier.Value);
                }
                return $"Upgrade {cardName} from {currentTier} to {nextTier}";

            case PedestalType.Enchant:
                return $"Enchant {cardName} with {pedestalInfo.EnchantmentName}";

            case PedestalType.EnchantRandom:
                return $"Enchant {cardName} with a random enchantment";

            default:
                return $"Use {cardName} at pedestal";
        }
    }

    /// <summary>
    /// Gets a preview of post-upgrade stats for the target tier.
    /// Shows full stats the item will have after upgrading.
    /// </summary>
    public static List<string> GetUpgradePreview(Card card)
    {
        var stats = new List<string>();
        if (card == null) return stats;

        try
        {
            var pedestalInfo = GetCurrentPedestalInfo();
            ETier targetTier = pedestalInfo.TargetTier ?? GetNextTier(card.Tier);

            if (targetTier == card.Tier)
            {
                stats.Add("Stats will improve (same tier)");
                return stats;
            }

            // Get the card template to access tier data
            var templateProp = card.GetType().GetProperty("Template");
            if (templateProp == null) return stats;

            var template = templateProp.GetValue(card);
            if (template == null) return stats;

            // Try GetAttributeBaseValueAtTier method first
            var getAttrMethod = template.GetType().GetMethod("GetAttributeBaseValueAtTier",
                BindingFlags.Public | BindingFlags.Instance);

            if (getAttrMethod == null)
            {
                foreach (var iface in template.GetType().GetInterfaces())
                {
                    getAttrMethod = iface.GetMethod("GetAttributeBaseValueAtTier");
                    if (getAttrMethod != null) break;
                }
            }

            if (getAttrMethod == null)
            {
                // Fallback: try Tiers dictionary
                var tiersProp = template.GetType().GetProperty("Tiers", BindingFlags.Public | BindingFlags.Instance);
                if (tiersProp != null)
                {
                    var tiersDict = tiersProp.GetValue(template) as System.Collections.IDictionary;
                    if (tiersDict != null)
                        return GetStatsFromTiersDictionary(tiersDict, targetTier);
                }
                return stats;
            }

            // Read target tier stats
            var attrTypes = new ECardAttributeType[]
            {
                ECardAttributeType.DamageAmount,
                ECardAttributeType.HealAmount,
                ECardAttributeType.ShieldApplyAmount,
                ECardAttributeType.Cooldown,
                ECardAttributeType.CooldownMax,
                ECardAttributeType.Ammo,
                ECardAttributeType.AmmoMax,
                ECardAttributeType.CritChance,
                ECardAttributeType.Multicast,
                ECardAttributeType.BurnApplyAmount,
                ECardAttributeType.PoisonApplyAmount,
                ECardAttributeType.HasteAmount,
                ECardAttributeType.SlowAmount,
                ECardAttributeType.FreezeAmount,
            };
            var attrNames = new string[]
            {
                "Damage", "Heal", "Shield", "Cooldown", "Cooldown Max",
                "Ammo", "Max Ammo", "Crit", "Multicast",
                "Burn", "Poison", "Haste", "Slow", "Freeze"
            };

            for (int i = 0; i < attrTypes.Length; i++)
            {
                try
                {
                    var val = getAttrMethod.Invoke(template, new object[] { attrTypes[i], targetTier });
                    if (val == null) continue;
                    int value = (int)val;
                    if (value <= 0) continue;

                    if (attrTypes[i] == ECardAttributeType.Cooldown || attrTypes[i] == ECardAttributeType.CooldownMax)
                        stats.Add($"{attrNames[i]} {value / 1000f:F1}s");
                    else
                        stats.Add($"{attrNames[i]} {value}");
                }
                catch { }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetUpgradePreview error: {ex.Message}");
        }

        return stats;
    }

    /// <summary>
    /// Gets stats from a Tiers dictionary for a specific tier.
    /// </summary>
    private static List<string> GetStatsFromTiersDictionary(System.Collections.IDictionary tiersDict, ETier targetTier)
    {
        var stats = new List<string>();

        try
        {
            object targetTierData = null;
            foreach (System.Collections.DictionaryEntry entry in tiersDict)
            {
                if ((ETier)entry.Key == targetTier)
                {
                    targetTierData = entry.Value;
                    break;
                }
            }

            if (targetTierData == null) return stats;

            var attrsProp = targetTierData.GetType().GetProperty("Attributes");
            if (attrsProp == null) return stats;

            var attrs = attrsProp.GetValue(targetTierData) as System.Collections.IDictionary;
            if (attrs == null) return stats;

            var attrNameMap = new Dictionary<string, string>
            {
                {"DamageAmount", "Damage"}, {"HealAmount", "Heal"},
                {"ShieldApplyAmount", "Shield"}, {"Cooldown", "Cooldown"},
                {"CooldownMax", "Cooldown Max"}, {"Ammo", "Ammo"},
                {"AmmoMax", "Max Ammo"}, {"CritChance", "Crit"},
                {"Multicast", "Multicast"}, {"BurnApplyAmount", "Burn"},
                {"PoisonApplyAmount", "Poison"}, {"HasteAmount", "Haste"},
                {"SlowAmount", "Slow"}, {"FreezeAmount", "Freeze"},
            };

            foreach (System.Collections.DictionaryEntry entry in attrs)
            {
                int value = (int)entry.Value;
                if (value <= 0) continue;

                string attrTypeName = entry.Key.ToString();
                string displayName = attrNameMap.ContainsKey(attrTypeName) ? attrNameMap[attrTypeName] : attrTypeName;

                if (attrTypeName.Contains("Cooldown"))
                    stats.Add($"{displayName} {value / 1000f:F1}s");
                else
                    stats.Add($"{displayName} {value}");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetStatsFromTiersDictionary error: {ex.Message}");
        }

        return stats;
    }

    /// <summary>
    /// Gets a preview of what an enchantment will add to an item.
    /// </summary>
    public static List<string> GetEnchantPreview(Card card, string enchantmentName)
    {
        var effects = new List<string>();
        if (card == null) return effects;

        try
        {
            // Get the card template
            var templateProp = card.GetType().GetProperty("Template");
            if (templateProp == null) return effects;

            var template = templateProp.GetValue(card);
            if (template == null) return effects;

            // Try to find the enchantment in the template
            var enchantmentsProp = template.GetType().GetProperty("Enchantments");
            if (enchantmentsProp == null)
            {
                Plugin.Logger.LogDebug("GetEnchantPreview: Enchantments property not found");
                effects.Add($"Adds {enchantmentName} enchantment");
                return effects;
            }

            var enchantments = enchantmentsProp.GetValue(template) as System.Collections.IDictionary;
            if (enchantments == null)
            {
                effects.Add($"Adds {enchantmentName} enchantment");
                return effects;
            }

            // Find matching enchantment by name
            foreach (System.Collections.DictionaryEntry entry in enchantments)
            {
                var enchant = entry.Value;
                if (enchant == null) continue;

                // Get localization to check name
                var locProp = enchant.GetType().GetProperty("Localization");
                if (locProp != null)
                {
                    var loc = locProp.GetValue(enchant);
                    if (loc != null)
                    {
                        var titleProp = loc.GetType().GetProperty("Title");
                        if (titleProp != null)
                        {
                            var title = titleProp.GetValue(loc) as string;
                            if (title != null && title.Equals(enchantmentName, System.StringComparison.OrdinalIgnoreCase))
                            {
                                // Found the enchantment, get its attributes
                                var attrsProp = enchant.GetType().GetProperty("Attributes");
                                if (attrsProp != null)
                                {
                                    var attrs = attrsProp.GetValue(enchant) as System.Collections.IDictionary;
                                    if (attrs != null && attrs.Count > 0)
                                    {
                                        foreach (System.Collections.DictionaryEntry attr in attrs)
                                        {
                                            string attrName = attr.Key.ToString();
                                            int value = (int)attr.Value;
                                            string sign = value >= 0 ? "+" : "";
                                            effects.Add($"{attrName} {sign}{value}");
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }

            if (effects.Count == 0)
            {
                effects.Add($"Adds {enchantmentName} effects");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetEnchantPreview error: {ex.Message}");
            effects.Add($"Adds {enchantmentName} enchantment");
        }

        return effects;
    }

    /// <summary>
    /// Gets the next tier enum value.
    /// </summary>
    private static ETier GetNextTier(ETier current)
    {
        return current switch
        {
            ETier.Bronze => ETier.Silver,
            ETier.Silver => ETier.Gold,
            ETier.Gold => ETier.Diamond,
            ETier.Diamond => ETier.Legendary,
            _ => current
        };
    }

    /// <summary>
    /// Checks if the current pedestal is for enchanting.
    /// </summary>
    public static bool IsEnchantPedestal()
    {
        var info = GetCurrentPedestalInfo();
        return info.Type == PedestalType.Enchant || info.Type == PedestalType.EnchantRandom;
    }

    /// <summary>
    /// Checks if the current pedestal is for upgrading.
    /// </summary>
    public static bool IsUpgradePedestal()
    {
        var info = GetCurrentPedestalInfo();
        return info.Type == PedestalType.Upgrade;
    }

    /// <summary>
    /// Enchants or upgrades an item at the current pedestal.
    /// Automatically detects the pedestal type.
    /// </summary>
    public static bool UseCurrentPedestal(Card card)
    {
        if (card == null)
        {
            Plugin.Logger.LogWarning("UseCurrentPedestal: card is null");
            return false;
        }

        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState != ERunState.Pedestal)
        {
            TolkWrapper.Speak("Not at a pedestal");
            return false;
        }

        var pedestalInfo = GetCurrentPedestalInfo();
        string cardName = ItemReader.GetCardName(card);

        switch (pedestalInfo.Type)
        {
            case PedestalType.Upgrade:
                return UpgradeItem(card);

            case PedestalType.Enchant:
            case PedestalType.EnchantRandom:
                return EnchantItem(card, pedestalInfo);

            default:
                // Detection failed - use CommitToPedestal directly and let the game decide
                Plugin.Logger.LogWarning("UseCurrentPedestal: pedestal type unknown, committing directly");
                return CommitToPedestalDirect(card);
        }
    }

    /// <summary>
    /// Commits an item to the pedestal without knowing the type.
    /// Used as fallback when pedestal detection fails - lets the game handle the logic.
    /// </summary>
    private static bool CommitToPedestalDirect(Card card)
    {
        var state = AppState.CurrentState;
        if (state == null)
        {
            TolkWrapper.Speak("Cannot use pedestal now");
            return false;
        }

        if (!state.CanHandleOperation(StateOps.CommitToPedestal))
        {
            TolkWrapper.Speak("Cannot use this item at pedestal");
            return false;
        }

        try
        {
            string name = ItemReader.GetCardName(card);

            var controller = Data.CardAndSkillLookup?.GetCardController(card) as ItemController;
            if (controller != null)
            {
                TriggerItemDroppedOnPedestalEvent(controller);
            }

            if (Singleton<BoardManager>.Instance != null)
            {
                Singleton<BoardManager>.Instance.MarkUpgradeOrFuseOrEnchantProcessing();
            }

            state.CommitToPedestalCommand(card.InstanceId);
            TolkWrapper.Speak($"Using {name} at pedestal");
            Plugin.Logger.LogInfo($"CommitToPedestalDirect: {name}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"CommitToPedestalDirect failed: {ex.Message}");
            TolkWrapper.Speak("Pedestal action failed");
            return false;
        }
    }

    /// <summary>
    /// Enchants an item at the pedestal.
    /// </summary>
    private static bool EnchantItem(Card card, PedestalInfo pedestalInfo)
    {
        var state = AppState.CurrentState;
        if (state == null)
        {
            Plugin.Logger.LogWarning("EnchantItem: AppState.CurrentState is null");
            TolkWrapper.Speak("Cannot enchant now");
            return false;
        }

        if (!state.CanHandleOperation(StateOps.CommitToPedestal))
        {
            TolkWrapper.Speak("Cannot enchant this item");
            return false;
        }

        // Check if already enchanted
        if (card is ItemCard itemCard && itemCard.Enchantment.HasValue)
        {
            TolkWrapper.Speak("Item is already enchanted");
            return false;
        }

        try
        {
            string name = ItemReader.GetCardName(card);
            string enchantName = pedestalInfo.EnchantmentName ?? "unknown";

            // Trigger visual feedback
            var controller = Data.CardAndSkillLookup?.GetCardController(card) as ItemController;
            if (controller != null)
            {
                TriggerItemDroppedOnPedestalEvent(controller);
            }

            // Mark processing
            if (Singleton<BoardManager>.Instance != null)
            {
                Singleton<BoardManager>.Instance.MarkUpgradeOrFuseOrEnchantProcessing();
            }

            state.CommitToPedestalCommand(card.InstanceId);

            if (pedestalInfo.Type == PedestalType.EnchantRandom)
            {
                TolkWrapper.Speak($"Enchanting {name} with random enchantment");
            }
            else
            {
                TolkWrapper.Speak($"Enchanting {name} with {enchantName}");
            }

            Plugin.Logger.LogInfo($"EnchantItem: {name} with {enchantName}");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"EnchantItem failed: {ex.Message}");
            TolkWrapper.Speak("Enchantment failed");
            return false;
        }
    }
}
