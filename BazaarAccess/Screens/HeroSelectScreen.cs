using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;
using TheBazaar.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Screens;

/// <summary>
/// Hero selection screen.
/// Shows available heroes, game mode selection, and play button.
/// Arrow navigation reads full hero info (name, title, description).
/// </summary>
public class HeroSelectScreen : BaseScreen
{
    public override string ScreenName => "Hero Select";

    private readonly List<HeroItemView> _heroViews = new List<HeroItemView>();

    public HeroSelectScreen(Transform root) : base(root)
    {
    }

    protected override void BuildMenu()
    {
        // Find all HeroItemView components in the scene
        FindHeroViews();

        // Add hero selection options with visibility callback for async loading
        foreach (var heroView in _heroViews)
        {
            if (heroView == null) continue;

            var view = heroView; // Capture for closure
            Menu.AddOption(
                () => GetHeroOptionText(view),
                () => SelectHero(view),
                () => view != null && view.gameObject.activeInHierarchy);
        }

        // Random Hero toggle
        AddRandomHeroToggle();

        // Game mode buttons with selection state
        AddGameModeButtons();

        // Ready button (for starting the run)
        AddReadyButton();

        // Back button - use custom method because it's a ButtonCustom without text
        AddBackButton();

        // Fallback if no buttons found
        if (Menu.OptionCount == 0)
        {
            TryAddGenericButtons();
        }
    }

    /// <summary>
    /// Adds the Random Hero toggle option.
    /// </summary>
    private void AddRandomHeroToggle()
    {
        var heroButtonsView = Object.FindObjectOfType<HeroSelectButtonsView>();
        if (heroButtonsView == null) return;

        var toggleField = typeof(HeroSelectButtonsView).GetField("RandomHeroToggle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (toggleField == null) return;

        var toggle = toggleField.GetValue(heroButtonsView) as Toggle;
        if (toggle == null) return;

        Menu.AddOption(
            () => $"Random Hero: {(HeroSelectButtonsView.IsRandomHeroEnabled ? "on" : "off")}",
            () =>
            {
                int currentIndex = Menu.CurrentIndex;
                toggle.isOn = !toggle.isOn;
                TolkWrapper.Speak($"Random Hero: {(toggle.isOn ? "on" : "off")}");
                Menu.Clear();
                BuildMenu();
                Menu.SetIndex(currentIndex);
            },
            () => toggle != null && toggle.gameObject.activeInHierarchy && toggle.interactable);
    }

    /// <summary>
    /// Adds game mode buttons (Casual and Ranked) with selection state.
    /// </summary>
    private void AddGameModeButtons()
    {
        // Find PlaymodeSelectionViewComponent
        var playmodeView = Object.FindObjectOfType<PlaymodeSelectionViewComponent>();
        if (playmodeView == null)
        {
            Plugin.Logger.LogInfo("HeroSelectScreen: PlaymodeSelectionViewComponent not found");
            // Fallback to standard buttons
            AddButtonIfExists("Btn_Casual", "_casualButton");
            AddButtonIfExists("Btn_Ranked", "_rankedButton");
            return;
        }

        // Get the buttons via reflection
        var casualButtonField = typeof(PlaymodeSelectionViewComponent).GetField("_casualButton",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var rankedButtonField = typeof(PlaymodeSelectionViewComponent).GetField("_rankedButton",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var casualButton = casualButtonField?.GetValue(playmodeView) as BazaarButtonController;
        var rankedButton = rankedButtonField?.GetValue(playmodeView) as BazaarButtonController;

        // Add Casual button
        if (casualButton != null && casualButton.gameObject.activeInHierarchy)
        {
            var btn = casualButton;
            Menu.AddOption(
                () => GetCasualButtonText(),
                () =>
                {
                    int currentIndex = Menu.CurrentIndex;
                    btn.onClick.Invoke();
                    // Rebuild to update selection state but keep position
                    Menu.Clear();
                    BuildMenu();
                    Menu.SetIndex(currentIndex);
                    TolkWrapper.Speak("Casual selected");
                });
        }

        // Add Ranked button
        if (rankedButton != null && rankedButton.gameObject.activeInHierarchy)
        {
            var btn = rankedButton;
            Menu.AddOption(
                () => GetRankedButtonText(),
                () =>
                {
                    int currentIndex = Menu.CurrentIndex;
                    btn.onClick.Invoke();
                    // Rebuild to update selection state but keep position
                    Menu.Clear();
                    BuildMenu();
                    Menu.SetIndex(currentIndex);
                    string rank = Gameplay.ItemReader.GetPlayerRank();
                    if (!string.IsNullOrEmpty(rank))
                        TolkWrapper.Speak($"Ranked selected. Rank: {rank}");
                    else
                        TolkWrapper.Speak("Ranked selected");
                });
        }
    }

    private string GetCasualButtonText()
    {
        try
        {
            if (Data.RunConfiguration != null && Data.RunConfiguration.RunType == EPlayMode.Unranked)
            {
                return "Casual, selected";
            }
        }
        catch { }
        return "Casual";
    }

    private string GetRankedButtonText()
    {
        try
        {
            if (Data.RunConfiguration != null && Data.RunConfiguration.RunType == EPlayMode.Ranked)
            {
                string rank = Gameplay.ItemReader.GetPlayerRank();
                if (!string.IsNullOrEmpty(rank))
                    return $"Ranked, selected. Rank: {rank}";
                return "Ranked, selected";
            }
        }
        catch { }
        return "Ranked";
    }

    /// <summary>
    /// Adds the Ready/Resume button with contextual text.
    /// </summary>
    private void AddReadyButton()
    {
        var readyButtonComponent = Object.FindObjectOfType<PlaymodeReadyButtonComponent>();
        if (readyButtonComponent == null)
        {
            // Fallback
            AddButtonIfExists("Btn_Ready", "ReadyButton");
            return;
        }

        var readyButton = readyButtonComponent.ReadyButton;
        if (readyButton == null || !readyButton.gameObject.activeInHierarchy)
        {
            return;
        }

        var btn = readyButton;
        Menu.AddOption(
            () => GetReadyButtonText(readyButtonComponent),
            () => btn.onClick.Invoke());
    }

    /// <summary>
    /// Gets the ready button text with context (Resume, Ready, etc.)
    /// </summary>
    private string GetReadyButtonText(PlaymodeReadyButtonComponent component)
    {
        // Try to get the button text
        var textField = typeof(PlaymodeReadyButtonComponent).GetField("_readyButtonText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (textField != null)
        {
            var tmpText = textField.GetValue(component) as TMPro.TextMeshProUGUI;
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                return tmpText.text;
            }
        }

        // Fallback based on game state
        if (Data.HasActiveRun)
        {
            return "Resume";
        }

        return "Ready";
    }

    /// <summary>
    /// Adds the back button with a fixed "Back" label.
    /// The back button is a ButtonCustom that may not have visible text.
    /// </summary>
    private void AddBackButton()
    {
        // First try standard button names
        string[] standardNames = { "Btn_Back", "BackButton" };
        foreach (var name in standardNames)
        {
            string text = GetButtonTextByName(name);
            if (!string.IsNullOrEmpty(text) && text != name && !text.ToLower().Contains("button"))
            {
                var buttonName = name;
                Menu.AddOption(
                    () => GetButtonTextByName(buttonName),
                    () => ClickButtonByName(buttonName));
                return;
            }
        }

        // Look for ButtonCustom named backButton (like in HeroSelectView)
        var buttonCustoms = Object.FindObjectsOfType<ButtonCustom>();
        foreach (var bc in buttonCustoms)
        {
            string goName = bc.gameObject.name.ToLower();
            if (goName.Contains("back") && bc.gameObject.activeInHierarchy)
            {
                var button = bc; // Capture for closure
                Menu.AddOption(
                    () => "Back",
                    () => button.OnMouseClickCustom());
                Plugin.Logger.LogInfo($"HeroSelectScreen: Added back ButtonCustom: {bc.gameObject.name}");
                return;
            }
        }

        // Last resort: look for any button that might be a back button by searching all buttons
        var allButtons = Object.FindObjectsOfType<UnityEngine.UI.Button>();
        foreach (var btn in allButtons)
        {
            string goName = btn.gameObject.name.ToLower();
            if ((goName.Contains("back") || goName.Contains("return") || goName.Contains("home"))
                && btn.gameObject.activeInHierarchy && btn.interactable)
            {
                var button = btn;
                Menu.AddOption(
                    () => "Back",
                    () => button.onClick.Invoke());
                Plugin.Logger.LogInfo($"HeroSelectScreen: Added back button: {btn.gameObject.name}");
                return;
            }
        }
    }

    private void FindHeroViews()
    {
        _heroViews.Clear();

        // Find HeroSelectButtonsView which contains all hero buttons
        var heroButtonsView = Object.FindObjectOfType<TheBazaar.UI.HeroSelectButtonsView>();
        if (heroButtonsView != null)
        {
            // Get HeroItemViews from HeroSelectButtonsView via reflection
            var field = heroButtonsView.GetType().GetField("HeroItemViews",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var views = field.GetValue(heroButtonsView) as List<HeroItemView>;
                if (views != null)
                {
                    // Include all heroes from the serialized list, even if inactive.
                    // Heroes may be hidden during async loading (RefreshButtons).
                    // Visibility is handled per-option via the menu's visible callback.
                    _heroViews.AddRange(views.Where(v => v != null));
                }
            }
        }

        // Fallback: find all HeroItemViews in the scene
        if (_heroViews.Count == 0)
        {
            var allHeroViews = Object.FindObjectsOfType<HeroItemView>();
            _heroViews.AddRange(allHeroViews.Where(v => v != null && v.gameObject.activeInHierarchy));
        }

        Plugin.Logger.LogInfo($"HeroSelectScreen: Found {_heroViews.Count} hero views");
    }

    private string GetHeroOptionText(HeroItemView view)
    {
        if (view == null) return "Unknown Hero";

        string heroName = view.Hero.ToString();
        bool isSelected = Data.SelectedHero == view.Hero;
        bool isLocked = !IsHeroUnlocked(view);

        var parts = new List<string> { heroName };

        // Get HeroSO for title and description
        var heroSO = GetHeroSO(view);
        if (heroSO != null)
        {
            // Add title if available and not a placeholder
            try
            {
                string title = heroSO.Title;
                if (!string.IsNullOrEmpty(title) &&
                    !title.Equals("Title", System.StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(title);
                }
            }
            catch { }

            // Add description if available and not a placeholder
            try
            {
                string description = heroSO.Description;
                if (!string.IsNullOrEmpty(description) &&
                    !description.Equals("Description", System.StringComparison.OrdinalIgnoreCase) &&
                    !description.Equals("Title", System.StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(description);
                }
            }
            catch { }
        }

        if (isSelected && !HeroSelectButtonsView.IsRandomHeroEnabled)
        {
            parts.Add("selected");
        }

        if (isLocked)
        {
            parts.Add("locked");
        }

        return string.Join(". ", parts);
    }

    private bool IsHeroUnlocked(HeroItemView view)
    {
        // Check private _isUnlocked field via reflection
        var field = view.GetType().GetField("_isUnlocked",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (bool)field.GetValue(view);
        }
        return false;
    }

    private void SelectHero(HeroItemView view)
    {
        if (view == null) return;

        bool isLocked = !IsHeroUnlocked(view);

        if (isLocked)
        {
            TolkWrapper.Speak($"{view.Hero} is locked");
            // This will open the hero unlock/purchase dialog
            view.OnItemSelected();
        }
        else
        {
            // Remember current position
            int currentIndex = Menu.CurrentIndex;

            // Selecting a hero disables random hero mode automatically (game handles this)
            view.OnItemSelected();
            TolkWrapper.Speak($"{view.Hero} selected");

            // Rebuild menu to update selection indicator and random hero state
            Menu.Clear();
            BuildMenu();
            Menu.SetIndex(currentIndex);
        }
    }

    private void AddButtonIfExists(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            string text = GetButtonTextByName(name);
            // Skip if text is empty, same as name, or a generic button name
            if (!string.IsNullOrEmpty(text) && text != name && !IsGenericButtonName(text))
            {
                var buttonName = name; // Capture for closure
                Menu.AddOption(
                    () => GetButtonTextByName(buttonName),
                    () => ClickButtonByName(buttonName));
                return;
            }
        }
    }

    /// <summary>
    /// Checks if a button text is a generic/useless name like "Button Large".
    /// </summary>
    private bool IsGenericButtonName(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;

        string lower = text.ToLower().Trim();

        // List of generic button names that don't provide useful information
        string[] genericNames = {
            "button", "button large", "button small", "button medium",
            "btn", "click", "press", "submit"
        };

        foreach (var generic in genericNames)
        {
            if (lower == generic || lower.StartsWith(generic + " ") || lower.EndsWith(" " + generic))
            {
                return true;
            }
        }

        return false;
    }

    private void TryAddGenericButtons()
    {
        // Try common button patterns
        var buttonPatterns = new[]
        {
            "Ready", "Play", "Start",
            "Casual", "Ranked",
            "Back", "Return"
        };

        foreach (var pattern in buttonPatterns)
        {
            // Try various naming conventions
            var names = new[]
            {
                pattern,
                $"Btn_{pattern}",
                $"Button_{pattern}",
                $"{pattern}Button",
                $"{pattern}Btn"
            };

            foreach (var name in names)
            {
                string text = GetButtonTextByName(name);
                // Skip generic button names
                if (!string.IsNullOrEmpty(text) && text != name && !IsGenericButtonName(text))
                {
                    var buttonName = name;
                    Menu.AddOption(
                        () => GetButtonTextByName(buttonName),
                        () => ClickButtonByName(buttonName));
                    break;
                }
            }
        }
    }

    public override void OnFocus()
    {
        if (HeroSelectButtonsView.IsRandomHeroEnabled)
        {
            TolkWrapper.Speak("Hero Select. Random hero mode enabled");
        }
        else
        {
            string currentHero = Data.SelectedHero.ToString();
            TolkWrapper.Speak($"Hero Select. Current hero: {currentHero}");
        }
        // Don't call base.OnFocus() to avoid double announcement
    }

    private HeroSO GetHeroSO(HeroItemView view)
    {
        // Get private HeroSO field via reflection
        var field = view.GetType().GetField("HeroSO",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return field.GetValue(view) as HeroSO;
        }
        return null;
    }
}
