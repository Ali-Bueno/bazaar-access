using System;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Representa una opción en un menú accesible.
/// Sigue el patrón de Hearthstone con delegados para texto dinámico.
/// </summary>
public class MenuOption
{
    /// <summary>
    /// Delegado para obtener el texto de la opción (permite texto dinámico).
    /// </summary>
    public Func<string> GetText { get; }

    /// <summary>
    /// Acción al confirmar (Enter).
    /// </summary>
    public Action OnConfirm { get; }

    /// <summary>
    /// Acción al leer la opción (opcional, para comportamiento especial).
    /// </summary>
    public Action OnRead { get; }

    /// <summary>
    /// Acción al ajustar con izquierda/derecha (para sliders/toggles).
    /// </summary>
    public Action<int> OnAdjust { get; }

    /// <summary>
    /// Tecla de acceso rápido (opcional).
    /// </summary>
    public string Hotkey { get; }

    // Constructor con texto estático
    public MenuOption(string text, Action onConfirm, Action onRead = null, Action<int> onAdjust = null, string hotkey = null)
        : this(() => text, onConfirm, onRead, onAdjust, hotkey)
    {
    }

    // Constructor con texto dinámico
    public MenuOption(Func<string> getText, Action onConfirm, Action onRead = null, Action<int> onAdjust = null, string hotkey = null)
    {
        GetText = getText ?? (() => "");
        OnConfirm = onConfirm;
        OnRead = onRead;
        OnAdjust = onAdjust;
        Hotkey = hotkey;
    }
}
