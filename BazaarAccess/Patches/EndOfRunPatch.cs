using System;
using System.Collections;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarAccess.Patches;

/// <summary>
/// Hace accesible la pantalla de fin de run (estadísticas, cofres, rank, etc.)
/// </summary>
[HarmonyPatch]
public static class EndOfRunPatch
{
    private static MethodBase _targetMethod;
    private static EndOfRunUI _currentUI;

    static bool Prepare()
    {
        try
        {
            var type = typeof(TheBazaar.PopupBase).Assembly.GetType("TheBazaar.UI.EndOfRun.EndOfRunScreenController");
            if (type != null)
            {
                _targetMethod = type.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("EndOfRunPatch: Found EndOfRunScreenController.Start");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("EndOfRunPatch: Could not find EndOfRunScreenController.Start - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"EndOfRunPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static MethodBase TargetMethod() => _targetMethod;

    static void Postfix(object __instance)
    {
        try
        {
            var monoBehaviour = __instance as MonoBehaviour;
            if (monoBehaviour == null) return;

            Plugin.Logger.LogInfo("EndOfRunScreenController.Start - Creating accessible UI");

            // Esperar a que la UI se inicialice
            Plugin.Instance.StartCoroutine(DelayedCreateUI(monoBehaviour));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"EndOfRunPatch error: {ex.Message}");
        }
    }

    private static IEnumerator DelayedCreateUI(MonoBehaviour controller)
    {
        // Esperar a que la animación inicial termine
        yield return new WaitForSeconds(1.5f);

        try
        {
            _currentUI = new EndOfRunUI(controller.transform);
            AccessibilityMgr.SetScreen(_currentUI);

            TolkWrapper.Speak("End of run. Press Enter to continue.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"EndOfRunPatch DelayedCreateUI error: {ex.Message}");
        }
    }
}

/// <summary>
/// UI accesible para la pantalla de fin de run.
/// </summary>
public class EndOfRunUI : IAccessibleScreen
{
    public string ScreenName => "End of Run";

    private readonly Transform _root;
    private Button _continueButton;

    public EndOfRunUI(Transform root)
    {
        _root = root;
        FindContinueButton();
    }

    private void FindContinueButton()
    {
        if (_root == null) return;

        // Buscar el botón de continuar
        var buttons = _root.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string name = btn.gameObject.name.ToLower();
            if (name.Contains("continue") || name.Contains("next"))
            {
                _continueButton = btn;
                Plugin.Logger.LogInfo($"EndOfRunUI: Found continue button '{btn.gameObject.name}'");
                break;
            }
        }

        // Si no encontró por nombre, buscar el primer botón interactable
        if (_continueButton == null && buttons.Length > 0)
        {
            foreach (var btn in buttons)
            {
                if (btn.interactable && btn.gameObject.activeInHierarchy)
                {
                    _continueButton = btn;
                    Plugin.Logger.LogInfo($"EndOfRunUI: Using first active button '{btn.gameObject.name}'");
                    break;
                }
            }
        }
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Confirm:
                ClickContinue();
                break;

            case AccessibleKey.Back:
                // También continuar con Escape
                ClickContinue();
                break;

            case AccessibleKey.Up:
            case AccessibleKey.Down:
            case AccessibleKey.Left:
            case AccessibleKey.Right:
                // Leer la información de la pantalla actual
                ReadCurrentScreen();
                break;
        }
    }

    private void ClickContinue()
    {
        // Refrescar el botón en caso de que haya cambiado
        FindContinueButton();

        if (_continueButton != null && _continueButton.interactable)
        {
            Plugin.Logger.LogInfo("EndOfRunUI: Clicking continue button");
            _continueButton.onClick?.Invoke();
            TolkWrapper.Speak("Continue");

            // Esperar y volver a buscar el botón después de la transición
            Plugin.Instance.StartCoroutine(DelayedRefresh());
        }
        else
        {
            TolkWrapper.Speak("Continue button not available");
        }
    }

    private System.Collections.IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(0.8f);
        FindContinueButton();
        ReadCurrentScreen();
    }

    private void ReadCurrentScreen()
    {
        if (_root == null) return;

        // Buscar textos visibles en la pantalla
        var texts = _root.GetComponentsInChildren<TMP_Text>(true);
        var visibleTexts = new System.Collections.Generic.List<string>();

        foreach (var text in texts)
        {
            if (text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
            {
                string t = text.text.Trim();
                // Filtrar textos muy cortos o duplicados
                if (t.Length > 2 && !visibleTexts.Contains(t))
                {
                    visibleTexts.Add(t);
                }
            }
        }

        if (visibleTexts.Count > 0)
        {
            // Leer los primeros textos relevantes
            string summary = string.Join(". ", visibleTexts.GetRange(0, Math.Min(5, visibleTexts.Count)));
            TolkWrapper.Speak(summary);
        }
        else
        {
            TolkWrapper.Speak("End of run screen");
        }
    }

    public string GetHelp()
    {
        return "Enter or Escape: Continue to next screen. Arrows: Read current screen info.";
    }

    public void OnFocus()
    {
        TolkWrapper.Speak("End of run. Press Enter to continue.");
    }

    public bool IsValid()
    {
        return _root != null && _root.gameObject.activeInHierarchy;
    }
}
