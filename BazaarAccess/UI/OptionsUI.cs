using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para el menú de opciones.
/// Incluye todas las secciones: audio, video, accesibilidad, etc.
/// </summary>
public class OptionsUI : BaseUI
{
    public override string UIName => "Options";

    public OptionsUI(Transform root) : base(root)
    {
        // Log de todos los elementos para debug
        LogAllElements();
    }

    private void LogAllElements()
    {
        Plugin.Logger.LogInfo("=== OptionsUI elementos ===");

        // Botones
        var buttons = Root.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                string text = GetComponentText(btn.transform);
                Plugin.Logger.LogInfo($"[Btn ACTIVO] {btn.gameObject.name}: '{text}'");
            }
        }

        // Toggles
        var toggles = Root.GetComponentsInChildren<Toggle>(true);
        foreach (var toggle in toggles)
        {
            if (toggle.gameObject.activeInHierarchy)
            {
                string text = GetComponentText(toggle.transform);
                Plugin.Logger.LogInfo($"[Toggle] {toggle.gameObject.name}: '{text}' = {toggle.isOn}");
            }
        }

        // Sliders
        var sliders = Root.GetComponentsInChildren<Slider>(true);
        foreach (var slider in sliders)
        {
            if (slider.gameObject.activeInHierarchy)
            {
                string text = GetComponentText(slider.transform.parent);
                Plugin.Logger.LogInfo($"[Slider] {slider.gameObject.name}: '{text}' = {slider.value:F2}");
            }
        }

        // Dropdowns
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
        // === Botones de navegación entre secciones ===
        AddSectionButton("Btn_GameplaySettings");
        AddSectionButton("Btn_NotificationSettings");
        AddSectionButton("Btn_AudioSettings");
        AddSectionButton("Btn_VideoSettings");
        AddSectionButton("Btn_AccessibilitySettings");

        // === Sliders de audio ===
        AddSliderOption("Slider_Master");
        AddSliderOption("Slider_Music");
        AddSliderOption("Slider_SoundEffects");
        AddSliderOption("Slider_Voice");

        // === Dropdowns de video ===
        AddDropdownOption("Dropdown", "Resolution"); // Resolución
        AddDropdownOptionByIndex(1, "Framerate"); // Segundo dropdown es framerate

        // === Toggles de video ===
        AddToggleOption("Toggle_Windowed");
        AddToggleOption("Toggle_VSync");
        AddToggleOption("Toggle_SkipVideo");

        // === Toggles de accesibilidad ===
        AddToggleOption("Toggle_AlternativeInput");
        AddToggleOption("Toggle_Clock");
        AddToggleOption("Toggle_Keyword");
        AddToggleOption("Toggle_HealthBarIcons");
        AddToggleOption("Toggle_GemStatusIcons");
        AddToggleOption("Toggle_DisableScreenShake");
        AddToggleOption("Toggle_MotionSensitivity");
        AddToggleOption("Toggle_ClosedCaptions");

        // === Dropdown de idioma ===
        AddDropdownOption("Dropdown_Language", "Language");

        // === Botones de acción ===
        AddButtonIfActive("Btn_Privacy");
        AddButtonIfActive("Btn_Terms");
        AddButtonIfActive("Btn_Save");
        AddButtonIfActive("Btn_ExitToDesktop");
        AddButtonIfActive("Btn_Close");
    }

    /// <summary>
    /// Reconstruye el menú para reflejar los elementos actuales.
    /// </summary>
    public void RebuildMenu()
    {
        Menu.Clear();
        LogAllElements();
        BuildMenu();
        Menu.StartReading(announceMenuName: false);
    }

    private void AddSectionButton(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.gameObject.activeInHierarchy)
        {
            Menu.AddOption(
                () => GetButtonText(name),
                () => {
                    ClickButtonByName(name);
                    // Esperar un frame y reconstruir el menú
                    Plugin.Instance.StartCoroutine(RebuildAfterDelay());
                });
        }
    }

    private System.Collections.IEnumerator RebuildAfterDelay()
    {
        yield return null; // Esperar un frame
        yield return null; // Esperar otro frame para que Unity actualice
        RebuildMenu();
    }

    private void AddButtonIfActive(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.gameObject.activeInHierarchy)
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
        int percent = Mathf.RoundToInt(slider.value * 100);
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
        // Intentar obtener label del padre
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

        float step = 0.1f;
        slider.value = Mathf.Clamp01(slider.value + (step * direction));

        // Anunciar el nuevo valor
        int percent = Mathf.RoundToInt(slider.value * 100);
        Core.TolkWrapper.Speak($"{percent}%");
    }

    private void ToggleValue(string name)
    {
        var toggle = FindToggle(name);
        if (toggle == null) return;

        toggle.isOn = !toggle.isOn;

        // Anunciar el nuevo estado
        string state = toggle.isOn ? "on" : "off";
        Core.TolkWrapper.Speak(state);
    }

    private void AdjustDropdown(string name, int direction)
    {
        var dropdown = FindDropdown(name);
        if (dropdown == null) return;

        // Usar reflexión para evitar referencia directa a List<>
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

            // Anunciar el nuevo valor
            string value = dropdown.captionText?.text ?? "";
            Core.TolkWrapper.Speak(value);
        }
    }

    protected override void OnBack()
    {
        ClickButtonByName("Btn_Close");
    }
}
