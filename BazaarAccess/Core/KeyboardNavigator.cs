using BazaarAccess.Accessibility;
using BazaarAccess.UI.Login;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BazaarAccess.Core;

/// <summary>
/// Handles keyboard input for accessible navigation.
/// </summary>
public class KeyboardNavigator : MonoBehaviour
{
    private static KeyboardNavigator _instance;

    public static void Create(GameObject parent)
    {
        if (_instance == null)
        {
            _instance = parent.AddComponent<KeyboardNavigator>();
            Plugin.Logger.LogInfo("KeyboardNavigator created");
        }
    }

    public static void Destroy()
    {
        if (_instance != null)
        {
            Object.Destroy(_instance);
            _instance = null;
        }
    }

    private void ClearUISelection()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    private void OnGUI()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;

        // If in edit mode, only allow Enter/Escape to exit
        if (IsAnyTextFieldEditing())
        {
            if (e.keyCode != KeyCode.Return &&
                e.keyCode != KeyCode.KeypadEnter &&
                e.keyCode != KeyCode.Escape)
            {
                return; // Unity handles text input
            }
        }

        AccessibleKey key = MapKey(e);
        if (key == AccessibleKey.None) return;

        // Don't clear selection if editing (keep focus on input field)
        if (!IsAnyTextFieldEditing())
        {
            ClearUISelection();
        }

        AccessibilityMgr.HandleInput(key);
        e.Use();
    }

    /// <summary>
    /// Checks if any text field is in edit mode.
    /// </summary>
    private bool IsAnyTextFieldEditing()
    {
        var focusedUI = AccessibilityMgr.GetFocusedUI();
        if (focusedUI is LoginBaseUI loginUI)
        {
            return loginUI.IsInEditMode;
        }
        return false;
    }

    /// <summary>
    /// Maps Unity KeyCode to AccessibleKey.
    /// Simple controls faithful to the original game.
    /// </summary>
    private AccessibleKey MapKey(Event e)
    {
        bool ctrl = e.control;
        bool shift = e.shift;
        KeyCode keyCode = e.keyCode;

        switch (keyCode)
        {
            // Ctrl = detailed reading, Shift = move between board/stash
            case KeyCode.UpArrow:
                if (ctrl) return AccessibleKey.DetailUp;
                if (shift) return AccessibleKey.MoveToStash;
                return AccessibleKey.Up;

            case KeyCode.DownArrow:
                if (ctrl) return AccessibleKey.DetailDown;
                if (shift) return AccessibleKey.MoveToBoard;
                return AccessibleKey.Down;

            // Shift+Left/Right = reorder items, Ctrl+Left/Right = change Hero subsection
            case KeyCode.LeftArrow:
                if (ctrl) return AccessibleKey.DetailLeft;
                if (shift) return AccessibleKey.ReorderLeft;
                return AccessibleKey.Left;

            case KeyCode.RightArrow:
                if (ctrl) return AccessibleKey.DetailRight;
                if (shift) return AccessibleKey.ReorderRight;
                return AccessibleKey.Right;

            // Main actions
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                return AccessibleKey.Confirm;

            case KeyCode.Backspace:
                return AccessibleKey.Back;

            // Note: Escape is NOT mapped here because the game uses it for options menu

            case KeyCode.Tab:
                return AccessibleKey.Tab;

            case KeyCode.F1:
                return AccessibleKey.Help;

            // Section navigation
            case KeyCode.B:
                return AccessibleKey.GoToBoard;

            case KeyCode.V:
                return AccessibleKey.GoToHero;

            case KeyCode.C:
                return AccessibleKey.GoToChoices;

            case KeyCode.F:
                return AccessibleKey.GoToEnemy;

            case KeyCode.G:
                return AccessibleKey.GoToStash;

            // Game actions
            case KeyCode.E:
                return AccessibleKey.Exit;

            case KeyCode.R:
                return AccessibleKey.Reroll;

            case KeyCode.Space:
                return AccessibleKey.Space;

            // Message buffer
            case KeyCode.Period:
                return AccessibleKey.NextMessage;

            case KeyCode.Comma:
                return AccessibleKey.PrevMessage;

            // Additional information
            case KeyCode.I:
                return AccessibleKey.Info;

            // Upgrade item
            case KeyCode.U:
                if (shift) return AccessibleKey.Upgrade;
                return AccessibleKey.None;

            // Board info
            case KeyCode.T:
                return AccessibleKey.BoardInfo;

            // Stash info
            case KeyCode.S:
                return AccessibleKey.StashInfo;

            // Combat summary
            case KeyCode.H:
                return AccessibleKey.CombatSummary;

            // Combat mode toggle
            case KeyCode.M:
                if (ctrl) return AccessibleKey.ToggleCombatMode;
                return AccessibleKey.None;

            // Wins info
            case KeyCode.W:
                return AccessibleKey.WinsInfo;

            // Fast navigation
            case KeyCode.Home:
                return AccessibleKey.Home;

            case KeyCode.End:
                return AccessibleKey.End;

            case KeyCode.PageUp:
                return AccessibleKey.PageUp;

            case KeyCode.PageDown:
                return AccessibleKey.PageDown;

            // Quick combat stats
            case KeyCode.Alpha1:
                return AccessibleKey.PlayerHealth;

            case KeyCode.Alpha2:
                return AccessibleKey.EnemyHealth;

            case KeyCode.Alpha3:
                return AccessibleKey.DamageDealt;

            case KeyCode.Alpha4:
                return AccessibleKey.DamageTaken;

            default:
                return AccessibleKey.None;
        }
    }
}
