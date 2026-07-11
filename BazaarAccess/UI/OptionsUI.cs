using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BazaarAccess.Accessibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// Accessible options menu. Builds a flat, section-headed list from the dialog's live controls.
/// See Progress.md ("Options dialog rework") for the ScrollSpy rationale.
/// </summary>
public class OptionsUI : BaseUI
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private struct SectionEntry
    {
        public Transform Nav;
        public Transform Section;
    }

    public override string UIName => "Options";

    public OptionsUI(Transform root) : base(root)
    {
    }

    protected override void BuildMenu()
    {
        if (Root == null) return;

        // Rebind + reset-to-default buttons are exposed via the keybind option, not as buttons.
        var keybindButtons = CollectKeybindOwnedButtons();
        int added = 0;

        var sections = GetScrollSpySections();
        if (sections != null && sections.Count > 0)
        {
            // Tab rows and section panels are covered per-section; exclude them from the footer sweep.
            var skipRoots = new List<Transform>();
            foreach (var entry in sections)
            {
                if (entry.Nav != null) skipRoots.Add(entry.Nav);
                if (entry.Section != null) skipRoots.Add(entry.Section);
            }

            foreach (var entry in sections)
            {
                if (entry.Section == null || !entry.Section.gameObject.activeInHierarchy) continue;

                // Header goes at the current menu size (counts headers already inserted, not `added`).
                int headerIndex = Menu.OptionCount;
                int sectionControls = BuildControlsUnder(entry.Section, keybindButtons);
                if (sectionControls > 0)
                {
                    InsertHeaderBefore(headerIndex, GetSectionHeader(entry));
                    added += sectionControls;
                }
            }

            // Footer controls (Save/Close/Exit) that live outside any section.
            added += BuildControlsUnder(Root, keybindButtons, skipRoots);
        }
        else
        {
            // No ScrollSpy: flat sweep of the whole dialog.
            added += BuildControlsUnder(Root, keybindButtons);
        }

        Plugin.Logger.LogInfo($"OptionsUI: built {added} options dynamically");
        if (added == 0)
            Plugin.Logger.LogWarning("OptionsUI: no active control found in the dialog");
    }

    // === Rebuild ===

    public void RebuildMenu()
    {
        if (Root == null || !Root.gameObject.activeInHierarchy) return;

        // Skip while the dialog is fading out (still active but no longer interactable).
        var canvasGroup = Root.GetComponent<CanvasGroup>();
        if (canvasGroup != null && (!canvasGroup.interactable || canvasGroup.alpha < 0.5f)) return;

        Menu.Clear();
        BuildMenu();
        Menu.StartReading(announceMenuName: true);
    }

    private IEnumerator RebuildAfterDelay()
    {
        // A new section's controls activate a frame or two after the click.
        yield return null;
        yield return null;
        RebuildMenu();
    }

    // === Control building ===

    /// <summary>
    /// Adds one option per interactable control under <paramref name="root"/>, in hierarchy order.
    /// Returns the count added. Skips anything under <paramref name="skipRoots"/>.
    /// </summary>
    private int BuildControlsUnder(Transform root, HashSet<Button> keybindButtons, List<Transform> skipRoots = null)
    {
        int added = 0;
        // includeInactive:false → only active GameObjects, in hierarchy order (preserves visual order).
        foreach (var t in root.GetComponentsInChildren<Transform>(false))
        {
            if (skipRoots != null && IsUnderAny(t, skipRoots)) continue;
            if (TryAddControl(t.gameObject, keybindButtons)) added++;
        }
        return added;
    }

    // Uses IsInteractable() (effective, respects CanvasGroups) rather than the serialized flag.
    private bool TryAddControl(GameObject go, HashSet<Button> keybindButtons)
    {
        var keybind = GetKeybindController(go);
        if (keybind != null)
        {
            var kbButton = GetKeybindButton(keybind);
            if (kbButton == null || kbButton.IsInteractable())
            {
                AddKeybindOption(keybind);
                return true;
            }
            return false;
        }

        var slider = go.GetComponent<Slider>();
        if (slider != null && slider.IsInteractable())
        {
            AddSliderOption(slider);
            return true;
        }

        var dropdown = go.GetComponent<TMP_Dropdown>();
        if (dropdown != null && dropdown.IsInteractable())
        {
            AddDropdownOption(dropdown);
            return true;
        }

        var toggle = go.GetComponent<Toggle>();
        if (toggle != null && toggle.IsInteractable() && !IsInsideDropdown(go))
        {
            AddToggleOption(toggle);
            return true;
        }

        var button = go.GetComponent<Button>();
        if (button != null && button.IsInteractable() && !keybindButtons.Contains(button))
        {
            AddButtonOption(button);
            return true;
        }

        return false;
    }

    // === Sections (ScrollSpy) ===

    // Reads the (tab, panel) pairs from ScrollSpyController._entries by reflection. Null if absent.
    private List<SectionEntry> GetScrollSpySections()
    {
        var spy = Root.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(m => m != null && m.GetType().Name == "ScrollSpyController");
        if (spy == null) return null;

        var entriesValue = spy.GetType().GetField("_entries", PrivateInstance)?.GetValue(spy);
        if (!(entriesValue is IEnumerable entries)) return null;

        var result = new List<SectionEntry>();
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            var type = entry.GetType();
            result.Add(new SectionEntry
            {
                Nav = type.GetField("NavButtonRoot")?.GetValue(entry) as Transform,
                Section = type.GetField("SectionRoot")?.GetValue(entry) as Transform
            });
        }
        return result;
    }

    private string GetSectionHeader(SectionEntry entry)
    {
        string label = entry.Nav != null ? FirstText(entry.Nav) : null;
        if (string.IsNullOrEmpty(label) && entry.Section != null)
            label = FirstText(entry.Section) ?? entry.Section.gameObject.name;
        return label;
    }

    // Inserts a non-interactive section header just before that section's controls.
    private void InsertHeaderBefore(int index, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        Menu.InsertOption(index, new MenuOption(() => text, null));
    }

    // === Option builders ===

    private void AddSliderOption(Slider slider)
    {
        Menu.AddOption(
            () => GetSliderText(slider),
            null,
            null,
            (direction) => AdjustSlider(slider, direction));
    }

    private void AddToggleOption(Toggle toggle)
    {
        Menu.AddOption(
            () => GetToggleText(toggle),
            () => ToggleValue(toggle),
            null,
            (direction) => ToggleValue(toggle));
    }

    private void AddDropdownOption(TMP_Dropdown dropdown)
    {
        // Browse with arrows without committing; apply on Enter. Committing on every arrow made
        // the language dropdown re-localize / prompt a restart on each step.
        int pending = dropdown.value;
        Menu.AddOption(
            () => GetDropdownText(dropdown, pending),
            () => CommitDropdown(dropdown, pending),
            null,
            (direction) =>
            {
                int count = dropdown.options?.Count ?? 0;
                if (count == 0) return;
                int next = Mathf.Clamp(pending + direction, 0, count - 1);
                if (next == pending) return;
                pending = next;
                Core.TolkWrapper.Speak(GetDropdownOptionText(dropdown, pending));
            });
    }

    private void AddButtonOption(Button button)
    {
        Menu.AddOption(
            () => GetButtonText(button),
            () =>
            {
                button.onClick.Invoke();
                // The button may open another panel or close the dialog; RebuildMenu guards both.
                Plugin.Instance.StartCoroutine(RebuildAfterDelay());
            });
    }

    private void AddKeybindOption(MonoBehaviour keybindController)
    {
        Menu.AddOption(
            () => GetKeybindLabel(keybindController),
            () => GetKeybindButton(keybindController)?.onClick.Invoke());
    }

    // === Labels and values ===

    private string GetSliderText(Slider slider)
    {
        string label = LabelFromParent(slider.transform) ?? slider.gameObject.name;
        int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
        return $"{label}: {percent}%";
    }

    private string GetToggleText(Toggle toggle)
    {
        string label = NearbyLabel(toggle.transform) ?? toggle.gameObject.name;
        string state = toggle.isOn ? "on" : "off";
        return $"{label}: {state}";
    }

    private string GetDropdownText(TMP_Dropdown dropdown, int index)
    {
        string value = GetDropdownOptionText(dropdown, index);
        string label = LabelFromParent(dropdown.transform, exclude: value) ?? dropdown.gameObject.name;
        return $"{label}: {value}";
    }

    private string GetDropdownOptionText(TMP_Dropdown dropdown, int index)
    {
        var options = dropdown.options;
        if (options != null && index >= 0 && index < options.Count)
            return options[index].text?.Trim() ?? "";
        return "";
    }

    private string GetButtonText(Button button)
    {
        var tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();
        return button.gameObject.name;
    }

    // === Value adjustment ===

    private void AdjustSlider(Slider slider, int direction)
    {
        float step = 0.1f;
        float newNormalized = Mathf.Clamp01(slider.normalizedValue + (step * direction));

        // Map normalized value (0-1) back to the slider's real range.
        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, newNormalized);
        slider.onValueChanged?.Invoke(slider.value);

        int percent = Mathf.RoundToInt(newNormalized * 100);
        Core.TolkWrapper.Speak($"{percent}%");
    }

    private void ToggleValue(Toggle toggle)
    {
        // Setting isOn fires onValueChanged, so the game applies the change.
        toggle.isOn = !toggle.isOn;
        Core.TolkWrapper.Speak(toggle.isOn ? "on" : "off");
    }

    private void CommitDropdown(TMP_Dropdown dropdown, int index)
    {
        int count = dropdown.options?.Count ?? 0;
        if (index < 0 || index >= count) return;

        // Setting value fires onValueChanged → the game applies it (may prompt a restart).
        if (dropdown.value != index)
            dropdown.value = index;
        Core.TolkWrapper.Speak(GetDropdownOptionText(dropdown, index));
    }

    // === Keybinds ===

    private Button GetKeybindButton(MonoBehaviour keybindController)
    {
        var field = keybindController.GetType().GetField("_keybindButton", PrivateInstance);
        return field?.GetValue(keybindController) as Button;
    }

    // Reads the action name + current key from KeyBindController (new Input System) by reflection.
    private string GetKeybindLabel(MonoBehaviour keybindController)
    {
        string actionName = "Keybind";
        var ctrlType = keybindController.GetType();
        var actionValue =
            ctrlType.GetField("_resolvedAction", PrivateInstance)?.GetValue(keybindController)
            ?? ctrlType.GetField("_action", PrivateInstance)?.GetValue(keybindController);

        if (actionValue != null)
        {
            var rawName = actionValue.GetType().GetProperty("name")?.GetValue(actionValue) as string;
            if (!string.IsNullOrEmpty(rawName))
            {
                // Name may be "Map/Action"; keep the last segment.
                int slash = rawName.LastIndexOf('/');
                if (slash >= 0 && slash < rawName.Length - 1)
                    rawName = rawName.Substring(slash + 1);
                actionName = ConvertActionToReadableName(rawName);
            }
        }

        string keyText = "";
        var textField = ctrlType.GetField("_keybindText", PrivateInstance);
        if (textField != null && textField.GetValue(keybindController) is TMP_Text tmpText)
        {
            keyText = tmpText.text?.Trim() ?? "";
        }

        return string.IsNullOrEmpty(keyText) ? actionName : $"{actionName}: {keyText}";
    }

    private string ConvertActionToReadableName(string actionEnum)
    {
        return actionEnum switch
        {
            "ToggleStash" => "Toggle Stash",
            "Settings" => "Access Settings",
            "Lock" => "Access Locked Tooltip",
            "Playlist" => "Playlist",
            "Atlas" => "Atlas",
            _ => actionEnum
        };
    }

    private HashSet<Button> CollectKeybindOwnedButtons()
    {
        var owned = new HashSet<Button>();
        var controllers = Root.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(m => m != null && m.GetType().Name == "KeyBindController");

        foreach (var controller in controllers)
        {
            var type = controller.GetType();
            if (type.GetField("_keybindButton", PrivateInstance)?.GetValue(controller) is Button rebind)
                owned.Add(rebind);
            if (type.GetField("_resetToDefaultButton", PrivateInstance)?.GetValue(controller) is Button reset)
                owned.Add(reset);
        }

        return owned;
    }

    private MonoBehaviour GetKeybindController(GameObject go)
    {
        return go.GetComponents<MonoBehaviour>()
            .FirstOrDefault(m => m != null && m.GetType().Name == "KeyBindController");
    }

    // === Label helpers ===

    // Label text under the control's parent (typical [Label][Control] row). Skips `exclude`
    // (e.g. a dropdown caption, which is the value) and the control's own inner text.
    private string LabelFromParent(Transform t, string exclude = null)
    {
        var parent = t.parent;
        if (parent == null) return null;

        foreach (var tmp in parent.GetComponentsInChildren<TMP_Text>(false))
        {
            if (tmp.transform.IsChildOf(t)) continue;

            string text = tmp.text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!string.IsNullOrEmpty(exclude) && text == exclude) continue;

            return text;
        }
        return null;
    }

    // Toggle label: child text first (label as child), else a sibling under the same parent.
    private string NearbyLabel(Transform t)
    {
        var childTmp = t.GetComponentInChildren<TMP_Text>();
        if (childTmp != null && !string.IsNullOrWhiteSpace(childTmp.text))
            return childTmp.text.Trim();

        return LabelFromParent(t);
    }

    private string FirstText(Transform t)
    {
        foreach (var tmp in t.GetComponentsInChildren<TMP_Text>(false))
        {
            string text = tmp.text?.Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }

    private bool IsInsideDropdown(GameObject go)
    {
        return go.GetComponentInParent<TMP_Dropdown>() != null;
    }

    private bool IsUnderAny(Transform t, List<Transform> roots)
    {
        foreach (var r in roots)
            if (r != null && t.IsChildOf(r)) return true;
        return false;
    }

    // === Back ===

    protected override void OnBack()
    {
        // Delegate to the dialog's own back logic (closes an open sub-panel, else closes the dialog).
        var controller = FindOptionsController();
        if (controller != null)
        {
            var method = controller.GetType().GetMethod("OnBackButtonClicked",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(controller, null);
                Plugin.Instance.StartCoroutine(RebuildAfterDelay());
                return;
            }
        }

        if (!ClickButtonByName("Btn_Close"))
            ClickButtonByName("CloseButton");
    }

    private MonoBehaviour FindOptionsController()
    {
        if (Root == null) return null;
        return Root.GetComponents<MonoBehaviour>()
            .FirstOrDefault(m => m != null && m.GetType().Name == "OptionsDialogController");
    }
}
