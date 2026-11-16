using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Screens;
using BazaarGameShared;
using HarmonyLib;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// Main menu "Equip Collectibles" popup (LoadoutPopupController): categories with what is equipped,
/// then the category's collectibles; Enter equips. Backspace goes back to the categories, then closes.
/// </summary>
public class LoadoutUI : BaseUI
{
    // The category list is rebuilt by the game a moment after the popup opens.
    private const float ListLoadDelaySeconds = 0.6f;

    private readonly MonoBehaviour _popup;
    private bool _inItems;

    public LoadoutUI(MonoBehaviour popup) : base(popup.transform, buildMenuNow: false)
    {
        _popup = popup;
        Initialize();
    }

    public override string UIName => Loc.T("ui.loadout.title");

    private MonoBehaviour Panel => AccessTools.Field(_popup.GetType(), "cosmeticsPanelController")?.GetValue(_popup) as MonoBehaviour;

    protected override void BuildMenu()
    {
        if (_inItems) BuildItems();
        else BuildCategories();
    }

    private void BuildCategories()
    {
        var panel = Panel;
        if (panel == null) return;

        if (AccessTools.Field(panel.GetType(), "_cosmeticsButton")?.GetValue(panel) is IEnumerable buttons)
        {
            foreach (var button in buttons.OfType<CosmeticButton>())
            {
                var current = button;
                Menu.AddOption(
                    () => CategoryLabel(current),
                    () => OpenCategory(current),
                    () => current != null && current.gameObject.activeInHierarchy);
            }
        }

        if (AccessTools.Field(panel.GetType(), "_randomizeToggle")?.GetValue(panel) is Toggle randomize)
        {
            Menu.AddOption(
                () => $"{Loc.T("ui.loadout.randomize")}: {(randomize.isOn ? Loc.T("ui.on") : Loc.T("ui.off"))}",
                () => randomize.isOn = !randomize.isOn,
                () => randomize.gameObject.activeInHierarchy && randomize.interactable);
        }
    }

    private static BazaarInventoryTypes.ECollectionType TypeOf(CosmeticButton button)
        => (BazaarInventoryTypes.ECollectionType)AccessTools.Field(typeof(CosmeticButton), "_cosmeticType").GetValue(button);

    private static string CategoryLabel(CosmeticButton button)
    {
        var type = TypeOf(button);
        string category = CollectionScreen.GetCategoryName(type);
        var manager = Services.Get<CollectionManager>();
        string equipped = manager != null ? manager.GetEquippedSaleItem(type, Data.SelectedHero).Name : null;
        return string.IsNullOrEmpty(equipped) ? category : Loc.T("ui.loadout.category_equipped", category, equipped);
    }

    private void OpenCategory(CosmeticButton button)
    {
        button.ForceClickButton();
        _inItems = true;
        Plugin.Instance.StartCoroutine(ShowItemsWhenLoaded(CollectionScreen.GetCategoryName(TypeOf(button))));
    }

    private IEnumerator ShowItemsWhenLoaded(string category)
    {
        yield return new WaitForSecondsRealtime(ListLoadDelaySeconds);
        if (!_inItems || !IsValid()) yield break;
        Menu.Clear();
        var items = BuildItems();
        TolkWrapper.Speak(Loc.Plural("screen.collection.category_summary", items.Count, category, items.Count));
        if (items.Count > 0) TolkWrapper.Speak(ItemLabel(items[0]), interrupt: false); // the cursor starts on it
    }

    private List<CosmeticItem> BuildItems()
    {
        // Hidden placeholder entries are skipped.
        var items = Root.GetComponentsInChildren<CosmeticItem>(false).Where(i => i.isActiveAndEnabled).ToList();
        foreach (var item in items)
        {
            var current = item;
            Menu.AddOption(
                () => ItemLabel(current),
                () => Equip(current),
                () => current != null && current.isActiveAndEnabled);
        }
        return items;
    }

    private static string ItemLabel(CosmeticItem item)
    {
        var data = (EquipableItem)AccessTools.Field(typeof(CosmeticItem), "_equipableItem").GetValue(item);
        string name = string.IsNullOrEmpty(data.itemData.Name) ? Loc.T("screen.collection.item_unknown") : data.itemData.Name;
        bool equipped = (bool)AccessTools.Field(typeof(CosmeticButtonBase), "_isSelected").GetValue(item);
        return equipped ? Loc.T("ui.loadout.equipped", name) : name;
    }

    private void Equip(CosmeticItem item)
    {
        var button = AccessTools.Field(typeof(CosmeticButtonBase), "_buttonComponent").GetValue(item) as ButtonCustom;
        button?.OnMouseClickCustom();
        Plugin.Instance.StartCoroutine(AnnounceEquipped(item));
    }

    private IEnumerator AnnounceEquipped(CosmeticItem item)
    {
        const float equipDelaySeconds = 1f; // the equip request goes to the server
        yield return new WaitForSecondsRealtime(equipDelaySeconds);
        if (item != null) TolkWrapper.Speak(ItemLabel(item));
    }

    protected override void OnBack()
    {
        if (_inItems)
        {
            _inItems = false;
            Menu.Clear();
            BuildCategories();
            Menu.StartReading(announceMenuName: true);
            return;
        }
        AccessTools.Method(_popup.GetType(), "OnBackButtonClicked")?.Invoke(_popup, null);
    }

    public override void OnFocus()
    {
        Plugin.Instance.StartCoroutine(ReadWhenLoaded());
    }

    private IEnumerator ReadWhenLoaded()
    {
        yield return new WaitForSecondsRealtime(ListLoadDelaySeconds);
        if (!IsValid()) yield break;
        Menu.Clear();
        BuildMenu();
        Menu.StartReading(announceMenuName: true);
        var first = FirstCategory();
        if (first != null && !_inItems) TolkWrapper.Speak(CategoryLabel(first), interrupt: false); // the cursor starts on it
    }

    private CosmeticButton FirstCategory()
    {
        var panel = Panel;
        var buttons = panel != null ? AccessTools.Field(panel.GetType(), "_cosmeticsButton")?.GetValue(panel) as IEnumerable : null;
        return buttons?.OfType<CosmeticButton>().FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy);
    }
}
