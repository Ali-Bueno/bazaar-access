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

    public void AddOption(Func<string> getText, Action onConfirm, Action<int> onAdjust, Func<bool> visible = null)
    {
        _options.Add(new MenuOption(getText, onConfirm, null, onAdjust, null, visible));
    }

    public void AddOption(Func<string> getText, Action onConfirm, Func<bool> visible)
    {
        _options.Add(new MenuOption(getText, onConfirm, null, null, null, visible));
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

            case AccessibleKey.Home:
                NavigateToFirst();
                return true;

            case AccessibleKey.End:
                NavigateToLast();
                return true;

            case AccessibleKey.PageUp:
                NavigatePage(-1);
                return true;

            case AccessibleKey.PageDown:
                NavigatePage(1);
                return true;

            default:
                return false;
        }
    }

    private void Navigate(int direction)
    {
        var visibleOptions = GetVisibleOptions();
        if (visibleOptions.Count == 0) return;

        // Find current visible index
        int currentVisibleIndex = -1;
        for (int i = 0; i < visibleOptions.Count; i++)
        {
            if (visibleOptions[i].Index == _currentIndex)
            {
                currentVisibleIndex = i;
                break;
            }
        }

        // Move to next/prev visible option
        if (currentVisibleIndex < 0) currentVisibleIndex = 0;
        int newVisibleIndex = currentVisibleIndex + direction;

        // No wrap - stay at limits, just read current item
        if (newVisibleIndex < 0)
        {
            newVisibleIndex = 0;
        }
        if (newVisibleIndex >= visibleOptions.Count)
        {
            newVisibleIndex = visibleOptions.Count - 1;
        }

        _currentIndex = visibleOptions[newVisibleIndex].Index;
        ReadCurrentOption();
    }

    private void NavigateToFirst()
    {
        var visibleOptions = GetVisibleOptions();
        if (visibleOptions.Count == 0) return;

        _currentIndex = visibleOptions[0].Index;
        ReadCurrentOption();
    }

    private void NavigateToLast()
    {
        var visibleOptions = GetVisibleOptions();
        if (visibleOptions.Count == 0) return;

        _currentIndex = visibleOptions[visibleOptions.Count - 1].Index;
        ReadCurrentOption();
    }

    private void NavigatePage(int direction)
    {
        var visibleOptions = GetVisibleOptions();
        if (visibleOptions.Count == 0) return;

        // Only use page navigation if more than 10 items
        if (visibleOptions.Count <= 10)
        {
            // For small lists, just go to start/end
            if (direction < 0)
                NavigateToFirst();
            else
                NavigateToLast();
            return;
        }

        // Find current visible index
        int currentVisibleIndex = 0;
        for (int i = 0; i < visibleOptions.Count; i++)
        {
            if (visibleOptions[i].Index == _currentIndex)
            {
                currentVisibleIndex = i;
                break;
            }
        }

        // Move by 10 items
        int newVisibleIndex = currentVisibleIndex + (direction * 10);

        // Clamp to bounds, just read current item at limits
        if (newVisibleIndex < 0)
        {
            newVisibleIndex = 0;
        }
        if (newVisibleIndex >= visibleOptions.Count)
        {
            newVisibleIndex = visibleOptions.Count - 1;
        }

        _currentIndex = visibleOptions[newVisibleIndex].Index;
        ReadCurrentOption();
    }

    private List<VisibleOption> GetVisibleOptions()
    {
        var result = new List<VisibleOption>();
        for (int i = 0; i < _options.Count; i++)
        {
            if (_options[i].IsVisible())
                result.Add(new VisibleOption { Option = _options[i], Index = i });
        }
        return result;
    }

    private struct VisibleOption
    {
        public MenuOption Option;
        public int Index;
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
    /// Reads the current option with its position among visible options.
    /// </summary>
    public void ReadCurrentOption()
    {
        if (_options.Count == 0 || _currentIndex >= _options.Count) return;

        var option = _options[_currentIndex];
        if (!option.IsVisible()) return;

        // Execute custom read action if it exists
        option.OnRead?.Invoke();

        // Get option text
        string text = option.GetText();

        // Calculate position among visible options
        var visibleOptions = GetVisibleOptions();
        int visibleIndex = 0;
        for (int i = 0; i < visibleOptions.Count; i++)
        {
            if (visibleOptions[i].Index == _currentIndex)
            {
                visibleIndex = i;
                break;
            }
        }

        string speech = text;

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
    // Board and Stash info
    BoardInfo,      // T - Board capacity info (slots used/total)
    StashInfo,      // S - Stash capacity info (items/total)
    // Fast navigation
    Home,           // Home - Go to first element
    End,            // End - Go to last element
    PageUp,         // Page Up - Navigate faster (10 items)
    PageDown,       // Page Down - Navigate faster (10 items)
    // Combat
    CombatSummary,  // H - Combat summary (damage dealt/taken, health)
    // Quick combat stats (number keys)
    PlayerHealth,   // 1 - Player health (number only)
    EnemyHealth,    // 2 - Enemy health (number only)
    DamageDealt,    // 3 - Damage dealt (number only)
    DamageTaken,    // 4 - Damage taken (number only)
    // Wins info
    WinsInfo,       // W - Announce current wins/victories
    // Combat mode toggle
    ToggleCombatMode  // Ctrl+M - Toggle between individual and batched combat announcements
}
