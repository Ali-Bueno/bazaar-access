using System;
using System.Reflection;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using BazaarAccess.UI;
using HarmonyLib;
using TheBazaar;
using TMPro;
using UnityEngine;

namespace BazaarAccess.Patches;

/// <summary>
/// Hook en PopupBase.Show para detectar cuando se abre un popup.
/// </summary>
[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Show))]
public static class PopupShowPatch
{
    private static IAccessibleUI _currentPopupUI;

    static void Postfix(PopupBase __instance)
    {
        string popupName = __instance.GetType().Name;
        Plugin.Logger.LogInfo($"PopupBase.Show: {popupName}");

        try
        {
            // Crear UI accesible según el tipo de popup
            if (__instance is GenericPopup genericPopup)
            {
                _currentPopupUI = GenericPopupUI.CreateFromPopup(genericPopup);
                AccessibilityMgr.ShowUI(_currentPopupUI);
            }
            else
            {
                // Para otros tipos de popup, intentar extraer texto genérico
                string title = ExtractTextFromChild(__instance.transform, "_titleLabel", "Title", "TitleText");
                string message = ExtractTextFromChild(__instance.transform, "_messageLabel", "Message", "MessageText", "Text");

                if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(message))
                {
                    _currentPopupUI = new GenericPopupUI(__instance.transform, title, message);
                    AccessibilityMgr.ShowUI(_currentPopupUI);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"PopupShowPatch error: {ex.Message}");
        }
    }

    private static string ExtractTextFromChild(Transform root, params string[] names)
    {
        foreach (var name in names)
        {
            var child = root.Find(name);
            if (child != null)
            {
                var tmp = child.GetComponent<TMP_Text>();
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text.Trim();
            }
        }

        // Buscar por nombre de GameObject
        var allTmp = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var tmp in allTmp)
        {
            foreach (var name in names)
            {
                if (tmp.gameObject.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!string.IsNullOrWhiteSpace(tmp.text))
                        return tmp.text.Trim();
                }
            }
        }

        return "";
    }

    public static void ClearCurrentUI()
    {
        _currentPopupUI = null;
    }
}

/// <summary>
/// Hook en PopupBase.Hide para detectar cuando se cierra un popup.
/// </summary>
[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
public static class PopupHidePatch
{
    static void Postfix(PopupBase __instance)
    {
        string popupName = __instance.GetType().Name;
        Plugin.Logger.LogInfo($"PopupBase.Hide: {popupName}");

        // Pop de la UI si había una
        AccessibilityMgr.PopUI();
        PopupShowPatch.ClearCurrentUI();
    }
}

/// <summary>
/// Hook en SequenceDialogController para capturar diálogos de tutorial.
/// </summary>
[HarmonyPatch]
public static class TutorialDialogPatch
{
    private static MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            var type = typeof(PopupBase).Assembly.GetType("TheBazaar.SequenceDialogController");
            if (type != null)
            {
                _targetMethod = type.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("TutorialDialogPatch: Found SequenceDialogController.Show");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("TutorialDialogPatch: Could not find SequenceDialogController.Show - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"TutorialDialogPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static MethodBase TargetMethod() => _targetMethod;

    static void Postfix(object __instance)
    {
        try
        {
            // Obtener el componente de secuencia para extraer el texto
            var sequenceField = __instance.GetType().GetField("_nodeSequenceComponent",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (sequenceField != null)
            {
                var sequenceComponent = sequenceField.GetValue(__instance);
                if (sequenceComponent != null)
                {
                    // Obtener la propiedad Text
                    var textProp = sequenceComponent.GetType().GetProperty("Text");
                    if (textProp != null)
                    {
                        var textObj = textProp.GetValue(sequenceComponent);
                        if (textObj != null)
                        {
                            string text = textObj.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                Plugin.Logger.LogInfo($"Tutorial: {text}");
                                MessageBuffer.Add(text);
                                TolkWrapper.Speak(text);
                                return;
                            }
                        }
                    }
                }
            }

            // Fallback: buscar TMP_Text en el objeto
            var monoBehaviour = __instance as MonoBehaviour;
            if (monoBehaviour != null)
            {
                var tmpTexts = monoBehaviour.GetComponentsInChildren<TMP_Text>(true);
                foreach (var tmp in tmpTexts)
                {
                    if (tmp.gameObject.name.Contains("Dialog") ||
                        tmp.gameObject.name.Contains("Text") ||
                        tmp.gameObject.name.Contains("Message"))
                    {
                        if (!string.IsNullOrWhiteSpace(tmp.text))
                        {
                            string text = tmp.text.Trim();
                            if (!MessageBuffer.ContainsRecent(text))
                            {
                                Plugin.Logger.LogInfo($"Tutorial (fallback): {text}");
                                MessageBuffer.Add(text);
                                TolkWrapper.Speak(text);
                            }
                            return;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"TutorialDialogPatch error: {ex.Message}");
        }
    }
}

/// <summary>
/// Hook en ImageSequenceDialogController (tutoriales con imágenes).
/// </summary>
[HarmonyPatch]
public static class ImageTutorialPatch
{
    private static MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            // ImageSequenceDialogController está en el namespace global
            var type = typeof(PopupBase).Assembly.GetType("ImageSequenceDialogController");
            if (type != null)
            {
                _targetMethod = type.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("ImageTutorialPatch: Found ImageSequenceDialogController.Show");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("ImageTutorialPatch: Could not find ImageSequenceDialogController.Show - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ImageTutorialPatch.Prepare error: {ex.Message}");
        }
        return false; // No aplicar este patch
    }

    static MethodBase TargetMethod()
    {
        return _targetMethod;
    }

    static void Postfix(object __instance)
    {
        try
        {
            var monoBehaviour = __instance as MonoBehaviour;
            if (monoBehaviour == null) return;

            var tmpTexts = monoBehaviour.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmpTexts)
            {
                if (!string.IsNullOrWhiteSpace(tmp.text))
                {
                    string text = tmp.text.Trim();
                    if (!MessageBuffer.ContainsRecent(text))
                    {
                        Plugin.Logger.LogInfo($"Image tutorial: {text}");
                        MessageBuffer.Add(text);
                        TolkWrapper.Speak(text);
                    }
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ImageTutorialPatch error: {ex.Message}");
        }
    }
}

/// <summary>
/// Hook en BasePointerDialogController para otros diálogos.
/// </summary>
[HarmonyPatch]
public static class BaseDialogPatch
{
    private static MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            var type = typeof(PopupBase).Assembly.GetType("TheBazaar.BasePointerDialogController");
            if (type != null)
            {
                _targetMethod = type.GetMethod("ShowDialog", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("BaseDialogPatch: Found BasePointerDialogController.ShowDialog");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("BaseDialogPatch: Could not find method - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"BaseDialogPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static MethodBase TargetMethod() => _targetMethod;

    static void Postfix(object __instance)
    {
        try
        {
            var dialogTextField = __instance.GetType().GetField("_dialogText",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (dialogTextField != null)
            {
                var dialogText = dialogTextField.GetValue(__instance) as TMP_Text;
                if (dialogText != null && !string.IsNullOrWhiteSpace(dialogText.text))
                {
                    string text = dialogText.text.Trim();
                    if (!MessageBuffer.ContainsRecent(text))
                    {
                        Plugin.Logger.LogInfo($"Dialog: {text}");
                        MessageBuffer.Add(text);
                        TolkWrapper.Speak(text);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"BaseDialogPatch error: {ex.Message}");
        }
    }
}

/// <summary>
/// Hook para ResultComponent (diálogos de confirmación como rendirse).
/// </summary>
[HarmonyPatch]
public static class ResultComponentPatch
{
    private static MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            // ResultComponent está en TheBazaar.Store namespace
            var type = typeof(PopupBase).Assembly.GetType("TheBazaar.Store.ResultComponent");
            if (type == null)
                type = typeof(PopupBase).Assembly.GetType("TheBazaar.ResultComponent");

            if (type != null)
            {
                _targetMethod = type.GetMethod("Show", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo($"ResultComponentPatch: Found {_targetMethod.Name}");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("ResultComponentPatch: Could not find ResultComponent.Show - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ResultComponentPatch.Prepare error: {ex.Message}");
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

            // Esperar un frame para que se inicialice el texto
            Plugin.Instance.StartCoroutine(DelayedRead(monoBehaviour));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ResultComponentPatch error: {ex.Message}");
        }
    }

    private static System.Collections.IEnumerator DelayedRead(MonoBehaviour component)
    {
        yield return null; // Esperar un frame

        try
        {
            var tmpTexts = component.GetComponentsInChildren<TMP_Text>(true);
            var texts = new System.Collections.Generic.List<string>();

            foreach (var tmp in tmpTexts)
            {
                if (!string.IsNullOrWhiteSpace(tmp.text) && tmp.gameObject.activeInHierarchy)
                {
                    string text = tmp.text.Trim();
                    // Filtrar textos muy cortos o que sean solo símbolos
                    if (text.Length > 1 && !texts.Contains(text))
                    {
                        texts.Add(text);
                    }
                }
            }

            if (texts.Count > 0)
            {
                string fullText = string.Join(". ", texts);
                Plugin.Logger.LogInfo($"ResultComponent: {fullText}");
                MessageBuffer.Add(fullText);
                TolkWrapper.Speak(fullText);

                // Crear UI de confirmación si hay botones
                var ui = new ConfirmationDialogUI(component.transform, fullText);
                AccessibilityMgr.ShowUI(ui);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ResultComponentPatch DelayedRead error: {ex.Message}");
        }
    }
}

/// <summary>
/// Hook para BazaarConfirmationDialogController.
/// Firma: Open(string confirmationText, Action confirmationAction, BazaarSaleItem SaleItem)
/// </summary>
[HarmonyPatch]
public static class ConfirmationDialogPatch
{
    private static MethodBase _targetMethod;

    static bool Prepare()
    {
        try
        {
            var type = typeof(PopupBase).Assembly.GetType("TheBazaar.BazaarConfirmationDialogController");
            if (type != null)
            {
                _targetMethod = type.GetMethod("Open", BindingFlags.Public | BindingFlags.Instance);
                if (_targetMethod != null)
                {
                    Plugin.Logger.LogInfo("ConfirmationDialogPatch: Found BazaarConfirmationDialogController.Open");
                    return true;
                }
            }
            Plugin.Logger.LogWarning("ConfirmationDialogPatch: Could not find method - skipping patch");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ConfirmationDialogPatch.Prepare error: {ex.Message}");
        }
        return false;
    }

    static MethodBase TargetMethod() => _targetMethod;

    // Usar object[] __args para capturar todos los parámetros sin importar la firma
    static void Postfix(object __instance, object[] __args)
    {
        try
        {
            // El primer argumento es confirmationText
            string confirmationText = __args?.Length > 0 ? __args[0] as string : null;
            if (string.IsNullOrEmpty(confirmationText)) return;

            Plugin.Logger.LogInfo($"Confirmation dialog: {confirmationText}");
            MessageBuffer.Add(confirmationText);
            TolkWrapper.Speak(confirmationText);

            var monoBehaviour = __instance as MonoBehaviour;
            if (monoBehaviour != null)
            {
                var ui = new ConfirmationDialogUI(monoBehaviour.transform, confirmationText);
                AccessibilityMgr.ShowUI(ui);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"ConfirmationDialogPatch error: {ex.Message}");
        }
    }
}
