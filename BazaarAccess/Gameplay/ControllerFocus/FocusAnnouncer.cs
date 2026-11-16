using System.Reflection;
using BazaarAccess.Core;
using BazaarAccess.Gameplay.Navigation;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using HarmonyLib;
using TheBazaar;
using TheBazaar.Inputs.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Gameplay.ControllerFocus;

/// <summary>
/// Speaks whatever the game's own controller navigation focuses during a run (cards, board buttons, stash, replay buttons).
/// </summary>
[HarmonyPatch(typeof(FocusController), nameof(FocusController.SetFocus))]
public static class FocusAnnouncer
{
    [HarmonyPrefix]
    public static void Prefix(FocusController __instance, out IFocusable __state) => __state = __instance.Current;

    [HarmonyPostfix]
    public static void Postfix(FocusController __instance, IFocusable __state)
    {
        // Silent while the mod's own navigation drives (the game keeps refocusing behind it).
        if (!GamepadNavigator.GameNavigationActive) return;

        var current = __instance.Current;
        if (current == null || ReferenceEquals(current, __state)) return;

        string text = Describe(current);
        if (string.IsNullOrEmpty(text)) return;

        // The game re-focuses the same button when a screen rebuilds (e.g. "Continue" after combat).
        bool repeat = text == _lastText && Time.realtimeSinceStartup - _lastTime < RepeatWindowSeconds;
        _lastText = text;
        _lastTime = Time.realtimeSinceStartup;
        if (!repeat) TolkWrapper.Speak(text);
    }

    private const float RepeatWindowSeconds = 2f;
    private static string _lastText;
    private static float _lastTime;

    /// <summary>
    /// Text for a focused target, reusing the descriptions of the mod's own navigation.
    /// </summary>
    public static string Describe(IFocusable target)
    {
        switch (target)
        {
            case CardController cardController:
                return DescribeCard(cardController.CardData);
            case RerollButton _:
                return Loc.T("nav.action.reroll_cost", Data.CurrentState?.RerollCost ?? 0);
            case ExitButton _:
                return Loc.T("nav.action.exit");
            case StorageToy _:
                return Loc.T(Data.IsStorageOpen ? "focus.stash.open" : "focus.stash.closed");
            case ButtonNavTarget buttonTarget:
                var button = AccessTools.Field(typeof(ButtonNavTarget), "_button")?.GetValue(buttonTarget) as Button;
                return button != null ? TextHelper.CleanText(button.GetComponentInChildren<TMP_Text>(true)?.text) : null;
            case Component component:
                return component.gameObject.name;
            default:
                return null;
        }
    }

    public static string DescribeCard(Card card)
    {
        if (card == null) return null;
        Patches.CardEventHandler.RememberName(card);

        var player = Data.Run?.Player;
        bool ownedByPlayer = player != null && card.Owner == player;

        if (!ownedByPlayer && card.Owner != null)
            return Loc.T("focus.opponent_card", ItemReader.GetEnemyCompactDescription(card));

        if (!ownedByPlayer)
            return SelectionNavigator.GetCardDescription(card, NavigationSection.Selection, SelectionNavigator.IsSelectionFree());

        if (card.Type == ECardType.Skill)
            return SelectionNavigator.GetCardDescription(card, NavigationSection.Skills, false);

        var section = card.Section == EInventorySection.Stash ? NavigationSection.Stash : NavigationSection.Board;
        return SelectionNavigator.GetCardDescription(card, section, false);
    }
}
