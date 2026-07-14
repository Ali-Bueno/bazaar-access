using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Main menu of the game.
/// </summary>
public class MainMenuScreen : BaseScreen
{
    public override string ScreenName => Loc.T("screen.mainmenu.name");

    public MainMenuScreen(Transform root) : base(root)
    {
    }

    protected override void BuildMenu()
    {
        // Play button
        AddButtonIfExists("Btn_Play", "playButton");

        // Season Pass / Battle Pass
        AddButtonIfExists("Btn_BattlePass", "battlePassButton");

        // Chests
        AddButtonIfExists("Btn_Chest", "chestButton");

        // Collection
        AddButtonIfExists("Btn_Collection", "collectionButton");

        // Marketplace / Bazaar - Hidden for now until accessible
        // AddButtonIfExists("Btn_Bazaar", "bazaarButton");
        // AddButtonIfExists("Btn_Marketplace", "_marketplaceButtonController");
    }

    private void AddButtonIfExists(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var button = FindButtonByName(name);
            if (button != null && button.gameObject.activeInHierarchy)
            {
                string text = GetButtonText(button);
                if (string.IsNullOrWhiteSpace(text)) text = name;

                Menu.AddOption(
                    () => GetButtonTextByName(name),
                    () => ClickButtonByName(name));
                return;
            }
        }
    }
}
