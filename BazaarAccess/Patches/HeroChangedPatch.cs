using BazaarAccess.Core;
using BazaarGameShared.Domain.Core.Types;
using HarmonyLib;
using TheBazaar;
using TheBazaar.AppFramework;

namespace BazaarAccess.Patches;

/// <summary>
/// Announces when the selected hero changes (or Random hero is chosen).
/// </summary>
[HarmonyPatch(typeof(HeroSelectDisplay), "OnHeroChanged")]
public static class HeroChangedPatch
{
    static void Postfix(EHero hero)
    {
        EHero selected = hero; // local copy: the Harmony analyzer flags method calls on patch parameters
        string heroName = PlayerPreferences.Data.RandomHeroEnabled
            ? HeroManager.RandomHeroName.GetLocalizedText()
            : Services.Get<HeroManager>()?.GetHeroName(selected);
        if (string.IsNullOrEmpty(heroName)) heroName = selected.ToString();

        Plugin.Logger.LogInfo($"Hero selected: {heroName}");
        TolkWrapper.Speak(heroName);
    }
}
