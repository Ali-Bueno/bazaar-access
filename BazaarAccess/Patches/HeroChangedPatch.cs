using BazaarAccess.Core;
using HarmonyLib;

namespace BazaarAccess.Patches;

/// <summary>
/// Announces when the selected hero changes.
/// </summary>
[HarmonyPatch(typeof(HeroSelectDisplay), "OnHeroChanged")]
public static class HeroChangedPatch
{
    static void Postfix(object hero)
    {
        string heroName = hero?.ToString() ?? Loc.T("patch.hero.unknown");
        Plugin.Logger.LogInfo($"Hero selected: {heroName}");
        TolkWrapper.Speak(heroName);
    }
}
