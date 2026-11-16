using System.Collections;
using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.Gameplay;
using HarmonyLib;
using TheBazaar;
using TheBazaar.UI;
using TheBazaar.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Screens;

/// <summary>
/// Main menu scene (play mode, hero, play button and the game's navigation menu), built on MainMenuBaseView.
/// </summary>
public class MainMenuScreen : BaseScreen
{
    // Scenes from the game's navigation menu that the mod has accessible screens for.
    private static readonly SceneID[] SupportedScenes =
    {
        SceneID.PrizePass, SceneID.CollectionScene, SceneID.ChestScene, SceneID.PlayerProfileScene, SceneID.ExitGame
    };

    private readonly MainMenuBaseView _view;

    public override string ScreenName => Loc.T("screen.mainmenu.name");

    public MainMenuScreen(MainMenuBaseView view) : base(view.transform)
    {
        _view = view;
        Menu.Clear();
        BuildMenu();
    }

    protected override void BuildMenu()
    {
        if (_view == null) return;

        AddPlaymodeOptions();

        var heroButton = GetField<BazaarButtonController>("_heroSelectButton");
        if (heroButton != null)
        {
            Menu.AddOption(
                () => Loc.T("screen.mainmenu.hero", ButtonLabel(heroButton)),
                () => heroButton.onClick.Invoke(),
                () => IsUsable(heroButton));
        }

        var playButton = GetField<BazaarButtonController>("_playButton");
        if (playButton != null)
        {
            Menu.AddOption(
                () => playButton.interactable ? ButtonLabel(playButton) : Loc.T("screen.mainmenu.unavailable", ButtonLabel(playButton)),
                () => { if (playButton.interactable) playButton.onClick.Invoke(); },
                () => playButton.gameObject.activeInHierarchy);
        }

        var loadoutButton = GetField<BazaarButtonController>("_loadoutButton");
        if (loadoutButton != null)
        {
            Menu.AddOption(
                () => ButtonLabel(loadoutButton),
                () => loadoutButton.onClick.Invoke(),
                () => IsUsable(loadoutButton));
        }

        var demoNotice = GetField<GameObject>("_demoNotice");
        if (demoNotice != null)
        {
            var buyButton = demoNotice.GetComponentInChildren<BazaarButtonController>(true);
            if (buyButton != null)
            {
                Menu.AddOption(
                    () => LabelOr(buyButton, "screen.mainmenu.buy_full_game"),
                    () => buyButton.onClick.Invoke(),
                    () => demoNotice.activeInHierarchy);
            }
        }

        // Top-right icon buttons carry a hidden "Button Large" placeholder text, so they get fixed labels.
        // Prize Pass is not added here: it is in the scene list, and its button only reacts to real mouse clicks.
        var settings = GetField<Selectable>("_settings");
        if (settings != null)
        {
            Menu.AddOption(
                () => Loc.T("screen.mainmenu.settings"),
                () => Activate(settings),
                () => IsUsable(settings));
        }

        // Enter opens the gem store, like clicking the gem counter (GemHUDViewComponent.OnGemButtonClick).
        Menu.AddOption(() => Loc.T("screen.mainmenu.gems", HudAmount<GemHUDViewComponent>()), () =>
        {
            var hud = UnityEngine.Object.FindObjectOfType<GemHUDViewComponent>();
            if (hud != null) AccessTools.Method(typeof(GemHUDViewComponent), "OnGemButtonClick")?.Invoke(hud, null);
        });

        // The chest store (ChestsHUDViewComponent.ShowPurchaseChestsPrompt), read by PurchasePanelUI.
        Menu.AddOption(() => Loc.T("screen.mainmenu.buy_chests"), () => UnityEngine.Object.FindObjectOfType<ChestsHUDViewComponent>()?.ShowPurchaseChestsPrompt());

        // Challenges is its own scene with no View: open it and attach a challenges-only screen once loaded.
        var challenges = GetField<Selectable>("_challenges");
        if (challenges != null)
        {
            Menu.AddOption(
                () => Loc.T("screen.battlepass.challenges"),
                () =>
                {
                    Activate(challenges);
                    Plugin.Instance.StartCoroutine(AttachChallengesScreen());
                },
                () => IsUsable(challenges));
        }

        // Chests now open inside the Collection scene, from the top-bar counter (ChestsHUDViewComponent.RequestChestScene).
        Menu.AddOption(
            () => Loc.T("screen.mainmenu.with_count", Loc.T("screen.chestscene.name"),
                Loc.Plural("screen.battlepass.chest_count", HudAmount<ChestsHUDViewComponent>(), HudAmount<ChestsHUDViewComponent>())),
            () =>
            {
                var hud = UnityEngine.Object.FindObjectOfType<ChestsHUDViewComponent>();
                if (hud != null) AccessTools.Method(typeof(ChestsHUDViewComponent), "RequestChestScene")?.Invoke(hud, null);
            });
        AddSceneOptions();
    }

    public override bool IsValid() => _view != null && _view.isActiveAndEnabled;

    // Rebuild on focus: the navigation menu and play modes finish loading after the view enables.
    public override void OnFocus()
    {
        Menu.Clear();
        BuildMenu();
        base.OnFocus();
    }

    // --- Play modes (Casual / Ranked / Tournament) ---

    private void AddPlaymodeOptions()
    {
        var selection = GetField<PlaymodeSelectionViewComponent>("_playmodeSelectionView");
        if (selection == null) return;

        var options = AccessTools.Field(typeof(PlaymodeSelectionViewComponent), "_playmodeOptions")?.GetValue(selection) as IEnumerable;
        if (options == null) return;

        foreach (var option in options)
        {
            var state = (EMainMenuState)Traverse.Create(option).Field("PlayState").GetValue();
            var toggle = Traverse.Create(option).Field("Toggle").GetValue() as Toggle;
            if (toggle == null) continue;

            Menu.AddOption(
                () => DescribeMode(state, toggle),
                () => SelectMode(state, toggle),
                () => IsUsable(toggle));
        }
    }

    private static string ModeLabel(EMainMenuState state, Toggle toggle)
    {
        string label = toggle.GetComponentInChildren<TMP_Text>(true)?.text;
        label = TextHelper.CleanText(label);
        return string.IsNullOrEmpty(label) ? Loc.T("screen.mainmenu.mode." + state.ToString().ToLowerInvariant()) : label;
    }

    private static string DescribeMode(EMainMenuState state, Toggle toggle)
    {
        string text = ModeLabel(state, toggle);
        if (toggle.isOn) text = Loc.T("screen.mainmenu.option_selected", text);
        if (state == EMainMenuState.Ranked)
        {
            string rank = ItemReader.GetPlayerRank();
            if (!string.IsNullOrEmpty(rank)) text = Loc.T("screen.mainmenu.with_rank", text, rank);
        }
        return text;
    }

    private static void SelectMode(EMainMenuState state, Toggle toggle)
    {
        if (!toggle.isOn) toggle.isOn = true;
        TolkWrapper.Speak(Loc.T("screen.mainmenu.mode_selected", ModeLabel(state, toggle)));
    }

    // --- Top-right shortcuts and the navigation menu's scene list ---

    private void AddSceneOptions()
    {
        var navType = AccessTools.TypeByName("TheBazaar.UI.Menu.NavigationMenuController");
        if (navType == null) return;

        var navMenu = UnityEngine.Object.FindObjectOfType(navType) as IPooledDataProvider;
        var handleSceneChange = AccessTools.Method(AccessTools.TypeByName("TheBazaar.UI.Menu.SceneSelectOptionContainer"), "HandleSceneChange");
        if (navMenu == null || handleSceneChange == null) return;

        foreach (var scene in navMenu.GetData().OfType<TraversableScenesSO.TraversableScene>())
        {
            if (!SupportedScenes.Contains(scene.ID)) continue;

            var id = scene.ID;
            var name = scene.DisplayName;
            Menu.AddOption(
                () => name.GetLocalizedText(),
                () => handleSceneChange.Invoke(null, new object[] { id, null }));
        }
    }

    private static System.Collections.IEnumerator AttachChallengesScreen()
    {
        const float pollSeconds = 0.25f;
        const int maxPolls = 40; // scene loads take a few seconds
        for (int i = 0; i < maxPolls; i++)
        {
            yield return new WaitForSecondsRealtime(pollSeconds);
            if (SceneLoader.ActiveScene != SceneID.Challenges) continue;
            var view = UnityEngine.Object.FindObjectOfType<PrizePassChallengesView>();
            if (view == null) continue;
            AccessibilityMgr.SetScreen(new BattlePassScreen(view.transform, null, challengesOnly: true));
            yield break;
        }
    }

    // --- Helpers ---

    private T GetField<T>(string name) where T : class
        => AccessTools.Field(typeof(MainMenuBaseView), name)?.GetValue(_view) as T;

    // Amount shown by a top-bar counter (gems, chests); the game keeps it in the protected _resourceAmount.
    private static int HudAmount<T>() where T : HUDResourceViewComponent
    {
        var hud = UnityEngine.Object.FindObjectOfType<T>();
        return hud != null ? (int)AccessTools.Field(typeof(HUDResourceViewComponent), "_resourceAmount").GetValue(hud) : 0;
    }

    private static bool IsUsable(Selectable selectable)
        => selectable != null && selectable.gameObject.activeInHierarchy && selectable.interactable;

    private static string ButtonLabel(BazaarButtonController button)
    {
        string text = button.ButtonText != null ? button.ButtonText.text : null;
        return TextHelper.CleanText(text) ?? string.Empty;
    }

    private static string LabelOr(Component component, string fallbackKey)
    {
        string text = TextHelper.CleanText(component.GetComponentInChildren<TMP_Text>(true)?.text);
        return string.IsNullOrEmpty(text) ? Loc.T(fallbackKey) : text;
    }

    private static void Activate(Selectable selectable)
    {
        switch (selectable)
        {
            case Button button:
                button.onClick.Invoke();
                break;
            case Toggle toggle:
                toggle.isOn = !toggle.isOn;
                break;
        }
    }
}
