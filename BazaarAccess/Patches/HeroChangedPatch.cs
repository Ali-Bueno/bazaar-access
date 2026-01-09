using BazaarAccess.Core;
using HarmonyLib;

namespace BazaarAccess.Patches;

/// <summary>
/// Anuncia cuando cambia el héroe seleccionado.
/// </summary>
[HarmonyPatch(typeof(HeroSelectDisplay), "OnHeroChanged")]
public static class HeroChangedPatch
{
    static void Postfix(object hero)
    {
        string heroName = hero?.ToString() ?? "Desconocido";
        Plugin.Logger.LogInfo($"Héroe seleccionado: {heroName}");
        TolkWrapper.Speak(heroName);
    }
}
