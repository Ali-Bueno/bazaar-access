using System.Linq;
using TheBazaar.AppFramework;
using TheBazaar.Inputs;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace BazaarAccess.Core;

/// <summary>
/// Hides gamepads from the game's input actions while the mod drives the controller, so a button
/// press is never handled twice; clearing the filter hands the controller back to the game.
/// </summary>
public static class GamepadOwnership
{
    private static InputActionAsset _appliedAsset;
    private static bool _modOwns;

    public static void Initialize() => InputSystem.onDeviceChange += OnDeviceChange;

    public static void Shutdown()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (_appliedAsset != null) _appliedAsset.devices = null;
        _appliedAsset = null;
    }

    /// <summary>
    /// Gives the controller to the mod (game actions ignore gamepads) or back to the game; cheap when nothing changed.
    /// </summary>
    public static void Update(bool modOwns)
    {
        var inputManager = Services.Get<InputManager>();
        var asset = inputManager != null && inputManager.IsReady ? inputManager.Actions?.asset : null;
        if (asset == null || (asset == _appliedAsset && modOwns == _modOwns)) return;

        _modOwns = modOwns;
        Apply(asset);
    }

    private static void Apply(InputActionAsset asset)
    {
        asset.devices = _modOwns
            ? new ReadOnlyArray<InputDevice>(InputSystem.devices.Where(d => !(d is Gamepad)).ToArray())
            : (ReadOnlyArray<InputDevice>?)null;
        _appliedAsset = asset;
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        bool connected = change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected;
        bool disconnected = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;
        if (!connected && !disconnected) return;

        // A new keyboard/gamepad must be re-filtered: the device list is a snapshot.
        if (_appliedAsset != null) Apply(_appliedAsset);

        if (device is Gamepad)
            TolkWrapper.Speak(Loc.T(connected ? "gamepad.connected" : "gamepad.disconnected"));
    }
}

/// <summary>Who handles the controller (config "Controller.Mode").</summary>
public enum ControllerMode
{
    Hybrid,
    KeyboardEmulation,
    GameOnly
}
