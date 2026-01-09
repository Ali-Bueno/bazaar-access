using BazaarAccess.Accessibility;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BazaarAccess.Core;

/// <summary>
/// Maneja la entrada de teclado para navegación accesible.
/// </summary>
public class KeyboardNavigator : MonoBehaviour
{
    private static KeyboardNavigator _instance;

    public static void Create(GameObject parent)
    {
        if (_instance == null)
        {
            _instance = parent.AddComponent<KeyboardNavigator>();
            Plugin.Logger.LogInfo("KeyboardNavigator creado");
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

        AccessibleKey key = MapKey(e);
        if (key == AccessibleKey.None) return;

        ClearUISelection();
        AccessibilityMgr.HandleInput(key);
        e.Use();
    }

    /// <summary>
    /// Mapea KeyCode de Unity a AccessibleKey.
    /// Controles simples y fieles al juego original.
    /// </summary>
    private AccessibleKey MapKey(Event e)
    {
        bool ctrl = e.control;
        bool shift = e.shift;
        KeyCode keyCode = e.keyCode;

        switch (keyCode)
        {
            // Ctrl = lectura detallada, Shift = mover entre board/stash
            case KeyCode.UpArrow:
                if (ctrl) return AccessibleKey.DetailUp;
                if (shift) return AccessibleKey.MoveToStash;
                return AccessibleKey.Up;

            case KeyCode.DownArrow:
                if (ctrl) return AccessibleKey.DetailDown;
                if (shift) return AccessibleKey.MoveToBoard;
                return AccessibleKey.Down;

            // Shift+Izq/Der = reordenar items, Ctrl+Izq/Der = cambiar subsección Hero
            case KeyCode.LeftArrow:
                if (ctrl) return AccessibleKey.DetailLeft;
                if (shift) return AccessibleKey.ReorderLeft;
                return AccessibleKey.Left;

            case KeyCode.RightArrow:
                if (ctrl) return AccessibleKey.DetailRight;
                if (shift) return AccessibleKey.ReorderRight;
                return AccessibleKey.Right;

            // Acciones principales
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                return AccessibleKey.Confirm;

            case KeyCode.Backspace:
            case KeyCode.Escape:
                return AccessibleKey.Back;

            case KeyCode.Tab:
                return AccessibleKey.Tab;

            case KeyCode.F1:
                return AccessibleKey.Help;

            // Navegación de secciones
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

            // Acciones del juego
            case KeyCode.E:
                return AccessibleKey.Exit;

            case KeyCode.R:
                return AccessibleKey.Reroll;

            case KeyCode.Space:
                return AccessibleKey.Space;

            // Buffer de mensajes
            case KeyCode.Period:
                return AccessibleKey.NextMessage;

            case KeyCode.Comma:
                return AccessibleKey.PrevMessage;

            default:
                return AccessibleKey.None;
        }
    }
}
