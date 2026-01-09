using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Clase base para pantallas accesibles.
/// Proporciona helpers para interactuar con la UI del juego.
/// </summary>
public abstract class BaseScreen : IAccessibleScreen
{
    protected readonly Transform Root;
    protected readonly AccessibleMenu Menu;

    public abstract string ScreenName { get; }

    protected BaseScreen(Transform root)
    {
        Root = root;
        Menu = new AccessibleMenu(ScreenName);
        BuildMenu();
    }

    /// <summary>
    /// Construye el menú con las opciones de la pantalla.
    /// Implementar en clases derivadas.
    /// </summary>
    protected abstract void BuildMenu();

    public virtual void HandleInput(AccessibleKey key)
    {
        Menu.HandleInput(key);
    }

    public virtual string GetHelp()
    {
        return Menu.GetHelp();
    }

    public virtual void OnFocus()
    {
        // Debug: listar todos los botones encontrados
        LogAllButtons();
        Menu.StartReading(announceMenuName: true);
    }

    /// <summary>
    /// Debug: Lista todos los botones en la UI.
    /// </summary>
    protected void LogAllButtons()
    {
        if (Root == null) return;

        var buttons = Root.GetComponentsInChildren<Button>(true);
        Plugin.Logger.LogInfo($"=== Botones en {ScreenName} ({buttons.Length} total) ===");

        foreach (var button in buttons)
        {
            string text = GetButtonText(button);
            string active = button.gameObject.activeInHierarchy ? "activo" : "inactivo";
            string interactable = button.interactable ? "interactable" : "no-interactable";
            Plugin.Logger.LogInfo($"  [{button.gameObject.name}] texto='{text}' ({active}, {interactable})");
        }

        Plugin.Logger.LogInfo("=== Fin botones ===");
    }

    public virtual bool IsValid()
    {
        if (Root == null) return false;
        if (!Root.gameObject.activeInHierarchy) return false;
        return true;
    }

    // --- Helpers para interactuar con la UI del juego ---

    /// <summary>
    /// Busca y hace click en un botón por su texto visible.
    /// </summary>
    protected bool ClickButtonByText(string text)
    {
        var button = FindButtonByText(text);
        if (button != null && button.interactable)
        {
            Plugin.Logger.LogInfo($"Click por texto: {text}");
            button.onClick.Invoke();
            return true;
        }

        Plugin.Logger.LogWarning($"Botón no encontrado por texto: {text}");
        return false;
    }

    /// <summary>
    /// Busca y hace click en un botón por el nombre del GameObject.
    /// </summary>
    protected bool ClickButtonByName(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.interactable)
        {
            Plugin.Logger.LogInfo($"Click por nombre: {name}");
            button.onClick.Invoke();
            return true;
        }

        Plugin.Logger.LogWarning($"Botón no encontrado por nombre: {name}");
        return false;
    }

    /// <summary>
    /// Busca un botón por su texto visible (case-insensitive).
    /// </summary>
    protected Button FindButtonByText(string text)
    {
        if (Root == null) return null;

        var buttons = Root.GetComponentsInChildren<Button>(true)
            .Where(b => b.gameObject.activeInHierarchy);

        foreach (var button in buttons)
        {
            string buttonText = GetButtonText(button);
            if (buttonText != null && buttonText.Equals(text, System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    /// <summary>
    /// Busca un botón por el nombre del GameObject (case-insensitive).
    /// </summary>
    protected Button FindButtonByName(string name)
    {
        if (Root == null) return null;

        return Root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.gameObject.activeInHierarchy &&
                                 b.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene el texto de un botón por nombre de GameObject.
    /// Útil para crear labels dinámicos.
    /// </summary>
    protected string GetButtonTextByName(string name)
    {
        var button = FindButtonByName(name);
        if (button == null) return name; // Fallback al nombre

        string text = GetButtonText(button);
        return string.IsNullOrWhiteSpace(text) ? name : text;
    }

    /// <summary>
    /// Obtiene el texto de un botón.
    /// </summary>
    protected string GetButtonText(Button button)
    {
        // Intentar BazaarButtonController primero
        var bazaarButton = button as BazaarButtonController;
        if (bazaarButton != null && bazaarButton.ButtonText != null)
        {
            return bazaarButton.ButtonText.text?.Trim();
        }

        // TMP_Text en hijos
        var tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
        {
            return tmp.text.Trim();
        }

        // Text legacy
        var legacyText = button.GetComponentInChildren<Text>();
        if (legacyText != null && !string.IsNullOrWhiteSpace(legacyText.text))
        {
            return legacyText.text.Trim();
        }

        return null;
    }

    /// <summary>
    /// Busca un Toggle por nombre.
    /// </summary>
    protected Toggle FindToggle(string name)
    {
        if (Root == null) return null;

        return Root.GetComponentsInChildren<Toggle>(true)
            .FirstOrDefault(t => t.gameObject.activeInHierarchy &&
                                 t.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Busca un Slider por nombre.
    /// </summary>
    protected Slider FindSlider(string name)
    {
        if (Root == null) return null;

        return Root.GetComponentsInChildren<Slider>(true)
            .FirstOrDefault(s => s.gameObject.activeInHierarchy &&
                                 s.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }
}
