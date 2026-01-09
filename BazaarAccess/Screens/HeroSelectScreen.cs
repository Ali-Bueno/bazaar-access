using BazaarAccess.Accessibility;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Pantalla de selección de héroe.
/// Usa textos dinámicos del juego para soporte multiidioma.
/// </summary>
public class HeroSelectScreen : BaseScreen
{
    public override string ScreenName => "HeroSelect";

    public HeroSelectScreen(Transform root) : base(root)
    {
    }

    protected override void BuildMenu()
    {
        // Usar delegados para obtener el texto real del juego (multiidioma)
        Menu.AddOption(
            () => GetButtonTextByName("Btn_Ready"),
            () => ClickButtonByName("Btn_Ready"));

        Menu.AddOption(
            () => GetButtonTextByName("Btn_Casual"),
            () => ClickButtonByName("Btn_Casual"));

        Menu.AddOption(
            () => GetButtonTextByName("Btn_Ranked"),
            () => ClickButtonByName("Btn_Ranked"));

        Menu.AddOption(
            () => GetButtonTextByName("Btn_Back"),
            () => ClickButtonByName("Btn_Back"));
    }
}
