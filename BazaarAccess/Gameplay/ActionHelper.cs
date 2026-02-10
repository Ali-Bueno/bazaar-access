using System.Collections.Generic;
using System.Reflection;
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
        if (card.Tier == ETier.Diamond || card.Tier == ETier.Legendary)
        {
            TolkWrapper.Speak("Item is already at maximum tier");
            return false;
        }

        try
        {
            string name = ItemReader.GetCardName(card);
            string currentTier = ItemReader.GetTierName(card);
            string nextTier = GetNextTierName(card.Tier);

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

            TolkWrapper.Speak($"Upgrading {name} from {currentTier} to {nextTier}");

            Plugin.Logger.LogInfo($"UpgradeItem: {name} ({currentTier} -> {nextTier})");
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
    private static string GetNextTierName(ETier currentTier)
    {
        switch (currentTier)
        {
            case ETier.Bronze: return "Silver";
            case ETier.Silver: return "Gold";
            case ETier.Gold: return "Diamond";
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

    /// <summary>
    /// Gets information about the current pedestal/altar.
    /// Uses reflection to access game's internal pedestal data.
    /// </summary>
    public static PedestalInfo GetCurrentPedestalInfo()
    {
        var info = new PedestalInfo();

        var currentState = StateChangePatch.GetCurrentRunState();
        if (currentState != ERunState.Pedestal)
        {
            return info;
        }

        try
        {
            // Get the current encounter ID
            var currentEncounterIdProp = typeof(Data).GetProperty("CurrentEncounterId",
                BindingFlags.Public | BindingFlags.Static);
            if (currentEncounterIdProp == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: CurrentEncounterId property not found");
                return info;
            }

            var encounterId = currentEncounterIdProp.GetValue(null) as System.Guid?;
            if (!encounterId.HasValue || encounterId.Value == System.Guid.Empty)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: No current encounter ID");
                return info;
            }

            // Get static data to retrieve the pedestal template
            var getStaticMethod = typeof(Data).GetMethod("GetStatic",
                BindingFlags.Public | BindingFlags.Static);
            if (getStaticMethod == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: GetStatic method not found");
                return info;
            }

            // GetStatic returns a Task, we need to get the result
            var staticDataTask = getStaticMethod.Invoke(null, null);
            if (staticDataTask == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: GetStatic returned null");
                return info;
            }

            // Get the Result property from the Task
            var resultProp = staticDataTask.GetType().GetProperty("Result");
            if (resultProp == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Task.Result not found");
                return info;
            }

            var staticData = resultProp.GetValue(staticDataTask);
            if (staticData == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Static data is null");
                return info;
            }

            // Get the card by ID
            var getCardMethod = staticData.GetType().GetMethod("GetCardById");
            if (getCardMethod == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: GetCardById method not found");
                return info;
            }

            var cardTemplate = getCardMethod.Invoke(staticData, new object[] { encounterId.Value });
            if (cardTemplate == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Card template is null");
                return info;
            }

            // Check if it's a pedestal encounter
            var pedestalType = typeof(Data).Assembly.GetType("BazaarGameShared.Domain.Cards.Encounter.Pedestal.TCardEncounterPedestal");
            if (pedestalType == null || !pedestalType.IsInstanceOfType(cardTemplate))
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Not a pedestal encounter");
                return info;
            }

            // Get the Behavior property
            var behaviorProp = pedestalType.GetProperty("Behavior");
            if (behaviorProp == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Behavior property not found");
                return info;
            }

            var behavior = behaviorProp.GetValue(cardTemplate);
            if (behavior == null)
            {
                Plugin.Logger.LogDebug("GetCurrentPedestalInfo: Behavior is null");
                return info;
            }

            // Check behavior type
            var behaviorTypeName = behavior.GetType().Name;
            Plugin.Logger.LogDebug($"GetCurrentPedestalInfo: Behavior type = {behaviorTypeName}");

            if (behaviorTypeName.Contains("Upgrade"))
            {
                info.Type = PedestalType.Upgrade;

                // Try to get TargetTier
                var targetTierProp = behavior.GetType().GetProperty("TargetTier");
                if (targetTierProp != null)
                {
                    var tierValue = targetTierProp.GetValue(behavior);
                    if (tierValue != null)
                    {
                        info.TargetTier = (ETier)tierValue;
                    }
                }
            }
            else if (behaviorTypeName.Contains("EnchantRandom"))
            {
                info.Type = PedestalType.EnchantRandom;
                info.EnchantmentName = "Random";
            }
            else if (behaviorTypeName.Contains("Enchant"))
            {
                info.Type = PedestalType.Enchant;

                // Get the specific enchantment type
                var enchantProp = behavior.GetType().GetProperty("Enchantment");
                if (enchantProp != null)
                {
                    var enchantValue = enchantProp.GetValue(behavior);
                    if (enchantValue != null)
                    {
                        info.EnchantmentName = enchantValue.ToString();
                    }
                }
            }

            Plugin.Logger.LogInfo($"GetCurrentPedestalInfo: Type={info.Type}, Enchant={info.EnchantmentName}, TargetTier={info.TargetTier}");
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetCurrentPedestalInfo error: {ex.Message}");
        }

        return info;
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
                    nextTier = pedestalInfo.TargetTier.Value.ToString();
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
    /// Gets a preview of what stats will change when upgrading an item.
    /// Returns a list of changes like "Damage 10 → 15"
    /// </summary>
    public static List<string> GetUpgradePreview(Card card)
    {
        var changes = new List<string>();
        if (card == null) return changes;

        try
        {
            var pedestalInfo = GetCurrentPedestalInfo();
            ETier targetTier = pedestalInfo.TargetTier ?? GetNextTier(card.Tier);

            if (targetTier == card.Tier)
            {
                changes.Add("Stats will improve (same tier)");
                return changes;
            }

            // Get the card template to access tier data
            var templateProp = card.GetType().GetProperty("Template");
            if (templateProp == null)
            {
                Plugin.Logger.LogWarning("GetUpgradePreview: Template property not found");
                return changes;
            }

            var template = templateProp.GetValue(card);
            if (template == null)
            {
                Plugin.Logger.LogWarning("GetUpgradePreview: Template is null");
                return changes;
            }

            Plugin.Logger.LogInfo($"GetUpgradePreview: Template type = {template.GetType().FullName}");

            // Try to get attribute values at current and target tier
            // The method might be on the concrete type or an interface
            var getAttrMethod = template.GetType().GetMethod("GetAttributeBaseValueAtTier",
                BindingFlags.Public | BindingFlags.Instance);

            if (getAttrMethod == null)
            {
                // Try finding it in interfaces
                foreach (var iface in template.GetType().GetInterfaces())
                {
                    getAttrMethod = iface.GetMethod("GetAttributeBaseValueAtTier");
                    if (getAttrMethod != null)
                    {
                        Plugin.Logger.LogInfo($"GetUpgradePreview: Found method in interface {iface.Name}");
                        break;
                    }
                }
            }

            if (getAttrMethod == null)
            {
                Plugin.Logger.LogWarning($"GetUpgradePreview: GetAttributeBaseValueAtTier not found on {template.GetType().Name}");

                // Try alternative approach: access Tiers dictionary directly
                var tiersProp = template.GetType().GetProperty("Tiers", BindingFlags.Public | BindingFlags.Instance);
                if (tiersProp != null)
                {
                    Plugin.Logger.LogInfo("GetUpgradePreview: Found Tiers property, using direct access");
                    var tiersDict = tiersProp.GetValue(template) as System.Collections.IDictionary;
                    if (tiersDict != null)
                    {
                        return GetChangesFromTiersDictionary(tiersDict, card.Tier, targetTier);
                    }
                }

                // List available properties for debugging
                var props = template.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    Plugin.Logger.LogDebug($"  Available property: {p.Name} ({p.PropertyType.Name})");
                }
                return changes;
            }

            // Check common combat stats
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
                "Damage", "Heal", "Shield", "Cooldown", "Cooldown",
                "Ammo", "Max Ammo", "Crit", "Multicast",
                "Burn", "Poison", "Haste", "Slow", "Freeze"
            };

            for (int i = 0; i < attrTypes.Length; i++)
            {
                var attrType = attrTypes[i];
                var attrName = attrNames[i];
                try
                {
                    var currentVal = getAttrMethod.Invoke(template, new object[] { attrType, card.Tier });
                    var nextVal = getAttrMethod.Invoke(template, new object[] { attrType, targetTier });

                    if (currentVal != null && nextVal != null)
                    {
                        int current = (int)currentVal;
                        int next = (int)nextVal;

                        if (current != next && (current > 0 || next > 0))
                        {
                            // Format cooldown as seconds
                            if (attrType == ECardAttributeType.Cooldown || attrType == ECardAttributeType.CooldownMax)
                            {
                                float currentSec = current / 1000f;
                                float nextSec = next / 1000f;
                                changes.Add(string.Format("{0} {1:F1}s to {2:F1}s", attrName, currentSec, nextSec));
                            }
                            else
                            {
                                changes.Add(string.Format("{0} {1} to {2}", attrName, current, next));
                            }
                        }
                    }
                }
                catch
                {
                    // Attribute not available at this tier, skip
                }
            }

            if (changes.Count == 0)
            {
                changes.Add("Stats will improve");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetUpgradePreview error: {ex.Message}");
            changes.Add("Preview not available");
        }

        return changes;
    }

    /// <summary>
    /// Gets stat changes by directly accessing the Tiers dictionary.
    /// Fallback method when GetAttributeBaseValueAtTier is not accessible.
    /// </summary>
    private static List<string> GetChangesFromTiersDictionary(System.Collections.IDictionary tiersDict, ETier currentTier, ETier targetTier)
    {
        var changes = new List<string>();

        try
        {
            // Get the TCardTier objects for current and target tiers
            object currentTierData = null;
            object targetTierData = null;

            foreach (System.Collections.DictionaryEntry entry in tiersDict)
            {
                var tierKey = (ETier)entry.Key;
                if (tierKey == currentTier || (tierKey == ETier.Diamond && currentTier == ETier.Legendary))
                {
                    currentTierData = entry.Value;
                }
                if (tierKey == targetTier || (tierKey == ETier.Diamond && targetTier == ETier.Legendary))
                {
                    targetTierData = entry.Value;
                }
            }

            if (currentTierData == null || targetTierData == null)
            {
                Plugin.Logger.LogWarning("GetChangesFromTiersDictionary: Could not find tier data");
                return changes;
            }

            // Get Attributes dictionary from each tier
            var attrsProp = currentTierData.GetType().GetProperty("Attributes");
            if (attrsProp == null)
            {
                Plugin.Logger.LogWarning("GetChangesFromTiersDictionary: Attributes property not found");
                return changes;
            }

            var currentAttrs = attrsProp.GetValue(currentTierData) as System.Collections.IDictionary;
            var targetAttrs = attrsProp.GetValue(targetTierData) as System.Collections.IDictionary;

            if (currentAttrs == null || targetAttrs == null)
            {
                return changes;
            }

            // Compare attributes
            var attrNameMap = new System.Collections.Generic.Dictionary<string, string>
            {
                {"DamageAmount", "Damage"},
                {"HealAmount", "Heal"},
                {"ShieldApplyAmount", "Shield"},
                {"Cooldown", "Cooldown"},
                {"CooldownMax", "Cooldown"},
                {"Ammo", "Ammo"},
                {"AmmoMax", "Max Ammo"},
                {"CritChance", "Crit"},
                {"Multicast", "Multicast"},
                {"BurnApplyAmount", "Burn"},
                {"PoisonApplyAmount", "Poison"},
                {"HasteAmount", "Haste"},
                {"SlowAmount", "Slow"},
                {"FreezeAmount", "Freeze"},
            };

            foreach (System.Collections.DictionaryEntry entry in targetAttrs)
            {
                var attrType = entry.Key;
                int targetVal = (int)entry.Value;
                int currentVal = 0;

                if (currentAttrs.Contains(attrType))
                {
                    currentVal = (int)currentAttrs[attrType];
                }

                if (currentVal != targetVal)
                {
                    string attrTypeName = attrType.ToString();
                    string displayName = attrNameMap.ContainsKey(attrTypeName) ? attrNameMap[attrTypeName] : attrTypeName;

                    // Format cooldown as seconds
                    if (attrTypeName.Contains("Cooldown"))
                    {
                        float currentSec = currentVal / 1000f;
                        float targetSec = targetVal / 1000f;
                        changes.Add(string.Format("{0} {1:F1}s to {2:F1}s", displayName, currentSec, targetSec));
                    }
                    else
                    {
                        changes.Add(string.Format("{0} {1} to {2}", displayName, currentVal, targetVal));
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GetChangesFromTiersDictionary error: {ex.Message}");
        }

        return changes;
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
                // Fallback to upgrade behavior
                return UpgradeItem(card);
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
