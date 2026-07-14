using System;
using BazaarAccess.Core;
using TMPro;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Opción de menú especial para campos de texto con modo edición.
/// Cuando no está editando: las flechas navegan el menú.
/// Cuando está editando: todas las teclas van al input field.
/// </summary>
public class TextFieldOption
{
    private readonly TMP_InputField _inputField;
    private readonly string _label;
    private bool _isEditing;

    public bool IsEditing => _isEditing;
    public TMP_InputField InputField => _inputField;
    public string Label => _label;

    /// <summary>
    /// Evento disparado cuando cambia el modo edición.
    /// </summary>
    public event Action<bool> OnEditModeChanged;

    public TextFieldOption(string label, TMP_InputField inputField)
    {
        _label = label;
        _inputField = inputField;
        _isEditing = false;
    }

    /// <summary>
    /// Obtiene el texto para mostrar: "Label: contenido" o "Label: X characters" para passwords.
    /// </summary>
    public string GetDisplayText()
    {
        if (_inputField == null)
            return Loc.T("patch.textfield.unavailable", _label);

        string value = _inputField.text ?? "";

        if (string.IsNullOrEmpty(value))
            return Loc.T("patch.textfield.empty", _label);

        // Para campos de contraseña, indicar caracteres sin revelar contenido
        if (_inputField.contentType == TMP_InputField.ContentType.Password)
            return Loc.Plural("patch.textfield.chars_entered", value.Length, _label, value.Length);

        return Loc.T("patch.textfield.value", _label, value);
    }

    /// <summary>
    /// Alterna entre modo navegación y modo edición.
    /// </summary>
    public void ToggleEditMode()
    {
        if (_inputField == null) return;

        _isEditing = !_isEditing;

        if (_isEditing)
        {
            // Enfocar el input field para que Unity maneje el texto
            _inputField.Select();
            _inputField.ActivateInputField();
            TolkWrapper.Speak(Loc.T("patch.textfield.editing"));
        }
        else
        {
            // Desenfocar el input field
            _inputField.DeactivateInputField();
            TolkWrapper.Speak(Loc.T("patch.textfield.done"));
        }

        OnEditModeChanged?.Invoke(_isEditing);
    }

    /// <summary>
    /// Fuerza la salida del modo edición.
    /// </summary>
    public void ExitEditMode()
    {
        if (!_isEditing) return;

        _isEditing = false;
        if (_inputField != null)
        {
            _inputField.DeactivateInputField();
        }
        TolkWrapper.Speak(Loc.T("patch.textfield.done"));
        OnEditModeChanged?.Invoke(false);
    }

    /// <summary>
    /// Entra en modo edición sin toggle.
    /// </summary>
    public void EnterEditMode()
    {
        if (_isEditing || _inputField == null) return;

        _isEditing = true;
        _inputField.Select();
        _inputField.ActivateInputField();
        TolkWrapper.Speak(Loc.T("patch.textfield.editing"));
        OnEditModeChanged?.Invoke(true);
    }
}
