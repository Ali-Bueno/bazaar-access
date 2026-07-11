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
using BazaarGameShared.Domain.Prerequisites.Conditionals;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.Utilities;

namespace BazaarAccess.Gameplay;

/// <summary>
/// Handles all pedestal/altar detection, caching, and actions (upgrade, enchant).
/// </summary>
public static class PedestalManager
{
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
                nextTier = TierHelper.GetName(pedestalInfo.TargetTier.Value);
            }
            else if (pedestalInfo.TargetTier.HasValue && pedestalInfo.TargetTier.Value == card.Tier)
            {
                nextTier = null; // stats-only upgrade
            }
            else
            {
                nextTier = TierHelper.GetNextName(card.Tier);
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
    /// Detects pedestal type using three strategies:
    /// 0. EncounterController (runtime encounter card instance)
    /// 1. Public Data API (Data.GetStatic().GetCardById(CurrentEncounterId))
    /// 2. Fallback: reflection on PedestalState._pedestalTemplate from AppState.CurrentState
    /// Each result is cross-validated against SelectionCriteria to catch default Behavior values.
    /// </summary>
    private static PedestalInfo DetectPedestalInfo()
    {
        var info = new PedestalInfo();

        // Strategy 0: EncounterController (runtime card instance - most reliable)
        info = DetectViaEncounterController();
        if (info.Type != PedestalType.None)
        {
            Plugin.Logger.LogInfo($"DetectPedestalInfo: EncounterController succeeded - Type={info.Type}");
            return info;
        }

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

        Plugin.Logger.LogWarning("DetectPedestalInfo: All detection strategies failed");
        return info;
    }

    /// <summary>
    /// Detects pedestal type via the runtime EncounterController (same path the game's UI uses).
    /// Data.CurrentEncounterController.CardData.Template gives the instantiated encounter card.
    /// </summary>
    private static PedestalInfo DetectViaEncounterController()
    {
        var info = new PedestalInfo();

        try
        {
            var controller = Data.CurrentEncounterController;
            if (controller == null)
            {
                Plugin.Logger.LogInfo("DetectViaEncounterController: CurrentEncounterController is null");
                return info;
            }

            var cardData = controller.CardData;
            if (cardData == null)
            {
                Plugin.Logger.LogInfo("DetectViaEncounterController: CardData is null");
                return info;
            }

            var template = cardData.Template;
            if (template == null)
            {
                Plugin.Logger.LogInfo("DetectViaEncounterController: Template is null");
                return info;
            }

            Plugin.Logger.LogInfo($"DetectViaEncounterController: Template type={template.GetType().FullName}");

            var pedestal = template as TCardEncounterPedestal;
            if (pedestal == null)
            {
                Plugin.Logger.LogWarning($"DetectViaEncounterController: Template is {template.GetType().Name}, not TCardEncounterPedestal");
                return info;
            }

            var behavior = pedestal.Behavior;
            Plugin.Logger.LogInfo($"DetectViaEncounterController: Behavior type={behavior?.GetType().FullName ?? "null"}");
            ExtractBehaviorInfo(behavior, info);

            // Cross-validate: SelectionCriteria can reveal the true pedestal type
            // when Behavior defaults to TPedestalBehaviorUpgrade
            CrossValidateWithSelectionCriteria(pedestal, info);

            Plugin.Logger.LogInfo($"DetectViaEncounterController: result Type={info.Type}, Enchant={info.EnchantmentName}");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"DetectViaEncounterController error: {ex.Message}\n{ex.StackTrace}");
        }

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
            Plugin.Logger.LogInfo($"DetectViaDataApi: CurrentEncounterId={encounterId?.ToString() ?? "null"}");
            if (encounterId == null || encounterId.Value == Guid.Empty)
            {
                Plugin.Logger.LogWarning("DetectViaDataApi: CurrentEncounterId is null/empty");
                return info;
            }

            var staticData = Data.GetStatic();
            if (staticData == null)
            {
                Plugin.Logger.LogWarning("DetectViaDataApi: static data manager is null");
                return info;
            }

            var encounterCard = staticData.GetCardById(encounterId.Value);
            if (encounterCard == null)
            {
                Plugin.Logger.LogWarning($"DetectViaDataApi: encounter card not found for id {encounterId.Value}");
                return info;
            }

            Plugin.Logger.LogInfo($"DetectViaDataApi: encounter card type={encounterCard.GetType().FullName}");

            var pedestal = encounterCard as TCardEncounterPedestal;
            if (pedestal == null)
            {
                Plugin.Logger.LogWarning($"DetectViaDataApi: encounter card is {encounterCard.GetType().Name}, not TCardEncounterPedestal");
                return info;
            }

            var behavior = pedestal.Behavior;
            Plugin.Logger.LogInfo($"DetectViaDataApi: Behavior type={behavior?.GetType().FullName ?? "null"}");
            ExtractBehaviorInfo(behavior, info);

            // Cross-validate against SelectionCriteria
            CrossValidateWithSelectionCriteria(pedestal, info);

            Plugin.Logger.LogInfo($"DetectViaDataApi: result Type={info.Type}, Enchant={info.EnchantmentName}");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"DetectViaDataApi error: {ex.Message}\n{ex.StackTrace}");
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

            // Cross-validate against SelectionCriteria
            CrossValidateWithSelectionCriteria(pedestal, info);
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

        Plugin.Logger.LogInfo($"ExtractBehaviorInfo: runtime type={behavior.GetType().FullName}, " +
            $"is Upgrade={behavior is TPedestalBehaviorUpgrade}, " +
            $"is Enchant={behavior is TPedestalBehaviorEnchant}, " +
            $"is EnchantRandom={behavior is TPedestalBehaviorEnchantRandom}");

        if (behavior is TPedestalBehaviorEnchant enchantBehavior)
        {
            info.Type = PedestalType.Enchant;
            info.EnchantmentName = enchantBehavior.Enchantment.ToString();
        }
        else if (behavior is TPedestalBehaviorEnchantRandom)
        {
            info.Type = PedestalType.EnchantRandom;
            info.EnchantmentName = "Random";
        }
        else if (behavior is TPedestalBehaviorUpgrade upgradeBehavior)
        {
            info.Type = PedestalType.Upgrade;
            info.TargetTier = upgradeBehavior.TargetTier;
        }
        else
        {
            // Unknown behavior type - try to detect via type name as last resort
            string typeName = behavior.GetType().Name;
            Plugin.Logger.LogWarning($"ExtractBehaviorInfo: Unknown behavior type: {typeName}");
            if (typeName.Contains("Enchant"))
            {
                info.Type = PedestalType.EnchantRandom;
                info.EnchantmentName = "unknown";
            }
            else if (typeName.Contains("Upgrade"))
            {
                info.Type = PedestalType.Upgrade;
            }
        }
    }

    /// <summary>
    /// Cross-validates the detected pedestal type against SelectionCriteria.
    /// The game uses TCardConditionalEnchantmentEligible for enchant pedestals and
    /// TCardConditionalTier for upgrade pedestals. If Behavior defaulted to
    /// TPedestalBehaviorUpgrade but SelectionCriteria says enchant, we override.
    /// SelectionCriteria can be wrapped in TCardConditionalAnd, so we search recursively.
    /// </summary>
    private static void CrossValidateWithSelectionCriteria(TCardEncounterPedestal pedestal, PedestalInfo info)
    {
        if (pedestal.SelectionCriteria == null) return;

        var criteriaType = pedestal.SelectionCriteria.GetType();
        Plugin.Logger.LogInfo($"CrossValidate: SelectionCriteria type={criteriaType.Name}, detected Type={info.Type}");

        // Only override if Behavior says Upgrade - enchant detection is already correct
        if (info.Type != PedestalType.Upgrade) return;

        // Search for TCardConditionalEnchantmentEligible recursively through composite conditionals
        var enchantCriteria = FindEnchantCriteria(pedestal.SelectionCriteria);
        if (enchantCriteria != null)
        {
            Plugin.Logger.LogWarning($"CrossValidate: OVERRIDE Upgrade -> Enchant (found TCardConditionalEnchantmentEligible in SelectionCriteria)");
            info.Type = PedestalType.Enchant;
            try
            {
                info.EnchantmentName = enchantCriteria.Enchantment.ToString();
                Plugin.Logger.LogInfo($"CrossValidate: Enchantment from criteria = {info.EnchantmentName}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"CrossValidate: Could not read enchantment from criteria: {ex.Message}");
                info.EnchantmentName = "unknown";
            }
        }
    }

    /// <summary>
    /// Recursively searches through composite conditionals (And/Or) to find a
    /// TCardConditionalEnchantmentEligible instance.
    /// </summary>
    private static TCardConditionalEnchantmentEligible FindEnchantCriteria(ITCardConditional conditional)
    {
        if (conditional is TCardConditionalEnchantmentEligible enchant)
            return enchant;

        if (conditional is TCardConditionalAnd andCond)
        {
            foreach (var child in andCond.Conditions)
            {
                var result = FindEnchantCriteria(child);
                if (result != null) return result;
            }
        }

        if (conditional is TCardConditionalOr orCond)
        {
            foreach (var child in orCond.Conditions)
            {
                var result = FindEnchantCriteria(child);
                if (result != null) return result;
            }
        }

        return null;
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
                string nextTier = TierHelper.GetNextName(card.Tier);
                if (pedestalInfo.TargetTier.HasValue)
                {
                    nextTier = TierHelper.GetName(pedestalInfo.TargetTier.Value);
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
