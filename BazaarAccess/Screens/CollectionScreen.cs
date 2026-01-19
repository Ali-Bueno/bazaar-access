using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarGameShared;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Accessible screen for the collections menu.
/// Allows navigation through collection categories and items.
/// </summary>
public class CollectionScreen : BaseScreen
{
    public override string ScreenName => "Collection";

    private Transform _root;
    private CollectionManager _collectionManager;
    private CollectionUIController _uiController;

    // Available collection types (excluding Invalid, Toys, Chests, Stash, Bank, Count)
    private readonly BazaarInventoryTypes.ECollectionType[] _collectionTypes = new[]
    {
        BazaarInventoryTypes.ECollectionType.HeroSkins,
        BazaarInventoryTypes.ECollectionType.Boards,
        BazaarInventoryTypes.ECollectionType.CardSkins,
        BazaarInventoryTypes.ECollectionType.Carpets,
        BazaarInventoryTypes.ECollectionType.CardBacks,
        BazaarInventoryTypes.ECollectionType.Album
    };

    private int _currentCategoryIndex = 0;
    private int _currentItemIndex = 0;
    private BazaarSaleItem[] _currentItems = Array.Empty<BazaarSaleItem>();
    private int _detailLineIndex = 0;
    private bool _inItemNavigation = false;

    public CollectionScreen(Transform root, CollectionUIController uiController) : base(root)
    {
        _root = root;
        _uiController = uiController;
        TryGetCollectionManager();
        LoadCurrentCategory();
        Plugin.Logger.LogInfo($"CollectionScreen created. CollectionManager: {(_collectionManager != null ? "found" : "NULL")}, UIController: {(_uiController != null ? "found" : "NULL")}");
    }

    protected override void BuildMenu()
    {
        // Menu is not used - we have custom navigation
    }

    private void TryGetCollectionManager()
    {
        if (_collectionManager == null)
        {
            _collectionManager = Services.Get<CollectionManager>();
        }
    }

    private void LoadCurrentCategory()
    {
        TryGetCollectionManager();

        var collectionType = _collectionTypes[_currentCategoryIndex];

        if (_collectionManager != null)
        {
            try
            {
                _currentItems = _collectionManager.GetPlayerCollectables(collectionType, includeDefault: true);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"CollectionScreen: Error getting collectables: {e.Message}");
                _currentItems = Array.Empty<BazaarSaleItem>();
            }
        }
        else
        {
            Plugin.Logger.LogWarning("CollectionScreen: CollectionManager is null");
            _currentItems = Array.Empty<BazaarSaleItem>();
        }

        _currentItemIndex = 0;
        _detailLineIndex = 0;

        Plugin.Logger.LogInfo($"CollectionScreen: Loaded {_currentItems.Length} items for {collectionType}");
    }

    private string GetCategoryName(BazaarInventoryTypes.ECollectionType type)
    {
        return type switch
        {
            BazaarInventoryTypes.ECollectionType.HeroSkins => "Hero Skins",
            BazaarInventoryTypes.ECollectionType.Boards => "Boards",
            BazaarInventoryTypes.ECollectionType.CardSkins => "Card Skins",
            BazaarInventoryTypes.ECollectionType.Carpets => "Carpets",
            BazaarInventoryTypes.ECollectionType.CardBacks => "Card Backs",
            BazaarInventoryTypes.ECollectionType.Album => "Albums",
            _ => type.ToString()
        };
    }

    private string GetCurrentCategoryName()
    {
        return GetCategoryName(_collectionTypes[_currentCategoryIndex]);
    }

    private string GetItemSummary(BazaarSaleItem item)
    {
        var parts = new List<string>();

        // Name
        if (!string.IsNullOrEmpty(item.Name))
            parts.Add(item.Name);
        else
            parts.Add("Unknown");

        // Rarity
        if (item.Rarity.Name != null)
            parts.Add(item.Rarity.Name);

        // Equipped status
        if (_collectionManager != null && _collectionManager.IsCollectibleOfIDEquipped(item))
            parts.Add("equipped");

        return string.Join(", ", parts);
    }

    private List<string> GetItemDetailLines(BazaarSaleItem item)
    {
        var lines = new List<string>();

        // Name
        if (!string.IsNullOrEmpty(item.Name))
            lines.Add(item.Name);

        // Type info (e.g., "Legendary Vanessa Hero Skin")
        string typeInfo = item.GetCollectionTypeInfo();
        if (!string.IsNullOrEmpty(typeInfo))
            lines.Add(typeInfo);

        // Description
        if (!string.IsNullOrEmpty(item.Description))
            lines.Add(item.Description);

        // Equipped status
        if (_collectionManager != null)
        {
            if (_collectionManager.IsCollectibleOfIDEquipped(item))
                lines.Add("Currently equipped");
            else
                lines.Add("Not equipped");
        }

        return lines;
    }

    public override void OnFocus()
    {
        Plugin.Logger.LogInfo("CollectionScreen.OnFocus called");
        AnnounceCurrentState();
    }

    private void AnnounceCurrentState()
    {
        string categoryName = GetCurrentCategoryName();
        int itemCount = _currentItems.Length;

        string message = $"{categoryName}, {itemCount} items";

        if (itemCount > 0 && _inItemNavigation)
        {
            message += $". {GetItemSummary(_currentItems[_currentItemIndex])}";
        }

        TolkWrapper.Speak(message);
    }

    private void AnnounceCategory()
    {
        string categoryName = GetCurrentCategoryName();
        int itemCount = _currentItems.Length;
        TolkWrapper.Speak($"{categoryName}, {itemCount} items");
    }

    private void AnnounceCurrentItem()
    {
        if (_currentItems.Length == 0)
        {
            TolkWrapper.Speak("No items");
            return;
        }

        var item = _currentItems[_currentItemIndex];
        string summary = GetItemSummary(item);
        TolkWrapper.Speak(summary);
    }

    public override void HandleInput(AccessibleKey key)
    {
        Plugin.Logger.LogInfo($"CollectionScreen.HandleInput: {key}");

        switch (key)
        {
            case AccessibleKey.Back:
                GoBack();
                return;

            case AccessibleKey.Left:
                NavigateCategory(-1);
                return;

            case AccessibleKey.Right:
                NavigateCategory(1);
                return;

            case AccessibleKey.Up:
                NavigateItem(-1);
                return;

            case AccessibleKey.Down:
                NavigateItem(1);
                return;

            case AccessibleKey.DetailUp:
                ReadDetailLine(-1);
                return;

            case AccessibleKey.DetailDown:
                ReadDetailLine(1);
                return;

            case AccessibleKey.Confirm:
                ConfirmItem();
                return;
        }
    }

    private void NavigateCategory(int direction)
    {
        _currentCategoryIndex += direction;

        if (_currentCategoryIndex < 0)
            _currentCategoryIndex = _collectionTypes.Length - 1;
        else if (_currentCategoryIndex >= _collectionTypes.Length)
            _currentCategoryIndex = 0;

        LoadCurrentCategory();
        _inItemNavigation = false;

        AnnounceCategory();
    }

    private void NavigateItem(int direction)
    {
        if (_currentItems.Length == 0)
        {
            TolkWrapper.Speak("No items in this category");
            return;
        }

        _inItemNavigation = true;
        _currentItemIndex += direction;
        _detailLineIndex = 0;

        if (_currentItemIndex < 0)
            _currentItemIndex = _currentItems.Length - 1;
        else if (_currentItemIndex >= _currentItems.Length)
            _currentItemIndex = 0;

        AnnounceCurrentItem();
    }

    private void ReadDetailLine(int direction)
    {
        if (_currentItems.Length == 0)
        {
            TolkWrapper.Speak("No item selected");
            return;
        }

        var item = _currentItems[_currentItemIndex];
        var lines = GetItemDetailLines(item);

        if (lines.Count == 0)
        {
            TolkWrapper.Speak("No details available");
            return;
        }

        // Navigate through detail lines
        if (direction > 0)
        {
            _detailLineIndex++;
            if (_detailLineIndex >= lines.Count)
                _detailLineIndex = 0;
        }
        else
        {
            _detailLineIndex--;
            if (_detailLineIndex < 0)
                _detailLineIndex = lines.Count - 1;
        }

        TolkWrapper.Speak(lines[_detailLineIndex]);
    }

    private void ConfirmItem()
    {
        if (_currentItems.Length == 0)
        {
            TolkWrapper.Speak("No item to select");
            return;
        }

        if (!_inItemNavigation)
        {
            // Enter item navigation mode
            _inItemNavigation = true;
            AnnounceCurrentItem();
            return;
        }

        var item = _currentItems[_currentItemIndex];

        // Check if already equipped
        if (_collectionManager != null && _collectionManager.IsCollectibleOfIDEquipped(item))
        {
            TolkWrapper.Speak($"{item.Name} is already equipped");
            return;
        }

        // Equip the item
        if (_collectionManager != null)
        {
            try
            {
                // Fire and forget the equip task
                _ = _collectionManager.EquipCollectible(item);
                TolkWrapper.Speak($"Equipping {item.Name}");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to equip item: {e.Message}");
                TolkWrapper.Speak("Failed to equip item");
            }
        }
    }

    private void GoBack()
    {
        Plugin.Logger.LogInfo("CollectionScreen.GoBack called");

        if (_inItemNavigation)
        {
            // Exit item navigation, stay in category view
            _inItemNavigation = false;
            AnnounceCategory();
            return;
        }

        // Try to click the back button via reflection on CollectionUIController
        if (ClickBackButtonViaReflection())
            return;

        // Try to find and click ButtonCustom (used in Collection UI)
        if (ClickButtonCustomByName("backButton"))
            return;

        // Fallback to standard buttons
        if (!ClickButtonByName("backButton"))
        {
            if (!ClickButtonByName("BackButton"))
            {
                ClickButtonByName("HomeButton");
            }
        }
    }

    /// <summary>
    /// Clicks the back button using reflection to access the private field.
    /// </summary>
    private bool ClickBackButtonViaReflection()
    {
        if (_uiController == null)
        {
            Plugin.Logger.LogInfo("ClickBackButtonViaReflection: UIController is null");
            return false;
        }

        try
        {
            // Get the private backButton field
            var field = typeof(CollectionUIController).GetField("backButton", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Plugin.Logger.LogInfo("ClickBackButtonViaReflection: backButton field not found");
                return false;
            }

            var backButton = field.GetValue(_uiController) as ButtonCustom;
            if (backButton == null)
            {
                Plugin.Logger.LogInfo("ClickBackButtonViaReflection: backButton is null");
                return false;
            }

            Plugin.Logger.LogInfo("ClickBackButtonViaReflection: Clicking back button");
            backButton.OnMouseClickCustom();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"ClickBackButtonViaReflection error: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Finds and clicks a ButtonCustom by name.
    /// </summary>
    private bool ClickButtonCustomByName(string name)
    {
        if (Root == null) return false;

        var buttonCustoms = Root.GetComponentsInChildren<ButtonCustom>(true);
        foreach (var bc in buttonCustoms)
        {
            if (bc.gameObject.name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                bc.gameObject.activeInHierarchy)
            {
                Plugin.Logger.LogInfo($"Click ButtonCustom: {name}");
                bc.OnMouseClickCustom();
                return true;
            }
        }

        Plugin.Logger.LogInfo($"ButtonCustom not found: {name}");
        return false;
    }
}
