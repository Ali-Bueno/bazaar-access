using BazaarAccess.Accessibility;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Menú principal del juego.
/// </summary>
public class MainMenuScreen : BaseScreen
{
    public override string ScreenName => "MainMenu";

    public MainMenuScreen(Transform root) : base(root)
    {
    }

    protected override void BuildMenu()
    {
        // Botones del MainMenu (según log)
        Menu.AddOption(
            () => GetButtonTextByName("Btn_Play"),
            () => ClickButtonByName("Btn_Play"));

        Menu.AddOption(
            () => GetButtonTextByName("Btn_Collection"),
            () => ClickButtonByName("Btn_Collection"));
    }
}
