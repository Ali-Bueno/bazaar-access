using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BazaarAccess.Accessibility;
using BepInEx;
using HarmonyLib;
using TheBazaar.AppFramework;
using TheBazaar.Inputs;
using TheBazaar.Inputs.Navigation;
using UnityEngine;

namespace BazaarAccess.Core;

/// <summary>
/// Developer test mode (config "Debug.TestMode"): runs commands written to BepInEx/BazaarAccess-commands.txt,
/// one per line, so a session can be driven without a keyboard or controller. Every spoken line is logged as "SPEECH:".
/// Commands: key &lt;AccessibleKey&gt; | nav &lt;NavCommand&gt; (game's controller navigation) | scheme &lt;InputScheme&gt; |
/// click &lt;button GameObject name&gt; | escape | wait &lt;seconds&gt; | where.
/// </summary>
public class TestCommandRunner : MonoBehaviour
{
    private const float PollIntervalSeconds = 0.25f;
    private const float DefaultStepDelaySeconds = 0.3f; // lets the game process one command before the next

    private static readonly string CommandFile = Path.Combine(Paths.BepInExRootPath, "BazaarAccess-commands.txt");

    private readonly Queue<string> _pending = new Queue<string>();
    private float _nextPoll;
    private bool _running;

    public static void Create(GameObject parent)
    {
        if (Plugin.TestMode.Value) parent.AddComponent<TestCommandRunner>();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + PollIntervalSeconds;

        if (!File.Exists(CommandFile)) return;
        try
        {
            foreach (var line in File.ReadAllLines(CommandFile))
                if (!string.IsNullOrWhiteSpace(line)) _pending.Enqueue(line.Trim());
            File.Delete(CommandFile);
        }
        catch (IOException)
        {
            return; // still being written; retry next poll
        }

        if (!_running) StartCoroutine(RunPending());
    }

    private IEnumerator RunPending()
    {
        _running = true;
        while (_pending.Count > 0)
        {
            string line = _pending.Dequeue();
            Plugin.Logger.LogInfo($"TEST> {line}");
            float delay = DefaultStepDelaySeconds;
            try
            {
                delay = Execute(line);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"TEST error: {ex.Message}");
            }
            yield return new WaitForSecondsRealtime(delay);
        }
        _running = false;
    }

    // Returns how long to wait before the next command.
    private static float Execute(string line)
    {
        var parts = line.Split(new[] { ' ' }, 2);
        string arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (parts[0].ToLowerInvariant())
        {
            case "key":
                KeyboardNavigator.Dispatch((AccessibleKey)Enum.Parse(typeof(AccessibleKey), arg, true));
                break;
            case "nav":
                var navigator = FindObjectOfType<GameplayNavigator>();
                if (navigator == null) Plugin.Logger.LogInfo("TEST: no GameplayNavigator");
                else AccessTools.Method(typeof(GameplayNavigator), "Dispatch")
                    .Invoke(navigator, new object[] { Enum.Parse(typeof(NavCommand), arg, true) });
                break;
            case "scheme":
                // The game's controller navigation only runs in the Gamepad scheme, normally set by real pad input.
                var scheme = Services.Get<InputManager>()?.Scheme;
                AccessTools.Method(typeof(InputSchemeTracker), "UpdateScheme")
                    ?.Invoke(scheme, new object[] { Enum.Parse(typeof(InputScheme), arg, true) });
                break;
            case "click":
                // Presses a game button by GameObject name, even a hidden one (e.g. LoadoutButton during a run).
                var target = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>()
                    .FirstOrDefault(b => b.name == arg && b.gameObject.scene.IsValid());
                if (target == null) Plugin.Logger.LogInfo($"TEST: no button '{arg}'");
                else target.onClick.Invoke();
                break;
            case "escape":
                GamepadNavigator.SendEscape();
                break;
            case "wait":
                return float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
            case "where":
                var focus = FindObjectOfType<GameplayNavigator>()?.CurrentFocus;
                Plugin.Logger.LogInfo($"TEST: screen={AccessibilityMgr.GetCurrentScreen()?.GetType().Name}, ui={AccessibilityMgr.GetFocusedUI()?.GetType().Name}, gameFocus={focus?.GetType().Name}");
                break;
            default:
                Plugin.Logger.LogWarning($"TEST: unknown command '{parts[0]}'");
                break;
        }
        return DefaultStepDelaySeconds;
    }
}
