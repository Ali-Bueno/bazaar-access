using System;
using System.Collections.Generic;
using BazaarAccess.Core;

namespace BazaarAccess.Accessibility;

/// <summary>
/// Accessible menu with vertical navigation.
/// Follows the Hearthstone pattern: composition, delegates, and reading with position.
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

    // --- Add options ---

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

    // --- Navigation ---

    /// <summary>
    /// Handles keyboard input. Returns true if the key was consumed.
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
            // Read the new state after adjusting
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

    // --- Reading ---

    /// <summary>
    /// Starts reading the menu from the first option.
    /// Only announces the menu name - user will hear first option when they navigate.
    /// </summary>
    public void StartReading(bool announceMenuName = true)
    {
        _currentIndex = 0;

        if (announceMenuName && !string.IsNullOrEmpty(_menuName))
        {
            // Only announce menu name, not the first option
            // User will hear the option when they press an arrow key
            TolkWrapper.Speak(_menuName);
        }
        else if (_options.Count > 0)
        {
            // No menu name, so we need to announce the first option
            ReadCurrentOption();
        }
    }

    /// <summary>
    /// Reads the current option with its position.
    /// </summary>
    public void ReadCurrentOption()
    {
        if (_options.Count == 0 || _currentIndex >= _options.Count) return;

        var option = _options[_currentIndex];

        // Execute custom read action if it exists
        option.OnRead?.Invoke();

        // Get option text
        string text = option.GetText();

        // Format: "Text, item X of Y"
        string speech = $"{text}, item {_currentIndex + 1} of {_options.Count}";

        TolkWrapper.Speak(speech);
    }

    /// <summary>
    /// Sets the current index without reading.
    /// </summary>
    public void SetIndex(int index)
    {
        if (index >= 0 && index < _options.Count)
        {
            _currentIndex = index;
        }
    }

    /// <summary>
    /// Gets the menu help text.
    /// </summary>
    public string GetHelp()
    {
        string help = "Use up and down to navigate. Enter to select.";
        if (_onBack != null)
        {
            help += " Escape to go back.";
        }
        return help;
    }
}

/// <summary>
/// Accessible keys.
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
    // Section navigation
    GoToBoard,      // B - Go to board
    GoToHero,       // V - Go to hero
    GoToChoices,    // C - Go to choices/selection
    GoToEnemy,      // F - Go to enemy/NPC
    GoToStash,      // G - Go to stash
    // Game actions
    Exit,           // E - Exit current state
    Reroll,         // R - Reroll/Refresh
    Space,          // Space - Move item
    // Move items between board and stash
    MoveToBoard,    // Shift+Up - Move item from stash to board
    MoveToStash,    // Shift+Down - Move item from board to stash
    // Reorder items on the board
    ReorderLeft,    // Shift+Left - Move item left
    ReorderRight,   // Shift+Right - Move item right
    // Detailed reading line by line (or Hero stats navigation)
    DetailUp,       // Ctrl+Up - Next line/stat
    DetailDown,     // Ctrl+Down - Previous line/stat
    // Change subsection in Hero (Stats/Skills)
    DetailLeft,     // Ctrl+Left - Previous subsection
    DetailRight,    // Ctrl+Right - Next subsection
    // Message buffer
    NextMessage,    // Period - Most recent message
    PrevMessage,    // Comma - Previous message
    // Additional information
    Info,           // I - Property/keyword info for the item
    // Upgrade
    Upgrade,        // Shift+U - Upgrade item at pedestal
    // Board info
    BoardInfo,      // T - Board capacity info (slots used/total)
    // Challenges
    Challenges      // Q - Read current challenges
}
