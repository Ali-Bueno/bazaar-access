using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using TheBazaar;
using TMPro;
using UnityEngine;

namespace BazaarAccess.UI;

/// <summary>
/// UI accesible para popups genéricos (tutoriales, mensajes, confirmaciones).
/// </summary>
public class GenericPopupUI : BaseUI
{
    private readonly string _title;
    private readonly string _message;

    public override string UIName => string.IsNullOrEmpty(_title) ? "Popup" : _title;

    public GenericPopupUI(Transform root, string title, string message) : base(root)
    {
        _title = title;
        _message = message;

        // Añadir mensaje al buffer
        if (!string.IsNullOrEmpty(message))
        {
            MessageBuffer.Add(message);
        }
    }

    protected override void BuildMenu()
    {
        // Añadir opción para leer el mensaje completo
        if (!string.IsNullOrEmpty(_message))
        {
            Menu.AddOption(
                () => Loc.T("ui.popup.read_message"),
                () => TolkWrapper.Speak(_message));
        }

        // Buscar todos los botones activos en el popup
        var buttons = Root.GetComponentsInChildren<BazaarButtonController>(true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.activeInHierarchy && btn.interactable)
            {
                var capturedBtn = btn;
                string btnText = btn.ButtonText?.text?.Trim() ?? btn.gameObject.name;

                Menu.AddOption(
                    () => btnText,
                    () => capturedBtn.onClick.Invoke());
            }
        }

        // Si no encontramos botones BazaarButtonController, buscar botones normales
        if (Menu.OptionCount == (string.IsNullOrEmpty(_message) ? 0 : 1))
        {
            var normalButtons = Root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var btn in normalButtons)
            {
                if (btn.gameObject.activeInHierarchy && btn.interactable)
                {
                    var capturedBtn = btn;
                    var tmp = btn.GetComponentInChildren<TMP_Text>();
                    string btnText = tmp?.text?.Trim() ?? btn.gameObject.name;

                    Menu.AddOption(
                        () => btnText,
                        () => capturedBtn.onClick.Invoke());
                }
            }
        }
    }

    public override void OnFocus()
    {
        // Leer título y mensaje al abrir
        if (!string.IsNullOrEmpty(_title))
        {
            TolkWrapper.Speak(_title);
        }

        if (!string.IsNullOrEmpty(_message))
        {
            TolkWrapper.Speak(_message);
        }

        // Luego leer la primera opción
        if (Menu.OptionCount > 0)
        {
            Menu.SetIndex(0);
            Menu.ReadCurrentOption();
        }
    }

    protected override void OnBack()
    {
        // Buscar botón de cerrar o cancelar
        if (!ClickButtonByName("Btn_Close"))
        {
            if (!ClickButtonByName("Btn_Cancel"))
            {
                // Intentar el primer botón disponible
                var buttons = Root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                foreach (var btn in buttons)
                {
                    if (btn.gameObject.activeInHierarchy && btn.interactable)
                    {
                        btn.onClick.Invoke();
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Crea una UI para un GenericPopup extrayendo el texto.
    /// </summary>
    public static GenericPopupUI CreateFromPopup(GenericPopup popup)
    {
        string title = "";
        string message = "";

        try
        {
            // Usar reflexión para obtener los campos privados
            var titleField = typeof(GenericPopup).GetField("_titleLabel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var messageField = typeof(GenericPopup).GetField("_messageLabel",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (titleField != null)
            {
                var titleLabel = titleField.GetValue(popup) as TMP_Text;
                title = titleLabel?.text?.Trim() ?? "";
            }

            if (messageField != null)
            {
                var messageLabel = messageField.GetValue(popup) as TMP_Text;
                message = messageLabel?.text?.Trim() ?? "";
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"GenericPopupUI.CreateFromPopup error: {ex.Message}");
        }

        return new GenericPopupUI(popup.transform, title, message);
    }
}
