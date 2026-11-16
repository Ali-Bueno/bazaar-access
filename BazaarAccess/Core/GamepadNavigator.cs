using System;
using BazaarAccess.Accessibility;
using BazaarAccess.Gameplay;
using BazaarAccess.Gameplay.Navigation;
using HarmonyLib;
using TheBazaar;
using TheBazaar.AppFramework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BazaarAccess.Core;

/// <summary>
/// Controller input. In the mod's navigation every button maps to a keyboard AccessibleKey (LT = action
/// layer, RT = info layer); in Hybrid mode the game's own navigation runs inside a run. See README "Controller".
/// </summary>
public class GamepadNavigator : MonoBehaviour
{
    // Same values as the game's NavInputRepeater, so held directions feel like the game's own navigation.
    private const float StickDeadzone = 0.5f;
    private const float RepeatDelaySeconds = 0.4f;
    private const float RepeatIntervalSeconds = 0.12f;

    private enum Layer { Base = 0, Action = 1, Info = 2 }

    private readonly struct ButtonBinding
    {
        public readonly Func<Gamepad, ButtonControl> Control;
        public readonly AccessibleKey[] Keys; // indexed by Layer

        public ButtonBinding(Func<Gamepad, ButtonControl> control, AccessibleKey baseKey, AccessibleKey actionKey, AccessibleKey infoKey)
        {
            Control = control;
            Keys = new[] { baseKey, actionKey, infoKey };
        }
    }

    private static readonly ButtonBinding[] Buttons =
    {
        new ButtonBinding(p => p.buttonSouth, AccessibleKey.Confirm, AccessibleKey.Upgrade, AccessibleKey.CombatSummary),
        new ButtonBinding(p => p.buttonEast, AccessibleKey.Back, AccessibleKey.GoToEnemy, AccessibleKey.ActionMove),
        new ButtonBinding(p => p.buttonWest, AccessibleKey.Inspect, AccessibleKey.Reroll, AccessibleKey.WinsInfo),
        new ButtonBinding(p => p.buttonNorth, AccessibleKey.Info, AccessibleKey.Exit, AccessibleKey.ToggleCombatMode),
        new ButtonBinding(p => p.leftShoulder, AccessibleKey.GoToChoices, AccessibleKey.GoToStash, AccessibleKey.Home),
        new ButtonBinding(p => p.rightShoulder, AccessibleKey.GoToBoard, AccessibleKey.GoToHero, AccessibleKey.End),
        new ButtonBinding(p => p.selectButton, AccessibleKey.Tab, AccessibleKey.Space, AccessibleKey.None),
        // Start is handled in Update: Escape in the base layer, the spoken controller layout with LT.
        new ButtonBinding(p => p.startButton, AccessibleKey.None, AccessibleKey.None, AccessibleKey.None),
        new ButtonBinding(p => p.leftStickButton, AccessibleKey.BoardInfo, AccessibleKey.None, AccessibleKey.None),
        new ButtonBinding(p => p.rightStickButton, AccessibleKey.StashInfo, AccessibleKey.None, AccessibleKey.None),
    };

    // Directions per layer: Up, Down, Left, Right.
    private static readonly AccessibleKey[][] Directions =
    {
        new[] { AccessibleKey.Up, AccessibleKey.Down, AccessibleKey.Left, AccessibleKey.Right },
        new[] { AccessibleKey.MoveToStash, AccessibleKey.MoveToBoard, AccessibleKey.ReorderLeft, AccessibleKey.ReorderRight },
        new[] { AccessibleKey.PlayerHealth, AccessibleKey.DamageDealt, AccessibleKey.DamageTaken, AccessibleKey.EnemyHealth },
    };

    private static GamepadNavigator _instance;

    /// <summary>True while the game's own controller navigation drives the run (Hybrid in a run, or GameOnly).</summary>
    public static bool GameNavigationActive { get; private set; }

    private int _heldDirection = -1;
    private float _nextRepeatTime;
    private int _rightStickDirection = -1;
    private bool _modNavigationInRun;

    public static void Create(GameObject parent)
    {
        if (_instance != null) return;
        _instance = parent.AddComponent<GamepadNavigator>();
        GamepadOwnership.Initialize();
    }

    public static void Destroy()
    {
        GamepadOwnership.Shutdown();
        if (_instance != null) UnityEngine.Object.Destroy(_instance);
        _instance = null;
    }

    private void Update()
    {
        var pad = Gamepad.current;
        bool modOwns = DecideModOwnership(pad);
        GamepadOwnership.Update(modOwns);
        GameNavigationActive = !modOwns && pad != null && InRun() && GameUsesControllerScheme();

        if (pad == null || !Application.isFocused || Plugin.ControllerMode.Value == ControllerMode.GameOnly) return;

        if (modOwns) HandleModNavigation(pad);
        else HandleGameNavigationExtras(pad);
    }

    // Hybrid: the game's own controller navigation runs inside a run (the mod speaks its focus);
    // the mod drives menus, its own run navigation (View toggles it) and the RT info layer while held.
    private bool DecideModOwnership(Gamepad pad)
    {
        switch (Plugin.ControllerMode.Value)
        {
            case ControllerMode.KeyboardEmulation: return true;
            case ControllerMode.GameOnly: return false;
        }

        if (!InRun())
        {
            _modNavigationInRun = false;
            return true;
        }
        return _modNavigationInRun || (pad != null && pad.rightTrigger.isPressed);
    }

    // The game's controller navigation only runs while its input scheme is a controller (last device used).
    private static bool GameUsesControllerScheme()
    {
        var scheme = Services.Get<TheBazaar.Inputs.InputManager>()?.Scheme?.Current;
        return scheme == TheBazaar.Inputs.InputScheme.Gamepad || scheme == TheBazaar.Inputs.InputScheme.SteamDeck;
    }

    private static bool InRun()
        => AccessibilityMgr.GetCurrentScreen() is GameplayScreen && AccessibilityMgr.GetFocusedUI() == null;

    private void ToggleModNavigation()
    {
        _modNavigationInRun = !_modNavigationInRun;
        TolkWrapper.Speak(Loc.T(_modNavigationInRun ? "gamepad.mod_navigation" : "gamepad.game_navigation"));
    }

    // Buttons the game leaves free during a run (View, stick clicks, right stick).
    private void HandleGameNavigationExtras(Gamepad pad)
    {
        if (pad.selectButton.wasPressedThisFrame) ToggleModNavigation();
        if (pad.leftStickButton.wasPressedThisFrame) SpeakFocusedDetails();
        if (pad.rightStickButton.wasPressedThisFrame) HeroNavigator.SpeakSummary();
        // No paging here: it would move the mod's hidden cursor behind the game's focus.
        HandleRightStick(pad, allowPaging: false);
    }

    private static void SpeakFocusedDetails()
    {
        var navigator = FindObjectOfType<TheBazaar.Inputs.Navigation.GameplayNavigator>();
        var card = (navigator?.CurrentFocus as CardController)?.CardData;
        TolkWrapper.Speak(card != null ? ItemReader.GetDetailedDescription(card) : Loc.T("nav.nothing_selected"));
    }

    // The mod's full navigation: every button maps to a keyboard key (README "Controller").
    private void HandleModNavigation(Gamepad pad)
    {
        var layer = pad.leftTrigger.isPressed ? Layer.Action : pad.rightTrigger.isPressed ? Layer.Info : Layer.Base;
        bool editing = KeyboardNavigator.IsAnyTextFieldEditing();
        bool hybridRun = Plugin.ControllerMode.Value == ControllerMode.Hybrid && InRun();

        foreach (var binding in Buttons)
        {
            var control = binding.Control(pad);
            if (!control.wasPressedThisFrame) continue;

            var key = binding.Keys[(int)layer];
            if (control == pad.startButton)
            {
                if (layer == Layer.Base) SendEscape();
                else if (layer == Layer.Action) TolkWrapper.Speak(Loc.T("gamepad.help"));
            }
            else if (hybridRun && layer == Layer.Base && control == pad.selectButton)
                ToggleModNavigation();
            else if (key != AccessibleKey.None && (!editing || key == AccessibleKey.Confirm || key == AccessibleKey.Back))
                KeyboardNavigator.Dispatch(key);
        }

        if (!editing)
        {
            HandleDirections(pad, layer);
            HandleRightStick(pad, allowPaging: true);
        }
    }

    // D-pad or left stick, with the game's hold-to-repeat timing.
    private void HandleDirections(Gamepad pad, Layer layer)
    {
        int direction = ReadDirection(pad.dpad.ReadValue() + pad.leftStick.ReadValue());
        if (direction < 0)
        {
            _heldDirection = -1;
            return;
        }

        bool fire = direction != _heldDirection || Time.unscaledTime >= _nextRepeatTime;
        if (!fire) return;

        _nextRepeatTime = Time.unscaledTime + (direction != _heldDirection ? RepeatDelaySeconds : RepeatIntervalSeconds);
        _heldDirection = direction;
        KeyboardNavigator.Dispatch(Directions[(int)layer][direction]);
    }

    // Right stick: up/down = message history, left/right = page up/down. One step per push.
    private void HandleRightStick(Gamepad pad, bool allowPaging)
    {
        int direction = ReadDirection(pad.rightStick.ReadValue());
        if (direction == _rightStickDirection) return;

        _rightStickDirection = direction;
        switch (direction)
        {
            case 0: KeyboardNavigator.Dispatch(AccessibleKey.PrevMessage); break;
            case 1: KeyboardNavigator.Dispatch(AccessibleKey.NextMessage); break;
            case 2 when allowPaging: KeyboardNavigator.Dispatch(AccessibleKey.PageUp); break;
            case 3 when allowPaging: KeyboardNavigator.Dispatch(AccessibleKey.PageDown); break;
        }
    }

    // 0 = up, 1 = down, 2 = left, 3 = right, -1 = centred. The dominant axis wins.
    private static int ReadDirection(Vector2 value)
    {
        if (value.magnitude < StickDeadzone) return -1;
        if (Mathf.Abs(value.y) >= Mathf.Abs(value.x)) return value.y > 0 ? 0 : 1;
        return value.x < 0 ? 2 : 3;
    }

    // Same path as the keyboard's Escape (PopupManager.OnOpenSettings → HandleEscape): close the top popup or open the pause menu.
    internal static void SendEscape()
    {
        var popupManager = Services.Get<PopupManager>();
        if (popupManager == null) return;
        AccessTools.Method(typeof(PopupManager), "HandleEscape")?.Invoke(popupManager, new object[] { true });
    }
}
