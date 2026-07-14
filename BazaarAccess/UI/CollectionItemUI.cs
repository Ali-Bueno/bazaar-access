using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using TheBazaar.Feature.Chest.Scene;
using UnityEngine;

namespace BazaarAccess.UI;

/// <summary>
/// Accessible UI for the collection item dialogue shown when opening chests with new cosmetics.
/// This dialogue appears when you get a new hero skin, board, carpet, etc.
/// </summary>
public class CollectionItemUI : BaseUI
{
    public override string UIName => "New Collection Item";

    private ChestSceneController _controller;
    private string _itemName;
    private string _editionNumber;

    public CollectionItemUI(Transform root, ChestSceneController controller) : base(root)
    {
        _controller = controller;
        ReadItemInfo();
    }

    private void ReadItemInfo()
    {
        try
        {
            // Get the item name from the header text
            if (_controller.CollectableHeaderText != null)
            {
                _itemName = _controller.CollectableHeaderText.text;
            }

            // Get the edition number
            if (_controller.CollectableEditionNumber != null)
            {
                _editionNumber = _controller.CollectableEditionNumber.text;
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"CollectionItemUI: Error reading item info: {e.Message}");
            _itemName = Loc.T("ui.collection.default_name");
        }
    }

    protected override void BuildMenu()
    {
        Menu.AddOption(
            () => Loc.T("ui.continue_prompt"),
            () => Close());
    }

    public override void OnFocus()
    {
        // Announce the new item
        string itemName = !string.IsNullOrEmpty(_itemName) ? _itemName : Loc.T("ui.collection.default_name");

        string announcement = !string.IsNullOrEmpty(_editionNumber)
            ? Loc.T("ui.collection.new_item_with_edition", itemName, _editionNumber)
            : Loc.T("ui.collection.new_item", itemName);

        announcement += " " + Loc.T("ui.continue_prompt");

        TolkWrapper.Speak(announcement);
        MessageBuffer.Add(announcement);
    }

    private void Close()
    {
        try
        {
            // Click the close button
            if (_controller.CollectablePanelCloseButton != null)
            {
                _controller.CollectablePanelCloseButton.onClick.Invoke();
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"CollectionItemUI: Error closing: {e.Message}");
        }

        AccessibilityMgr.PopUI();
    }

    protected override void OnBack()
    {
        // Only Enter closes, not Escape
    }

    public override void HandleInput(AccessibleKey key)
    {
        // Only Enter closes
        if (key == AccessibleKey.Confirm)
        {
            Close();
            return;
        }

        // Ignore all other keys
    }
}
