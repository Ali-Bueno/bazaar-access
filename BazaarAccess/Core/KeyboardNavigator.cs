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

        AccessibleKey key = MapKey(e.keyCode);
        if (key == AccessibleKey.None) return;

        ClearUISelection();
        AccessibilityMgr.HandleInput(key);
        e.Use();
    }

    /// <summary>
    /// Mapea KeyCode de Unity a AccessibleKey.
    /// </summary>
    private AccessibleKey MapKey(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.UpArrow:
                return AccessibleKey.Up;

            case KeyCode.DownArrow:
                return AccessibleKey.Down;

            case KeyCode.LeftArrow:
                return AccessibleKey.Left;

            case KeyCode.RightArrow:
                return AccessibleKey.Right;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                return AccessibleKey.Confirm;

            case KeyCode.Escape:
            case KeyCode.Backspace:
                return AccessibleKey.Back;

            case KeyCode.F1:
                return AccessibleKey.Help;

            default:
                return AccessibleKey.None;
        }
    }
}
