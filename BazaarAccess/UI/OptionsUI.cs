using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para el menú de opciones.
/// Detecta automáticamente si estamos en la sección principal o en gameplay settings.
/// </summary>
public class OptionsUI : BaseUI
{
    private bool _inGameplaySection = false;

    public override string UIName => _inGameplaySection ? "Gameplay Settings" : "Options";

    public OptionsUI(Transform root) : base(root)
    {
        DetectCurrentSection();
        LogAllElements();
    }

    /// <summary>
    /// Detecta si estamos en la sección de gameplay settings.
    /// </summary>
    private void DetectCurrentSection()
    {
        // Buscar el overlay de gameplay settings
        var gameplayOverlay = FindGameObject("GameplaySettingsOverlay")
                           ?? FindGameObject("_gameplaySettingsOverlay")
                           ?? FindGameObject("GameplayOverlay");

        // También buscar el botón de volver que solo aparece en gameplay settings
        var backButton = FindButtonByName("Btn_Back")
                      ?? FindButtonByName("_gameplayBackButton")
                      ?? FindButtonByName("Btn_GameplayBack");

        _inGameplaySection = (gameplayOverlay != null && gameplayOverlay.activeInHierarchy) ||
                            (backButton != null && backButton.gameObject.activeInHierarchy && backButton.interactable);
    }

    private GameObject FindGameObject(string name)
    {
        var all = Root.GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }
        return null;
    }

    private void LogAllElements()
    {
        Plugin.Logger.LogInfo($"=== OptionsUI elementos (Gameplay: {_inGameplaySection}) ===");

        var buttons = Root.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                string text = GetComponentText(btn.transform);
                Plugin.Logger.LogInfo($"[Btn ACTIVO] {btn.gameObject.name}: '{text}'");
            }
        }

        var toggles = Root.GetComponentsInChildren<Toggle>(true);
        foreach (var toggle in toggles)
        {
            if (toggle.gameObject.activeInHierarchy)
            {
                string text = GetComponentText(toggle.transform);
                Plugin.Logger.LogInfo($"[Toggle] {toggle.gameObject.name}: '{text}' = {toggle.isOn}");
            }
        }

        var sliders = Root.GetComponentsInChildren<Slider>(true);
        foreach (var slider in sliders)
        {
            if (slider.gameObject.activeInHierarchy)
            {
                string text = GetComponentText(slider.transform.parent);
                Plugin.Logger.LogInfo($"[Slider] {slider.gameObject.name}: '{text}' = {slider.value:F2}");
            }
        }

        var dropdowns = Root.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (var dd in dropdowns)
        {
            if (dd.gameObject.activeInHierarchy)
            {
                string caption = dd.captionText?.text ?? "";
                Plugin.Logger.LogInfo($"[Dropdown] {dd.gameObject.name}: '{caption}'");
            }
        }

        Plugin.Logger.LogInfo("=== Fin OptionsUI ===");
    }

    private string GetComponentText(Transform t)
    {
        if (t == null) return "";
        var tmp = t.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();
        return "";
    }

    protected override void BuildMenu()
    {
        if (_inGameplaySection)
        {
            BuildGameplayMenu();
        }
        else
        {
            BuildMainMenu();
        }
    }

    private void BuildMainMenu()
    {
        // Botón para ir a gameplay settings
        AddSectionButton("Btn_GameplaySettings");

        // === Sliders de audio ===
        AddSliderOption("Slider_Master");
        AddSliderOption("Slider_Music");
        AddSliderOption("Slider_SoundEffects");
        AddSliderOption("Slider_Voice");

        // === Dropdowns de video ===
        AddDropdownOption("Dropdown", "Resolution");
        AddDropdownOptionByIndex(1, "Framerate");

        // === Toggles de video ===
        AddToggleOption("Toggle_Windowed");
        AddToggleOption("Toggle_VSync");
        AddToggleOption("Toggle_SkipVideo");

        // === Botones de acción ===
        AddButtonIfActive("Btn_Privacy");
        AddButtonIfActive("Btn_Terms");
        AddButtonIfActive("Btn_Save");
        AddButtonIfActive("Btn_ExitToDesktop");
        AddButtonIfActive("Btn_Close");
    }

    private void BuildGameplayMenu()
    {
        // Botón de volver (primero para fácil acceso)
        AddBackToMainButton();

        // === Toggles de gameplay/accesibilidad ===
        AddToggleOption("Toggle_Keyword");
        AddToggleOption("Toggle_DisableScreenShake");
        AddToggleOption("Toggle_MotionSensitivity");
        AddToggleOption("Toggle_AlternativeInput");
        AddToggleOption("Toggle_Clock");
        AddToggleOption("Toggle_HealthBarIcons");
        AddToggleOption("Toggle_GemStatusIcons");
        AddToggleOption("Toggle_ClosedCaptions");

        // === Dropdown de idioma ===
        AddDropdownOption("Dropdown_Language", "Language");

        // === Keybinds (si existen) ===
        AddAllKeybindButtons();
    }

    private void AddBackToMainButton()
    {
        // Buscar cualquier botón de volver
        string[] backButtonNames = { "Btn_Back", "Btn_GameplayBack", "_gameplayBackButton" };

        foreach (var name in backButtonNames)
        {
            var button = FindButtonByName(name);
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                Menu.AddOption(
                    () => GetButtonText(name).Length > 0 ? GetButtonText(name) : "Back",
                    () => {
                        ClickButtonByName(name);
                        _inGameplaySection = false;
                        Plugin.Instance.StartCoroutine(RebuildAfterDelay());
                    });
                return;
            }
        }
    }

    private void AddAllKeybindButtons()
    {
        // Buscar todos los KeyBindController en el menú
        var keybindControllers = Root.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(m => m.GetType().Name == "KeyBindController" && m.gameObject.activeInHierarchy);

        foreach (var controller in keybindControllers)
        {
            // Obtener el botón del keybind
            var buttonField = controller.GetType().GetField("_keybindButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var button = buttonField?.GetValue(controller) as Button;

            if (button == null || !button.interactable) continue;

            // Capturar referencias para el closure
            var ctrl = controller;
            var btn = button;

            Menu.AddOption(
                () => GetKeybindLabel(ctrl),
                () => btn.onClick.Invoke());
        }
    }

    private string GetKeybindLabel(MonoBehaviour keybindController)
    {
        // Obtener el nombre de la acción
        string actionName = "Keybind";
        var actionField = keybindController.GetType().GetField("_keybindAction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (actionField != null)
        {
            var actionValue = actionField.GetValue(keybindController);
            if (actionValue != null)
            {
                // Convertir enum a nombre legible
                actionName = ConvertActionToReadableName(actionValue.ToString());
            }
        }

        // Obtener la tecla actual
        string keyText = "";
        var textField = keybindController.GetType().GetField("_keybindText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (textField != null)
        {
            var tmpText = textField.GetValue(keybindController) as TMP_Text;
            if (tmpText != null)
            {
                keyText = tmpText.text?.Trim() ?? "";
            }
        }

        return $"{actionName}: {keyText}";
    }

    private string ConvertActionToReadableName(string actionEnum)
    {
        // Convertir nombres del enum a nombres legibles
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

    public void RebuildMenu()
    {
        Menu.Clear();
        DetectCurrentSection();
        LogAllElements();
        BuildMenu();
        Menu.StartReading(announceMenuName: true);
    }

    private void AddSectionButton(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.gameObject.activeInHierarchy && button.interactable)
        {
            Menu.AddOption(
                () => GetButtonText(name),
                () => {
                    ClickButtonByName(name);
                    _inGameplaySection = true;
                    Plugin.Instance.StartCoroutine(RebuildAfterDelay());
                });
        }
    }

    private System.Collections.IEnumerator RebuildAfterDelay()
    {
        yield return null;
        yield return null;
        RebuildMenu();
    }

    private void AddButtonIfActive(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.gameObject.activeInHierarchy && button.interactable)
        {
            Menu.AddOption(
                () => GetButtonText(name),
                () => ClickButtonByName(name));
        }
    }

    private void AddSliderOption(string name)
    {
        var slider = FindSlider(name);
        if (slider != null && slider.gameObject.activeInHierarchy)
        {
            Menu.AddOption(
                () => GetSliderText(name),
                null,
                null,
                (direction) => AdjustSlider(name, direction));
        }
    }

    private void AddToggleOption(string name)
    {
        var toggle = FindToggle(name);
        if (toggle != null && toggle.gameObject.activeInHierarchy)
        {
            Menu.AddOption(
                () => GetToggleText(name),
                () => ToggleValue(name),
                null,
                (direction) => ToggleValue(name));
        }
    }

    private void AddDropdownOption(string name, string fallbackLabel)
    {
        var dropdown = FindDropdown(name);
        if (dropdown != null && dropdown.gameObject.activeInHierarchy)
        {
            Menu.AddOption(
                () => GetDropdownText(name, fallbackLabel),
                null,
                null,
                (direction) => AdjustDropdown(name, direction));
        }
    }

    private void AddDropdownOptionByIndex(int index, string fallbackLabel)
    {
        var dropdowns = Root.GetComponentsInChildren<TMP_Dropdown>(true)
            .Where(d => d.gameObject.activeInHierarchy)
            .ToArray();

        if (index < dropdowns.Length)
        {
            var dropdown = dropdowns[index];
            string name = dropdown.gameObject.name;
            Menu.AddOption(
                () => GetDropdownText(name, fallbackLabel),
                null,
                null,
                (direction) => AdjustDropdown(name, direction));
        }
    }

    // === Helpers ===

    private TMP_Dropdown FindDropdown(string name)
    {
        return Root.GetComponentsInChildren<TMP_Dropdown>(true)
            .FirstOrDefault(d => d.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    private string GetSliderText(string name)
    {
        var slider = FindSlider(name);
        if (slider == null) return name;

        string label = GetSliderLabel(name);
        // Usar normalizedValue para obtener el porcentaje correcto (0-100%)
        int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
        return $"{label}: {percent}%";
    }

    private string GetToggleText(string name)
    {
        var toggle = FindToggle(name);
        if (toggle == null) return name;

        string label = GetToggleLabel(name);
        string state = toggle.isOn ? "on" : "off";
        return $"{label}: {state}";
    }

    private string GetDropdownText(string name, string fallbackLabel)
    {
        var dropdown = FindDropdown(name);
        if (dropdown == null) return fallbackLabel;

        string label = fallbackLabel;
        var parent = dropdown.transform.parent;
        if (parent != null)
        {
            var tmp = parent.GetComponentInChildren<TMP_Text>();
            if (tmp != null && tmp.gameObject != dropdown.captionText?.gameObject)
            {
                string text = tmp.text?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && text != dropdown.captionText?.text)
                {
                    label = text;
                }
            }
        }

        string value = dropdown.captionText?.text ?? "";
        return $"{label}: {value}";
    }

    private void AdjustSlider(string name, int direction)
    {
        var slider = FindSlider(name);
        if (slider == null) return;

        // Usar normalizedValue para ajustar correctamente en el rango del slider
        float step = 0.1f;
        slider.normalizedValue = Mathf.Clamp01(slider.normalizedValue + (step * direction));

        int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
        Core.TolkWrapper.Speak($"{percent}%");
    }

    private void ToggleValue(string name)
    {
        var toggle = FindToggle(name);
        if (toggle == null) return;

        toggle.isOn = !toggle.isOn;

        string state = toggle.isOn ? "on" : "off";
        Core.TolkWrapper.Speak(state);
    }

    private void AdjustDropdown(string name, int direction)
    {
        var dropdown = FindDropdown(name);
        if (dropdown == null) return;

        var optionsProp = dropdown.GetType().GetProperty("options");
        if (optionsProp == null) return;

        var options = optionsProp.GetValue(dropdown);
        if (options == null) return;

        var countProp = options.GetType().GetProperty("Count");
        if (countProp == null) return;

        int optionCount = (int)countProp.GetValue(options);
        int newIndex = dropdown.value + direction;
        if (newIndex >= 0 && newIndex < optionCount)
        {
            dropdown.value = newIndex;

            string value = dropdown.captionText?.text ?? "";
            Core.TolkWrapper.Speak(value);
        }
    }

    protected override void OnBack()
    {
        if (_inGameplaySection)
        {
            // Volver a la sección principal
            string[] backButtonNames = { "Btn_Back", "Btn_GameplayBack", "_gameplayBackButton" };
            foreach (var name in backButtonNames)
            {
                if (ClickButtonByName(name))
                {
                    _inGameplaySection = false;
                    Plugin.Instance.StartCoroutine(RebuildAfterDelay());
                    return;
                }
            }
        }

        // Cerrar opciones
        ClickButtonByName("Btn_Close");
    }
}
