using System;
using System.Collections.Generic;
using BazaarAccess.Core;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Menú accesible con navegación vertical.
/// Sigue el patrón de Hearthstone: composición, delegados, y lectura con posición.
/// </summary>
public class AccessibleMenu
{
    private readonly List<MenuOption> _options = new List<MenuOption>();
    private readonly string _menuName;
    private readonly Action _onBack;
    private int _currentIndex;

    public string MenuName => _menuName;
    public int CurrentIndex => _currentIndex;
    public int OptionCount => _options.Count;

    public AccessibleMenu(string menuName, Action onBack = null)
    {
        _menuName = menuName;
        _onBack = onBack;
        _currentIndex = 0;
    }

    // --- Añadir opciones ---

    public void AddOption(string text, Action onConfirm)
    {
        _options.Add(new MenuOption(text, onConfirm));
    }

    public void AddOption(string text, Action onConfirm, Action onRead)
    {
        _options.Add(new MenuOption(text, onConfirm, onRead));
    }

    public void AddOption(Func<string> getText, Action onConfirm)
    {
        _options.Add(new MenuOption(getText, onConfirm));
    }

    public void AddOption(MenuOption option)
    {
        _options.Add(option);
    }

    public void AddOption(Func<string> getText, Action onConfirm, Action onRead, Action<int> onAdjust)
    {
        _options.Add(new MenuOption(getText, onConfirm, onRead, onAdjust));
    }

    public void Clear()
    {
        _options.Clear();
        _currentIndex = 0;
    }

    // --- Navegación ---

    /// <summary>
    /// Maneja la entrada del teclado. Retorna true si consumió la tecla.
    /// </summary>
    public bool HandleInput(AccessibleKey key)
    {
        if (_options.Count == 0) return false;

        switch (key)
        {
            case AccessibleKey.Up:
                Navigate(-1);
                return true;

            case AccessibleKey.Down:
                Navigate(1);
                return true;

            case AccessibleKey.Left:
                Adjust(-1);
                return true;

            case AccessibleKey.Right:
                Adjust(1);
                return true;

            case AccessibleKey.Confirm:
                Confirm();
                return true;

            case AccessibleKey.Back:
                Back();
                return true;

            default:
                return false;
        }
    }

    private void Navigate(int direction)
    {
        if (_options.Count == 0) return;

        _currentIndex += direction;

        // Wrap around
        if (_currentIndex < 0) _currentIndex = _options.Count - 1;
        if (_currentIndex >= _options.Count) _currentIndex = 0;

        ReadCurrentOption();
    }

    private void Adjust(int direction)
    {
        if (_options.Count == 0 || _currentIndex >= _options.Count) return;

        var option = _options[_currentIndex];
        if (option.OnAdjust != null)
        {
            option.OnAdjust(direction);
            // Leer el nuevo estado después de ajustar
            ReadCurrentOption();
        }
    }

    private void Confirm()
    {
        if (_options.Count == 0 || _currentIndex >= _options.Count) return;

        var option = _options[_currentIndex];
        option.OnConfirm?.Invoke();
    }

    private void Back()
    {
        _onBack?.Invoke();
    }

    // --- Lectura ---

    /// <summary>
    /// Empieza a leer el menú desde la primera opción.
    /// </summary>
    public void StartReading(bool announceMenuName = true)
    {
        if (announceMenuName && !string.IsNullOrEmpty(_menuName))
        {
            TolkWrapper.Speak(_menuName);
        }

        _currentIndex = 0;

        if (_options.Count > 0)
        {
            ReadCurrentOption();
        }
    }

    /// <summary>
    /// Lee la opción actual con su posición.
    /// </summary>
    public void ReadCurrentOption()
    {
        if (_options.Count == 0 || _currentIndex >= _options.Count) return;

        var option = _options[_currentIndex];

        // Ejecutar acción de lectura personalizada si existe
        option.OnRead?.Invoke();

        // Obtener texto de la opción
        string text = option.GetText();

        // Formato: "Texto, elemento X de Y"
        string speech = $"{text}, elemento {_currentIndex + 1} de {_options.Count}";

        TolkWrapper.Speak(speech);
    }

    /// <summary>
    /// Establece el índice actual sin leer.
    /// </summary>
    public void SetIndex(int index)
    {
        if (index >= 0 && index < _options.Count)
        {
            _currentIndex = index;
        }
    }

    /// <summary>
    /// Obtiene la ayuda del menú.
    /// </summary>
    public string GetHelp()
    {
        string help = "Usa arriba y abajo para navegar. Enter para seleccionar.";
        if (_onBack != null)
        {
            help += " Escape para volver.";
        }
        return help;
    }
}

/// <summary>
/// Teclas accesibles.
/// </summary>
public enum AccessibleKey
{
    None,
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Back,
    Help,
    Tab,
    // Navegación de secciones
    GoToBoard,      // B - Ir al tablero
    GoToHero,       // V - Ir al héroe
    GoToChoices,    // C - Ir a choices/selection
    GoToEnemy,      // F - Ir al enemigo/NPC
    GoToStash,      // G - Ir al stash
    // Acciones del juego
    Exit,           // E - Salir del estado actual
    Reroll,         // R - Reroll/Refresh
    Space,          // Espacio - Mover item
    // Mover items entre board y stash
    MoveToBoard,    // Shift+Up - Mover item del stash al board
    MoveToStash,    // Shift+Down - Mover item del board al stash
    // Reordenar items en el tablero
    ReorderLeft,    // Shift+Left - Mover item a la izquierda
    ReorderRight,   // Shift+Right - Mover item a la derecha
    // Lectura detallada línea por línea (o navegación Hero stats)
    DetailUp,       // Ctrl+Up - Siguiente línea/stat
    DetailDown,     // Ctrl+Down - Línea/stat anterior
    // Cambiar subsección en Hero (Stats/Skills)
    DetailLeft,     // Ctrl+Left - Subsección anterior
    DetailRight,    // Ctrl+Right - Subsección siguiente
    // Buffer de mensajes
    NextMessage,    // Punto - Mensaje más reciente
    PrevMessage     // Coma - Mensaje anterior
}
