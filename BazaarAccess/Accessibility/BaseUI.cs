using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Clase base para UIs accesibles (popups/diálogos).
/// </summary>
public abstract class BaseUI : IAccessibleUI
{
    protected readonly Transform Root;
    protected readonly AccessibleMenu Menu;

    public abstract string UIName { get; }

    protected BaseUI(Transform root)
    {
        Root = root;
        Menu = new AccessibleMenu(UIName, OnBack);
        BuildMenu();
    }

    protected abstract void BuildMenu();

    protected virtual void OnBack()
    {
        // Cerrar el diálogo - buscar botón de cerrar
        ClickButtonByName("Btn_Close");
    }

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
        Menu.StartReading(announceMenuName: true);
    }

    public virtual bool IsValid()
    {
        if (Root == null) return false;
        if (!Root.gameObject.activeInHierarchy) return false;
        return true;
    }

    // --- Helpers ---

    protected bool ClickButtonByName(string name)
    {
        var button = FindButtonByName(name);
        if (button != null && button.interactable)
        {
            Plugin.Logger.LogInfo($"UI Click: {name}");
            button.onClick.Invoke();
            return true;
        }
        return false;
    }

    protected Button FindButtonByName(string name)
    {
        if (Root == null) return null;
        return Root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.gameObject.activeInHierarchy &&
                                 b.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    protected Toggle FindToggle(string name)
    {
        if (Root == null) return null;
        return Root.GetComponentsInChildren<Toggle>(true)
            .FirstOrDefault(t => t.gameObject.activeInHierarchy &&
                                 t.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    protected Slider FindSlider(string name)
    {
        if (Root == null) return null;
        return Root.GetComponentsInChildren<Slider>(true)
            .FirstOrDefault(s => s.gameObject.activeInHierarchy &&
                                 s.gameObject.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    protected string GetSliderLabel(string name)
    {
        var slider = FindSlider(name);
        if (slider == null) return name;

        // Buscar texto en el padre
        var parent = slider.transform.parent;
        if (parent != null)
        {
            var tmp = parent.GetComponentInChildren<TMP_Text>();
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
                return tmp.text.Trim();
        }
        return name;
    }

    protected string GetToggleLabel(string name)
    {
        var toggle = FindToggle(name);
        if (toggle == null) return name;

        var tmp = toggle.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();

        return name;
    }

    protected string GetButtonText(string name)
    {
        var button = FindButtonByName(name);
        if (button == null) return name;

        var tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();

        return name;
    }
}
