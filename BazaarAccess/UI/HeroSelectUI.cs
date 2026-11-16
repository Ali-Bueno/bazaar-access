using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarGameShared.Domain.Core.Types;
using HarmonyLib;
using TheBazaar;
using TheBazaar.AppFramework;
using TheBazaar.UI;
using TheBazaar.UI.Menu;
using UnityEngine;

namespace BazaarAccess.UI;

/// <summary>
/// The hero picker popup (HeroSelectPopupController): one option per hero plus Random.
/// </summary>
public class HeroSelectUI : BaseUI
{
    private readonly MonoBehaviour _popup;

    public override string UIName => Loc.T("ui.heroselect.name");

    public HeroSelectUI(MonoBehaviour popup) : base(popup.transform, buildMenuNow: false)
    {
        _popup = popup;
        Initialize();
    }

    protected override void BuildMenu()
    {
        if (!(_popup is IPooledDataProvider provider)) return;

        foreach (var hero in provider.GetData().OfType<HeroData>())
        {
            var data = hero;
            Menu.AddOption(() => Describe(data), () => Select(data), direction => ToggleRandomExclusion(data));
        }
    }

    public override void OnFocus()
    {
        base.OnFocus();
        TolkWrapper.Speak(Loc.T("ui.heroselect.hint"), interrupt: false);
    }

    protected override void OnBack()
    {
        AccessTools.Method(_popup.GetType(), "OnBackButtonClicked")?.Invoke(_popup, null);
    }

    private static bool IsRandom(HeroData data) => data.Hero == EHero.Common;

    private static bool IsSelected(HeroData data)
        => IsRandom(data) ? PlayerPreferences.Data.RandomHeroEnabled : HeroManager.IsSelectedHero(data.Hero);

    private static string Describe(HeroData data)
    {
        string text = data.Name;
        if (IsSelected(data)) text = Loc.T("screen.mainmenu.option_selected", text);
        if (!data.Owned) text = Loc.T("ui.heroselect.locked", text);
        else if (!IsRandom(data) && PlayerPreferences.Data.GetHeroExcludedFromRandom(data.Hero))
            text = Loc.T("ui.heroselect.excluded", text);
        return text;
    }

    // Mirrors HeroOptionController.HeroClicked; the game closes the popup on HeroChanged.
    private static void Select(HeroData data)
    {
        if (IsRandom(data))
        {
            PlayerPreferences.Data.RandomHeroEnabled = true;
            TriggerHeroChanged(ClientCache.RunConfig.Value.Hero);
            return;
        }

        Services.Get<HeroManager>()?.SelectHero(data.Hero);
    }

    // TheBazaar.Events is internal; raise Events.HeroChanged the way the game's hero option does.
    private static void TriggerHeroChanged(EHero hero)
    {
        var heroChanged = AccessTools.Field(AccessTools.TypeByName("TheBazaar.Events"), "HeroChanged")?.GetValue(null);
        if (heroChanged == null) return;
        AccessTools.Method(heroChanged.GetType(), "Trigger", new[] { typeof(EHero) })?.Invoke(heroChanged, new object[] { hero });
    }

    // Mirrors HeroRandomExcludeController: only owned heroes can be toggled.
    private static void ToggleRandomExclusion(HeroData data)
    {
        if (IsRandom(data) || !data.Owned) return;

        bool exclude = !PlayerPreferences.Data.GetHeroExcludedFromRandom(data.Hero);
        PlayerPreferences.Data.SetHeroExcludedFromRandom(data.Hero, exclude);
        TolkWrapper.Speak(Loc.T(exclude ? "ui.heroselect.excluded_announce" : "ui.heroselect.included_announce", data.Name));
    }
}
