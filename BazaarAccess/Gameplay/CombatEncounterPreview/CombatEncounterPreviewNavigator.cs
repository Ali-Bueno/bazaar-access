using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Navigation;
using BazaarGameClient.Domain.Models.Cards;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TheBazaar;
using TheBazaar.Tooltips;
using TheBazaar.UI;
using TheBazaar.UI.Tooltips;

namespace BazaarAccess.Gameplay.CombatEncounterPreview;

internal sealed class CombatEncounterPreviewNavigator
{
    private enum CombatEncounterPreviewSection
    {
        Stats,
        Skills,
        Board
    }

    private CombatEncounterPreviewModel _model;
    private Card _sourceEncounterCard;
    private CardTooltipController _visualTooltipController;
    private bool _visualPending;
    private bool _tooltipUnlockObserved;
    private object _tooltipUnlockEvent;
    private CombatEncounterPreviewSection _section = CombatEncounterPreviewSection.Stats;
    private int _skillIndex;
    private int _itemIndex;
    private readonly DetailReader _detailReader = new DetailReader();

    public bool IsActive => _model != null;

    public bool TryEnter(Card card)
    {
        var model = CombatEncounterPreviewFactory.Create(card);
        if (model == null)
        {
            TolkWrapper.Speak("Nothing to inspect");
            return false;
        }

        _model = model;
        _sourceEncounterCard = card;
        _visualPending = true;
        _tooltipUnlockObserved = false;
        _section = CombatEncounterPreviewSection.Stats;
        _skillIndex = 0;
        _itemIndex = 0;
        _detailReader.Clear();

        TolkWrapper.Speak($"{_model.EnemyName} loadout");
        return true;
    }

    public void Exit(bool announce = true)
    {
        if (!IsActive) return;

        UnsubscribeTooltipEvents();
        HideCurrentPreviewHover();
        SetEncounterCursorState(isOverCard: false);
        UnlockVisualPreview();

        _model = null;
        _sourceEncounterCard = null;
        _visualTooltipController = null;
        _visualPending = false;
        _tooltipUnlockObserved = false;
        _tooltipUnlockEvent = null;
        _section = CombatEncounterPreviewSection.Stats;
        _skillIndex = 0;
        _itemIndex = 0;
        _detailReader.Clear();

        if (announce)
            TolkWrapper.Speak("Exited preview");
    }

    public IEnumerator ShowVisualPreview()
    {
        if (_sourceEncounterCard == null)
        {
            _visualPending = false;
            yield break;
        }

        var boardManager = BoardStashNavigator.GetBoardManager();
        if (boardManager == null)
        {
            _visualPending = false;
            yield break;
        }

        var controller = VisualSelector.FindCardController(_sourceEncounterCard, boardManager);
        if (controller is not EncounterController encounterController)
        {
            _visualPending = false;
            yield break;
        }

        SetCursorOverCard(encounterController, true);
        SubscribeTooltipEvents();

        if (!TryShowEncounterTooltip(encounterController))
        {
            SetCursorOverCard(encounterController, false);
            _visualPending = false;
            Exit(announce: false);
            yield break;
        }

        float waited = 0f;
        const float step = 0.05f;
        const float maxWait = 2.0f;
        float lastShowRetry = 0f;
        bool lockRequested = false;

        while (waited < maxWait)
        {
            if (waited - lastShowRetry >= 0.25f)
            {
                TryShowEncounterTooltip(encounterController);
                lastShowRetry = waited;
            }

            var tooltipController = GetTooltipController();
            if (!lockRequested &&
                tooltipController != null &&
                tooltipController.CurrentTooltipData is CardTooltipData tooltipData &&
                tooltipData.CardInstance == _sourceEncounterCard &&
                tooltipController.HasShown)
            {
                _visualTooltipController = tooltipController;
                tooltipController.Lock();
                lockRequested = true;
            }

            waited += step;
            yield return new UnityEngine.WaitForSeconds(step);
        }

        waited = 0f;
        while (waited < maxWait)
        {
            if (IsVisualActive())
            {
                _visualPending = false;
                SyncVisualFocus();
                yield break;
            }

            waited += step;
            yield return new UnityEngine.WaitForSeconds(step);
        }

        _visualPending = false;
        Exit(announce: false);
    }

    public IEnumerator MonitorVisualState()
    {
        while (IsActive)
        {
            if (_tooltipUnlockObserved)
            {
                Exit(announce: false);
                yield break;
            }

            if (!_visualPending && !IsVisualActive())
            {
                Exit(announce: false);
                yield break;
            }

            yield return new UnityEngine.WaitForSeconds(0.1f);
        }
    }

    public bool IsVisualActive()
    {
        if (_visualPending)
            return true;

        if (!IsActive || _sourceEncounterCard == null)
            return false;

        var tooltipController = GetTooltipController();
        if (tooltipController == null)
            return false;

        if (!IsTooltipLocked(tooltipController))
            return false;

        if (tooltipController.CurrentTooltipData is not CardTooltipData tooltipData)
            return false;

        return tooltipData.CardInstance == _sourceEncounterCard;
    }

    public void HandleInput(AccessibleKey key)
    {
        if (!IsActive) return;

        switch (key)
        {
            case AccessibleKey.Back:
                Exit();
                return;

            case AccessibleKey.GoToEnemy:
                _section = CombatEncounterPreviewSection.Stats;
                _detailReader.Clear();
                AnnounceStatsSection();
                return;

            case AccessibleKey.GoToStash:
                _section = CombatEncounterPreviewSection.Board;
                _detailReader.Clear();
                AnnounceBoardSection();
                return;
        }

        switch (_section)
        {
            case CombatEncounterPreviewSection.Stats:
                HandleStatsInput(key);
                break;

            case CombatEncounterPreviewSection.Skills:
                HandleSkillsInput(key);
                break;

            case CombatEncounterPreviewSection.Board:
                HandleBoardInput(key);
                break;
        }
    }

    private void HandleStatsInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Left:
                AnnounceStats();
                SyncVisualFocus();
                return;

            case AccessibleKey.Up:
                TolkWrapper.Speak("Start of list");
                SyncVisualFocus();
                return;

            case AccessibleKey.Down:
                TolkWrapper.Speak("End of list");
                SyncVisualFocus();
                return;

            case AccessibleKey.Right:
                if (_model.Skills.Count == 0)
                {
                    TolkWrapper.Speak("No skills");
                    return;
                }

                _section = CombatEncounterPreviewSection.Skills;
                _skillIndex = 0;
                _detailReader.Clear();
                AnnounceSkillsSection();
                SyncVisualFocus();
                return;

            case AccessibleKey.Confirm:
                AnnounceStats();
                SyncVisualFocus();
                return;
        }
    }

    private void HandleSkillsInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Left:
                _section = CombatEncounterPreviewSection.Stats;
                _detailReader.Clear();
                AnnounceStatsSection();
                SyncVisualFocus();
                return;

            case AccessibleKey.Right:
                AnnounceCurrentSkill();
                SyncVisualFocus();
                return;

            case AccessibleKey.Up:
                if (_model.Skills.Count == 0)
                {
                    TolkWrapper.Speak("No skills");
                    return;
                }

                if (_skillIndex <= 0)
                {
                    TolkWrapper.Speak("Start of list");
                    SyncVisualFocus();
                    return;
                }

                _skillIndex--;

                AnnounceCurrentSkill();
                SyncVisualFocus();
                return;

            case AccessibleKey.Down:
                if (_model.Skills.Count == 0)
                {
                    TolkWrapper.Speak("No skills");
                    return;
                }

                if (_skillIndex >= _model.Skills.Count - 1)
                {
                    TolkWrapper.Speak("End of list");
                    SyncVisualFocus();
                    return;
                }

                _skillIndex++;

                AnnounceCurrentSkill();
                SyncVisualFocus();
                return;

            case AccessibleKey.Confirm:
                Card skill = GetCurrentSkill();
                if (skill == null)
                {
                    TolkWrapper.Speak("No skill");
                    return;
                }

                TolkWrapper.Speak(ItemReader.GetDetailedDescription(skill));
                SyncVisualFocus();
                return;
        }
    }

    private void HandleBoardInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Left:
                NavigateBoard(-1);
                return;

            case AccessibleKey.Right:
                NavigateBoard(1);
                return;

            case AccessibleKey.Up:
                SpeakBoardDetail(up: true);
                return;

            case AccessibleKey.Down:
                SpeakBoardDetail(up: false);
                return;

            case AccessibleKey.Confirm:
                Card item = GetCurrentItem();
                if (item == null)
                {
                    TolkWrapper.Speak("No item");
                    return;
                }

                TolkWrapper.Speak(ItemReader.GetDetailedDescription(item));
                return;
        }
    }

    private void NavigateBoard(int delta)
    {
        if (_model.Items.Count == 0)
        {
            TolkWrapper.Speak("No items");
            return;
        }

        _detailReader.Clear();

        int nextIndex = _itemIndex + delta;
        if (nextIndex < 0)
        {
            TolkWrapper.Speak("Start of list");
            SyncVisualFocus();
            return;
        }
        else if (nextIndex >= _model.Items.Count)
        {
            TolkWrapper.Speak("End of list");
            SyncVisualFocus();
            return;
        }

        _itemIndex = nextIndex;
        AnnounceCurrentItem();
        SyncVisualFocus();
    }

    private void SpeakBoardDetail(bool up)
    {
        Card item = GetCurrentItem();
        if (item == null)
        {
            TolkWrapper.Speak("No item");
            return;
        }

        _detailReader.Init(item, c => ItemReader.GetEnemyDetailLines(c));
        if (!_detailReader.HasLines)
        {
            TolkWrapper.Speak("No details");
            return;
        }

        string line = up ? _detailReader.LineUp() : _detailReader.LineDown();
        TolkWrapper.Speak(line ?? "No details");
        SyncVisualFocus();
    }

    private void AnnounceStats()
    {
        TolkWrapper.Speak($"Health: {_model.Health}");
    }

    private void AnnounceStatsSection()
    {
        TolkWrapper.Speak($"Stats. Health: {_model.Health}");
    }

    private void AnnounceSkillsSection()
    {
        if (_model.Skills.Count == 0)
        {
            TolkWrapper.Speak("Skills (0)");
            return;
        }

        TolkWrapper.Speak($"Skills ({_model.Skills.Count}). {GetCurrentSkillAnnouncement()}");
    }

    private void AnnounceBoardSection()
    {
        if (_model.Items.Count == 0)
        {
            TolkWrapper.Speak("Board. No items");
            return;
        }

        TolkWrapper.Speak($"Board. {GetCurrentItemAnnouncement()}");
    }

    private void AnnounceCurrentSkill()
    {
        Card skill = GetCurrentSkill();
        if (skill == null)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        TolkWrapper.Speak(GetCurrentSkillAnnouncement());
    }

    private void AnnounceCurrentItem()
    {
        Card item = GetCurrentItem();
        if (item == null)
        {
            TolkWrapper.Speak("No item");
            return;
        }

        TolkWrapper.Speak(GetCurrentItemAnnouncement());
    }

    private Card GetCurrentSkill()
    {
        if (_model == null || _skillIndex < 0 || _skillIndex >= _model.Skills.Count)
            return null;

        return _model.Skills[_skillIndex];
    }

    private Card GetCurrentItem()
    {
        if (_model == null || _itemIndex < 0 || _itemIndex >= _model.Items.Count)
            return null;

        return _model.Items[_itemIndex];
    }

    private string GetCurrentSkillAnnouncement()
    {
        Card skill = GetCurrentSkill();
        if (skill == null)
            return "No skill";

        string name = ItemReader.GetCardName(skill);
        string description = ItemReader.GetFullDescription(skill);
        return !string.IsNullOrEmpty(description) ? $"{name}: {description}" : name;
    }

    private string GetCurrentItemAnnouncement()
    {
        Card item = GetCurrentItem();
        if (item == null)
            return "No item";

        return ItemReader.GetEnemyCompactDescription(item);
    }

    private CardTooltipController GetTooltipController()
    {
        if (_visualTooltipController != null)
            return _visualTooltipController;

        if (_sourceEncounterCard == null || Data.TooltipParentComponent == null)
            return null;

        return Data.TooltipParentComponent.GetCardTooltipController(_sourceEncounterCard);
    }

    private bool TryShowEncounterTooltip(EncounterController encounterController)
    {
        try
        {
            if (encounterController == null || Data.TooltipParentComponent == null)
                return false;

            var tooltipData = encounterController.GetTooltipData();
            if (tooltipData == null)
                return false;

            Data.TooltipParentComponent.ShowCardTooltipController(
                encounterController.transform,
                GetTooltipOffset(encounterController),
                tooltipData);

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"TryShowEncounterTooltip error: {ex.Message}");
            return false;
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
            Plugin.Logger.LogWarning($"IsTooltipLocked reflection error: {ex.Message}");
        }

        return Data.TooltipParentComponent != null &&
               Data.TooltipParentComponent.IsCardTooltipControllerLocked(tooltipController);
    }

    private void SubscribeTooltipEvents()
    {
        try
        {
            var tooltipUnlockEvent = GetTooltipUnlockEvent();
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
            Plugin.Logger.LogWarning($"SubscribeTooltipEvents error: {ex.Message}");
        }
    }

    private void UnsubscribeTooltipEvents()
    {
        try
        {
            var tooltipUnlockEvent = _tooltipUnlockEvent ?? GetTooltipUnlockEvent();
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
            Plugin.Logger.LogWarning($"UnsubscribeTooltipEvents error: {ex.Message}");
        }
    }

    private void OnTooltipUnlock()
    {
        if (IsActive)
        {
            _tooltipUnlockObserved = true;
        }
    }

    private static object GetTooltipUnlockEvent()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type eventsType = typeof(Data).Assembly.GetType("TheBazaar.Events");
        FieldInfo field = eventsType?.GetField("TooltipUnlock", flags);
        return field?.GetValue(null);
    }

    private void SetEncounterCursorState(bool isOverCard)
    {
        try
        {
            var boardManager = BoardStashNavigator.GetBoardManager();
            if (boardManager == null || _sourceEncounterCard == null)
                return;

            var controller = VisualSelector.FindCardController(_sourceEncounterCard, boardManager) as EncounterController;
            if (controller != null)
            {
                SetCursorOverCard(controller, isOverCard);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"SetEncounterCursorState error: {ex.Message}");
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
            Plugin.Logger.LogWarning($"SetCursorOverCard error: {ex.Message}");
        }
    }

    private void UnlockVisualPreview()
    {
        try
        {
            var tooltipController = GetTooltipController();
            if (tooltipController != null && IsTooltipLocked(tooltipController))
            {
                tooltipController.Unlock();
            }
            else if (tooltipController != null)
            {
                tooltipController.StartTooltipFadeOut();
            }

            if (Data.TooltipParentComponent != null)
            {
                Data.TooltipParentComponent.HideSecondaryCardTooltipController();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"UnlockVisualPreview error: {ex.Message}");
        }
    }

    private void SyncVisualFocus()
    {
        try
        {
            if (!IsVisualActive())
                return;

            HideCurrentPreviewHover();

            if (_section == CombatEncounterPreviewSection.Stats)
                return;

            if (!TryGetMonsterBoardTooltip(out var monsterBoardTooltip))
                return;

            if (_section == CombatEncounterPreviewSection.Skills)
            {
                var skills = GetActivePreviewCards(monsterBoardTooltip, "_activeSkills");
                if (_skillIndex >= 0 && _skillIndex < skills.Count)
                {
                    skills[_skillIndex]?.OnHover();
                }
            }
            else if (_section == CombatEncounterPreviewSection.Board)
            {
                var items = GetActivePreviewCards(monsterBoardTooltip, "_activeCards");
                if (_itemIndex >= 0 && _itemIndex < items.Count)
                {
                    items[_itemIndex]?.OnHover();
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"SyncVisualFocus error: {ex.Message}");
        }
    }

    private void HideCurrentPreviewHover()
    {
        try
        {
            if (Data.TooltipParentComponent != null)
                Data.TooltipParentComponent.HideSecondaryCardTooltipController();

            if (!TryGetMonsterBoardTooltip(out var monsterBoardTooltip))
                return;

            foreach (var preview in GetActivePreviewCards(monsterBoardTooltip, "_activeSkills"))
            {
                preview?.OnHoverOut();
            }

            foreach (var preview in GetActivePreviewCards(monsterBoardTooltip, "_activeCards"))
            {
                preview?.OnHoverOut();
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"HideCurrentPreviewHover error: {ex.Message}");
        }
    }

    private bool TryGetMonsterBoardTooltip(out MonsterBoardTooltip monsterBoardTooltip)
    {
        monsterBoardTooltip = null;

        var tooltipController = GetTooltipController();
        if (tooltipController == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(CardTooltipController).GetField("_monsterBoardTooltip", flags);
        monsterBoardTooltip = field?.GetValue(tooltipController) as MonsterBoardTooltip;
        return monsterBoardTooltip != null;
    }

    private static List<CardPreviewBase> GetActivePreviewCards(MonsterBoardTooltip monsterBoardTooltip, string fieldName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(MonsterBoardTooltip).GetField(fieldName, flags);
        if (field?.GetValue(monsterBoardTooltip) is System.Collections.IEnumerable enumerable)
        {
            var result = new List<CardPreviewBase>();
            foreach (var entry in enumerable)
            {
                if (entry is CardPreviewBase preview)
                    result.Add(preview);
            }
            return result;
        }

        return new List<CardPreviewBase>();
    }
}
