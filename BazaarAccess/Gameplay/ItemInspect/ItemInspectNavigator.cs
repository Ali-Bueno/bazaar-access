using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Navigation;
using BazaarGameClient.Domain.Models.Cards;
using TMPro;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.UI.Tooltips;
using UnityEngine;

namespace BazaarAccess.Gameplay.ItemInspect;

internal sealed class ItemInspectNavigator
{
    private enum ItemInspectSection
    {
        Legend,
        Tier,
        Enchantment,
        Stats,
        Reset,
        Exit
    }

    private readonly DetailReader _detailReader = new DetailReader();

    private ItemCard _sourceItemCard;
    private CardTooltipController _visualTooltipController;
    private object _tooltipUnlockEvent;
    private bool _isPending;
    private bool _isActive;
    private bool _tooltipUnlockObserved;
    private bool _announceOnClose;
    private ItemInspectSection _currentSection = ItemInspectSection.Stats;

    public bool IsActive => _isActive;

    public bool TryEnter(Card card)
    {
        if (_isActive || _isPending || card is not ItemCard itemCard)
            return false;

        _sourceItemCard = itemCard;
        _visualTooltipController = null;
        _tooltipUnlockEvent = null;
        _tooltipUnlockObserved = false;
        _announceOnClose = false;
        _isPending = true;
        _isActive = false;
        _currentSection = ItemInspectSection.Stats;
        _detailReader.Clear();
        return true;
    }

    public void Exit(bool announce = false)
    {
        if (!_isActive && !_isPending)
            return;

        UnsubscribeTooltipEvents();
        SetItemCursorState(isOverCard: false);
        UnlockVisualPreviewIfOwned();

        _sourceItemCard = null;
        _visualTooltipController = null;
        _tooltipUnlockEvent = null;
        _tooltipUnlockObserved = false;
        _announceOnClose = false;
        _isPending = false;
        _isActive = false;
        _currentSection = ItemInspectSection.Stats;
        _detailReader.Clear();

        if (announce)
            TolkWrapper.Speak("Exited item inspect");
    }

    public IEnumerator ShowVisualPreview()
    {
        if (_sourceItemCard == null)
        {
            Exit();
            yield break;
        }

        var boardManager = BoardStashNavigator.GetBoardManager();
        if (boardManager == null)
        {
            Exit();
            yield break;
        }

        CardController controller = VisualSelector.FindCardController(_sourceItemCard, boardManager);
        if (controller == null)
        {
            Exit();
            yield break;
        }

        SetCursorOverCard(controller, true);
        SubscribeTooltipEvents();

        if (!TryShowItemTooltip(controller))
        {
            Exit();
            yield break;
        }

        float waited = 0f;
        const float step = 0.05f;
        const float maxWait = 2f;
        float lastShowRetry = 0f;
        bool lockRequested = false;

        while (waited < maxWait)
        {
            if (waited - lastShowRetry >= 0.25f)
            {
                TryShowItemTooltip(controller);
                lastShowRetry = waited;
            }

            var tooltipController = GetTooltipController();
            if (!lockRequested &&
                tooltipController != null &&
                tooltipController.CurrentTooltipData is CardTooltipData tooltipData &&
                tooltipData.CardInstance == _sourceItemCard &&
                tooltipController.HasShown)
            {
                _visualTooltipController = tooltipController;
                tooltipController.LockTooltipToggle();
                lockRequested = true;
            }

            if (IsVisualActive())
            {
                _isPending = false;
                _isActive = true;
                _currentSection = GetDefaultSection();
                _detailReader.Clear();
                TolkWrapper.Speak("Item inspect");
                ReadCurrentSection();
                yield break;
            }

            waited += step;
            yield return new WaitForSeconds(step);
        }

        Exit();
    }

    public IEnumerator MonitorVisualState()
    {
        while (_isPending || _isActive)
        {
            if (_tooltipUnlockObserved)
            {
                Exit(_announceOnClose);
                yield break;
            }

            if (_isActive && !IsVisualActive())
            {
                Exit(_announceOnClose);
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    public bool IsVisualActive()
    {
        if (_sourceItemCard == null)
            return false;

        CardTooltipController tooltipController = GetTooltipController();
        if (tooltipController == null || Data.TooltipParentComponent == null)
            return false;

        if (!Data.TooltipParentComponent.CardTooltipControllerAreCardsEqual(_sourceItemCard, tooltipController))
            return false;

        if (!IsTooltipLocked(tooltipController))
            return false;

        return IsLockVariantPanelVisible(tooltipController);
    }

    public void HandleInput(AccessibleKey key)
    {
        if (!_isActive)
            return;

        if (key == AccessibleKey.Back)
        {
            RequestClose();
            return;
        }

        EnsureCurrentSectionVisible();

        switch (key)
        {
            case AccessibleKey.Left:
                Navigate(-1);
                return;

            case AccessibleKey.Right:
                Navigate(1);
                return;

            case AccessibleKey.Up:
                HandleAdjust(-1);
                return;

            case AccessibleKey.Down:
                HandleAdjust(1);
                return;

            case AccessibleKey.Home:
                HandleJumpToBoundary(first: true);
                return;

            case AccessibleKey.End:
                HandleJumpToBoundary(first: false);
                return;

            case AccessibleKey.Confirm:
                HandleConfirm();
                return;
        }
    }

    private void Navigate(int delta)
    {
        List<ItemInspectSection> visibleSections = GetVisibleSections();
        if (visibleSections.Count == 0)
            return;

        int currentIndex = visibleSections.IndexOf(_currentSection);
        if (currentIndex < 0)
        {
            _currentSection = visibleSections[0];
            ReadCurrentSection();
            return;
        }

        int nextIndex = currentIndex + delta;
        if (nextIndex < 0)
        {
            TolkWrapper.Speak("Start of list");
            return;
        }

        if (nextIndex >= visibleSections.Count)
        {
            TolkWrapper.Speak("End of list");
            return;
        }

        _currentSection = visibleSections[nextIndex];
        _detailReader.Clear();
        ReadCurrentSection();
    }

    private void HandleAdjust(int direction)
    {
        switch (_currentSection)
        {
            case ItemInspectSection.Legend:
                SpeakLegend();
                return;

            case ItemInspectSection.Tier:
                AdjustDropdown(GetTierDropdown(), direction);
                return;

            case ItemInspectSection.Enchantment:
                AdjustDropdown(GetEnchantmentDropdown(), direction);
                return;

            case ItemInspectSection.Stats:
                SpeakStatsDetail(up: direction < 0);
                return;
        }
    }

    private void HandleConfirm()
    {
        switch (_currentSection)
        {
            case ItemInspectSection.Legend:
                SpeakLegend();
                return;

            case ItemInspectSection.Tier:
            case ItemInspectSection.Enchantment:
                ReadCurrentSection();
                return;

            case ItemInspectSection.Stats:
            {
                Card previewCard = GetPreviewCard();
                if (previewCard != null)
                    TolkWrapper.Speak(ItemReader.GetDetailedDescription(previewCard));
                return;
            }

            case ItemInspectSection.Reset:
                ClickResetButton();
                return;

            case ItemInspectSection.Exit:
                RequestClose();
                return;
        }
    }

    private void HandleJumpToBoundary(bool first)
    {
        switch (_currentSection)
        {
            case ItemInspectSection.Tier:
                SetDropdownToBoundary(GetTierDropdown(), first);
                return;

            case ItemInspectSection.Enchantment:
                SetDropdownToBoundary(GetEnchantmentDropdown(), first);
                return;
        }
    }

    private void ReadCurrentSection()
    {
        string speech = GetCurrentSectionSpeech();
        if (!string.IsNullOrWhiteSpace(speech))
            TolkWrapper.Speak(speech);
    }

    private string GetCurrentSectionSpeech()
    {
        EnsureCurrentSectionVisible();

        switch (_currentSection)
        {
            case ItemInspectSection.Legend:
                return GetLegendSpeech();

            case ItemInspectSection.Tier:
                return GetComboBoxSpeech("Tier", GetTierDropdown());

            case ItemInspectSection.Enchantment:
                return GetComboBoxSpeech("Enchantment", GetEnchantmentDropdown());

            case ItemInspectSection.Stats:
            {
                _detailReader.Clear();
                Card previewCard = GetPreviewCard();
                if (previewCard == null)
                {
                    return "No item";
                }

                return ItemReader.GetShortDescription(previewCard);
            }

            case ItemInspectSection.Reset:
                return "Reset";

            case ItemInspectSection.Exit:
                return "Exit";
        }

        return string.Empty;
    }

    private void SpeakLegend()
    {
        string speech = GetLegendSpeech();
        if (!string.IsNullOrWhiteSpace(speech))
            TolkWrapper.Speak(speech);
    }

    private string GetLegendSpeech()
    {
        string legendText = GetLegendText();
        return string.IsNullOrWhiteSpace(legendText)
            ? string.Empty
            : $"Legend. {legendText}";
    }

    private void SpeakStatsDetail(bool up)
    {
        Card previewCard = GetPreviewCard();
        if (previewCard == null)
        {
            TolkWrapper.Speak("No item");
            return;
        }

        _detailReader.Init(previewCard, ItemReader.GetDetailLines);
        if (!_detailReader.HasLines)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        string line = up ? _detailReader.LineUp() : _detailReader.LineDown();
        TolkWrapper.Speak(line ?? "No details");
    }

    private void AdjustDropdown(TMP_Dropdown dropdown, int direction)
    {
        if (dropdown == null)
            return;

        int optionCount = dropdown.options?.Count ?? 0;
        int newIndex = dropdown.value + direction;
        if (newIndex < 0 || newIndex >= optionCount)
            return;

        _detailReader.Clear();
        dropdown.value = newIndex;

        string value = GetDropdownCurrentText(dropdown);
        if (!string.IsNullOrEmpty(value))
            TolkWrapper.Speak(value);
    }

    private static void SetDropdownToBoundary(TMP_Dropdown dropdown, bool first)
    {
        if (dropdown == null)
            return;

        int optionCount = dropdown.options?.Count ?? 0;
        if (optionCount <= 0)
            return;

        int targetIndex = first ? 0 : optionCount - 1;
        if (dropdown.value == targetIndex)
            return;

        dropdown.value = targetIndex;

        string value = GetDropdownCurrentText(dropdown);
        if (!string.IsNullOrEmpty(value))
            TolkWrapper.Speak(value);
    }

    private void ClickResetButton()
    {
        ButtonCustom button = GetResetButton();
        if (button == null || !button.gameObject.activeInHierarchy)
            return;

        _detailReader.Clear();
        button.OnMouseClickCustom();
        TolkWrapper.Speak("Reset");
    }

    private void RequestClose()
    {
        if (!_isActive)
            return;

        _announceOnClose = true;
        _detailReader.Clear();

        ButtonCustom button = GetExitButton();
        if (button != null && button.gameObject.activeInHierarchy)
        {
            button.OnMouseClickCustom();
            return;
        }

        CardTooltipController tooltipController = GetTooltipController();
        tooltipController?.LockTooltipToggle();
    }

    private void EnsureCurrentSectionVisible()
    {
        List<ItemInspectSection> visibleSections = GetVisibleSections();
        if (visibleSections.Count > 0 && !visibleSections.Contains(_currentSection))
            _currentSection = visibleSections[0];
    }

    private ItemInspectSection GetDefaultSection()
    {
        List<ItemInspectSection> visibleSections = GetVisibleSections();
        return visibleSections.Count > 0 ? visibleSections[0] : ItemInspectSection.Stats;
    }

    private List<ItemInspectSection> GetVisibleSections()
    {
        var sections = new List<ItemInspectSection>();

        if (HasLegend())
            sections.Add(ItemInspectSection.Legend);
        if (IsTierVisible())
            sections.Add(ItemInspectSection.Tier);
        if (IsEnchantmentVisible())
            sections.Add(ItemInspectSection.Enchantment);

        sections.Add(ItemInspectSection.Stats);

        if (IsResetVisible())
            sections.Add(ItemInspectSection.Reset);
        if (IsExitVisible())
            sections.Add(ItemInspectSection.Exit);

        return sections;
    }

    private bool HasLegend()
    {
        CardTooltipController tooltipController = GetTooltipController();
        return tooltipController?.LegendTooltipComponent != null &&
               tooltipController.LegendTooltipComponent.HasText;
    }

    private string GetLegendText()
    {
        CardTooltipController tooltipController = GetTooltipController();
        LegendTooltipComponent legend = tooltipController?.LegendTooltipComponent;
        if (legend == null || !legend.HasText)
            return string.Empty;

        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo rowsField = typeof(LegendTooltipComponent).GetField("_spawnedLegendRows", flags);
            if (rowsField?.GetValue(legend) is not System.Collections.IEnumerable rows)
                return string.Empty;

            var parts = new List<string>();
            foreach (object rowObj in rows)
            {
                if (rowObj is not LegendTooltipRowComponent row || !row.IsVisible)
                    continue;

                string title = TextHelper.CleanText(row.TitleText?.text ?? string.Empty);
                string description = TextHelper.CleanText(row.DescriptionText?.text ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(description))
                    parts.Add($"{title}: {description}");
                else if (!string.IsNullOrWhiteSpace(description))
                    parts.Add(description);
                else if (!string.IsNullOrWhiteSpace(title))
                    parts.Add(title);
            }

            return string.Join(". ", parts);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"GetLegendText error: {ex.Message}");
            return string.Empty;
        }
    }

    private bool IsTierVisible()
        => GetTierGroupObject()?.activeInHierarchy == true;

    private bool IsEnchantmentVisible()
        => GetEnchantmentGroupObject()?.activeInHierarchy == true;

    private bool IsResetVisible()
        => GetResetButton()?.gameObject.activeInHierarchy == true;

    private bool IsExitVisible()
        => GetExitButton()?.gameObject.activeInHierarchy == true;

    private Card GetPreviewCard()
    {
        CardTooltipController tooltipController = GetTooltipController();
        if (tooltipController?.CurrentTooltipData is CardTooltipData tooltipData)
            return tooltipData.CardInstance;

        return _sourceItemCard;
    }

    private CardTooltipController GetTooltipController()
    {
        if (_visualTooltipController != null)
            return _visualTooltipController;

        if (_sourceItemCard == null || Data.TooltipParentComponent == null)
            return null;

        return Data.TooltipParentComponent.GetCardTooltipController(_sourceItemCard);
    }

    private bool TryShowItemTooltip(CardController controller)
    {
        try
        {
            if (controller == null || Data.TooltipParentComponent == null)
                return false;

            ITooltipData tooltipData = controller.GetTooltipData();
            if (tooltipData == null)
                return false;

            Data.TooltipParentComponent.ShowCardTooltipController(
                controller.transform,
                GetTooltipOffset(controller),
                tooltipData);

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"TryShowItemTooltip error: {ex.Message}");
            return false;
        }
    }

    private void UnlockVisualPreviewIfOwned()
    {
        try
        {
            CardTooltipController tooltipController = GetTooltipController();
            if (tooltipController != null &&
                Data.TooltipParentComponent != null &&
                Data.TooltipParentComponent.CardTooltipControllerAreCardsEqual(_sourceItemCard, tooltipController))
            {
                if (IsTooltipLocked(tooltipController))
                    tooltipController.Unlock();
                else
                    tooltipController.StartTooltipFadeOut();
            }

            Data.TooltipParentComponent?.HideSecondaryCardTooltipController();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"UnlockVisualPreviewIfOwned error: {ex.Message}");
        }
    }

    private bool IsLockVariantPanelVisible(CardTooltipController tooltipController)
    {
        CardLockVariantPanel panel = GetLockVariantPanel(tooltipController);
        if (panel == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo panelRootField = typeof(CardLockVariantPanel).GetField("_panelRoot", flags);
        return panelRootField?.GetValue(panel) is RectTransform panelRoot &&
               panelRoot.gameObject.activeInHierarchy;
    }

    private CardLockVariantPanel GetLockVariantPanel(CardTooltipController tooltipController)
    {
        if (tooltipController?.LockModeContainer == null)
            return null;

        return tooltipController.LockModeContainer.GetComponent<CardLockVariantPanel>();
    }

    private GameObject GetTierGroupObject()
    {
        CardLockVariantPanel panel = GetLockVariantPanel(GetTooltipController());
        if (panel == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardLockVariantPanel).GetField("_tierGroupObject", flags);
        return field?.GetValue(panel) as GameObject;
    }

    private GameObject GetEnchantmentGroupObject()
    {
        CardLockVariantPanel panel = GetLockVariantPanel(GetTooltipController());
        if (panel == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardLockVariantPanel).GetField("_enchantmentGroupObject", flags);
        return field?.GetValue(panel) as GameObject;
    }

    private TMP_Dropdown GetTierDropdown()
    {
        CardLockVariantPanel panel = GetLockVariantPanel(GetTooltipController());
        if (panel == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardLockVariantPanel).GetField("_tierDropdown", flags);
        return field?.GetValue(panel) as TMP_Dropdown;
    }

    private TMP_Dropdown GetEnchantmentDropdown()
    {
        CardLockVariantPanel panel = GetLockVariantPanel(GetTooltipController());
        if (panel == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardLockVariantPanel).GetField("_enchantmentDropdown", flags);
        return field?.GetValue(panel) as TMP_Dropdown;
    }

    private ButtonCustom GetResetButton()
    {
        CardLockVariantPanel panel = GetLockVariantPanel(GetTooltipController());
        if (panel == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardLockVariantPanel).GetField("_resetButton", flags);
        return field?.GetValue(panel) as ButtonCustom;
    }

    private ButtonCustom GetExitButton()
    {
        CardTooltipController tooltipController = GetTooltipController();
        if (tooltipController == null)
            return null;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo field = typeof(CardTooltipController).GetField("lockModeExitButton", flags);
        return field?.GetValue(tooltipController) as ButtonCustom;
    }

    private static string GetDropdownCurrentText(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return string.Empty;

        string caption = TextHelper.CleanText(dropdown.captionText?.text ?? string.Empty);
        if (!string.IsNullOrEmpty(caption))
            return caption;

        int index = dropdown.value;
        if (dropdown.options != null && index >= 0 && index < dropdown.options.Count)
            return TextHelper.CleanText(dropdown.options[index]?.text ?? string.Empty);

        return string.Empty;
    }

    private static string GetComboBoxSpeech(string label, TMP_Dropdown dropdown)
    {
        string value = GetDropdownCurrentText(dropdown);
        return $"{label} combo box: {value}. Use home, end, up or down arrows to change.";
    }

    private void SubscribeTooltipEvents()
    {
        try
        {
            object tooltipUnlockEvent = GetTooltipUnlockEvent();
            if (tooltipUnlockEvent == null)
                return;

            _tooltipUnlockEvent = tooltipUnlockEvent;

            MethodInfo removeListener = tooltipUnlockEvent.GetType().GetMethod(
                "RemoveListener",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Action) },
                null);

            MethodInfo addListener = tooltipUnlockEvent.GetType().GetMethod(
                "AddListener",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Action), typeof(MonoBehaviour) },
                null);

            removeListener?.Invoke(tooltipUnlockEvent, new object[] { (Action)OnTooltipUnlock });
            addListener?.Invoke(tooltipUnlockEvent, new object[] { (Action)OnTooltipUnlock, null });
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ItemInspect SubscribeTooltipEvents error: {ex.Message}");
        }
    }

    private void UnsubscribeTooltipEvents()
    {
        try
        {
            object tooltipUnlockEvent = _tooltipUnlockEvent ?? GetTooltipUnlockEvent();
            if (tooltipUnlockEvent == null)
                return;

            MethodInfo removeListener = tooltipUnlockEvent.GetType().GetMethod(
                "RemoveListener",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Action) },
                null);

            removeListener?.Invoke(tooltipUnlockEvent, new object[] { (Action)OnTooltipUnlock });
            _tooltipUnlockEvent = null;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ItemInspect UnsubscribeTooltipEvents error: {ex.Message}");
        }
    }

    private void OnTooltipUnlock()
    {
        if (_isPending || _isActive)
            _tooltipUnlockObserved = true;
    }

    private static object GetTooltipUnlockEvent()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type eventsType = typeof(Data).Assembly.GetType("TheBazaar.Events");
        FieldInfo field = eventsType?.GetField("TooltipUnlock", flags);
        return field?.GetValue(null);
    }

    private void SetItemCursorState(bool isOverCard)
    {
        try
        {
            BoardManager boardManager = BoardStashNavigator.GetBoardManager();
            if (boardManager == null || _sourceItemCard == null)
                return;

            CardController controller = VisualSelector.FindCardController(_sourceItemCard, boardManager);
            if (controller != null)
                SetCursorOverCard(controller, isOverCard);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"SetItemCursorState error: {ex.Message}");
        }
    }

    private static void SetCursorOverCard(CardController controller, bool isOverCard)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            PropertyInfo property = typeof(CardController).GetProperty("IsCursorOverCard", flags);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            setter?.Invoke(controller, new object[] { isOverCard });
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ItemInspect SetCursorOverCard error: {ex.Message}");
        }
    }

    private static Vector3 GetTooltipOffset(CardController controller)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            PropertyInfo property = typeof(CardController).GetProperty("TooltipOffset", flags);
            if (property?.GetValue(controller) is Vector3 offset)
                return offset;

            FieldInfo field = typeof(CardController).GetField("tooltipOffset", flags);
            if (field?.GetValue(controller) is Vector3 fieldOffset)
                return fieldOffset;
        }
        catch
        {
            // Fall through to zero offset fallback.
        }

        return Vector3.zero;
    }

    private static bool IsTooltipLocked(CardTooltipController tooltipController)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            PropertyInfo property = typeof(CardTooltipController).GetProperty("IsLocked", flags);
            if (property?.GetValue(tooltipController) is bool propertyValue)
                return propertyValue;

            FieldInfo field = typeof(BaseTooltipController).GetField("isLocked", flags);
            if (field?.GetValue(tooltipController) is bool fieldValue)
                return fieldValue;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"ItemInspect IsTooltipLocked reflection error: {ex.Message}");
        }

        return Data.TooltipParentComponent != null &&
               Data.TooltipParentComponent.IsCardTooltipControllerLocked(tooltipController);
    }
}
