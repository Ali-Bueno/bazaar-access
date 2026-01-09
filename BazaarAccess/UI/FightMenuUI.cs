using BazaarAccess.Accessibility;
using UnityEngine;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para el menú de pausa durante el gameplay.
/// </summary>
public class FightMenuUI : BaseUI
{
    public override string UIName => "Pause Menu";

    public FightMenuUI(Transform root) : base(root)
    {
        LogAllElements();
    }

    private void LogAllElements()
    {
        Plugin.Logger.LogInfo("=== FightMenuUI elementos ===");

        var buttons = Root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                string text = GetButtonTextFromTransform(btn.transform);
                Plugin.Logger.LogInfo($"[Btn] {btn.gameObject.name}: '{text}'");
            }
        }

        Plugin.Logger.LogInfo("=== Fin FightMenuUI ===");
    }

    private string GetButtonTextFromTransform(Transform t)
    {
        var tmp = t.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
            return tmp.text.Trim();
        return "";
    }

    protected override void BuildMenu()
    {
        // Continuar jugando
        AddButtonIfActive("Btn_Resume");

        // Ajustes/Opciones
        AddButtonIfActive("Btn_SettingsCol");

        // Reportar bug
        AddButtonIfActive("Btn_ReportBug");

        // Rendirse
        AddButtonIfActive("Btn_Concede");

        // Volver al menú principal
        AddButtonIfActive("Btn_Exit_MainMenu");

        // Salir del juego
        AddButtonIfActive("Btn_Exit_Desktop");

        // Botón de cerrar (X)
        AddButtonIfActive("Btn_Close_Menu");
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

    protected override void OnBack()
    {
        // Al presionar Escape, cerrar el menú de pausa (continuar)
        ClickButtonByName("Btn_Resume");
    }
}
