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
            // El mensaje se anuncia en OnFocus() del UI
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"EndOfRunPatch DelayedCreateUI error: {ex.Message}");
        }
    }
}

/// <summary>
/// UI accesible para la pantalla de fin de run.
/// Permite navegar línea por línea con flechas arriba/abajo.
/// </summary>
public class EndOfRunUI : IAccessibleScreen
{
    public string ScreenName => "End of Run";

    private readonly Transform _root;
    private Button _continueButton;
    private System.Collections.Generic.List<string> _textLines = new System.Collections.Generic.List<string>();
    private int _currentLineIndex = -1;
    private string _lastScreenState = "";

    public EndOfRunUI(Transform root)
    {
        _root = root;
        RefreshScreen();
    }

    private void RefreshScreen()
    {
        FindContinueButton();
        RefreshTextLines();
    }

    private void FindContinueButton()
    {
        if (_root == null) return;

        _continueButton = null;

        // Buscar el botón de continuar
        var buttons = _root.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            if (!btn.gameObject.activeInHierarchy || !btn.interactable) continue;

            string name = btn.gameObject.name.ToLower();
            if (name.Contains("continue") || name.Contains("next") || name.Contains("claim"))
            {
                _continueButton = btn;
                Plugin.Logger.LogInfo($"EndOfRunUI: Found continue button '{btn.gameObject.name}'");
                return;
            }
        }

        // Si no encontró por nombre, buscar el primer botón interactable
        foreach (var btn in buttons)
        {
            if (btn.interactable && btn.gameObject.activeInHierarchy)
            {
                _continueButton = btn;
                Plugin.Logger.LogInfo($"EndOfRunUI: Using first active button '{btn.gameObject.name}'");
                return;
            }
        }
    }

    private void RefreshTextLines()
    {
        if (_root == null) return;

        var newLines = new System.Collections.Generic.List<string>();

        // Buscar todos los textos visibles
        var allTexts = _root.GetComponentsInChildren<TMP_Text>(true);
        var allTextItems = new System.Collections.Generic.List<TextWithPosition>();

        foreach (var text in allTexts)
        {
            if (!text.gameObject.activeInHierarchy) continue;
            if (string.IsNullOrWhiteSpace(text.text)) continue;

            string t = CleanText(text.text);
            if (string.IsNullOrEmpty(t)) continue;
            if (IsUselessText(t)) continue;
            if (IsGenericButtonText(t)) continue;

            var pos = text.rectTransform.position;

            allTextItems.Add(new TextWithPosition
            {
                Text = t,
                X = pos.x,
                Y = pos.y,
                Parent = text.transform.parent
            });
        }

        var allItems = new System.Collections.Generic.List<LineWithY>();
        var usedItems = new System.Collections.Generic.HashSet<TextWithPosition>();

        // PASO 1: Agrupar por padre para estadísticas (label + valor)
        var parentGroups = new System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<TextWithPosition>>();
        foreach (var item in allTextItems)
        {
            if (!parentGroups.ContainsKey(item.Parent))
            {
                parentGroups[item.Parent] = new System.Collections.Generic.List<TextWithPosition>();
            }
            parentGroups[item.Parent].Add(item);
        }

        foreach (var kvp in parentGroups)
        {
            var texts = kvp.Value;
            if (texts.Count == 2)
            {
                texts.Sort((a, b) => a.X.CompareTo(b.X));
                string first = texts[0].Text;
                string second = texts[1].Text;

                // Combinar label + valor numérico
                if (IsLabel(first) && IsStrictValue(second))
                {
                    string line = first.EndsWith(":") ? $"{first} {second}" : $"{first}: {second}";
                    allItems.Add(new LineWithY { Text = line, Y = texts[0].Y });
                    usedItems.Add(texts[0]);
                    usedItems.Add(texts[1]);
                }
                else if (IsStrictValue(first) && IsLabel(second))
                {
                    string line = $"{second}: {first}";
                    allItems.Add(new LineWithY { Text = line, Y = texts[0].Y });
                    usedItems.Add(texts[0]);
                    usedItems.Add(texts[1]);
                }
            }
        }

        // PASO 2: Para items no usados, agrupar por Y para desafíos
        var remainingItems = new System.Collections.Generic.List<TextWithPosition>();
        foreach (var item in allTextItems)
        {
            if (!usedItems.Contains(item))
            {
                remainingItems.Add(item);
            }
        }

        // Agrupar por Y similar - tolerancia de 15 unidades
        var rowGroups = new System.Collections.Generic.List<System.Collections.Generic.List<TextWithPosition>>();
        float yTolerance = 15f;

        foreach (var item in remainingItems)
        {
            bool addedToGroup = false;
            foreach (var group in rowGroups)
            {
                if (Mathf.Abs(group[0].Y - item.Y) < yTolerance)
                {
                    group.Add(item);
                    addedToGroup = true;
                    break;
                }
            }
            if (!addedToGroup)
            {
                var newGroup = new System.Collections.Generic.List<TextWithPosition> { item };
                rowGroups.Add(newGroup);
            }
        }

        // Procesar cada fila de desafíos
        foreach (var row in rowGroups)
        {
            row.Sort((a, b) => a.X.CompareTo(b.X));

            // Detectar headers de sección y agregarlos por separado
            foreach (var item in row)
            {
                if (IsSectionHeader(item.Text))
                {
                    allItems.Add(new LineWithY { Text = item.Text, Y = item.Y + 1000 }); // Headers primero
                }
            }

            // Filtrar headers de la fila
            var nonHeaders = new System.Collections.Generic.List<TextWithPosition>();
            foreach (var item in row)
            {
                if (!IsSectionHeader(item.Text) && !IsPlayerTitle(item.Text))
                {
                    nonHeaders.Add(item);
                }
            }

            if (nonHeaders.Count == 0) continue;

            // Solo combinar si hay spread horizontal significativo (> 50 unidades entre items)
            float minX = nonHeaders[0].X;
            float maxX = nonHeaders[nonHeaders.Count - 1].X;
            bool hasHorizontalSpread = (maxX - minX) > 50f;

            if (nonHeaders.Count > 1 && hasHorizontalSpread)
            {
                string combined = TryCombineChallengeRow(nonHeaders);
                if (!string.IsNullOrEmpty(combined))
                {
                    allItems.Add(new LineWithY { Text = combined, Y = nonHeaders[0].Y });
                    continue;
                }
            }

            // Si no se combinó, añadir individualmente
            foreach (var item in nonHeaders)
            {
                if (!IsOrphanNumber(item.Text))
                {
                    allItems.Add(new LineWithY { Text = item.Text, Y = item.Y });
                }
            }
        }

        // Ordenar por Y (de arriba a abajo)
        allItems.Sort((a, b) => b.Y.CompareTo(a.Y));

        // Extraer líneas únicas
        foreach (var item in allItems)
        {
            if (!newLines.Contains(item.Text))
            {
                newLines.Add(item.Text);
            }
        }

        // Verificar si la pantalla cambió
        string newState = string.Join("|", newLines);
        if (newState != _lastScreenState)
        {
            _textLines = newLines;
            _currentLineIndex = -1;
            _lastScreenState = newState;
            Plugin.Logger.LogInfo($"EndOfRunUI: Refreshed with {_textLines.Count} text lines");
            foreach (var line in _textLines)
            {
                Plugin.Logger.LogInfo($"  Line: {line}");
            }
        }
    }

    private string TryCombineChallengeRow(System.Collections.Generic.List<TextWithPosition> row)
    {
        if (row.Count == 0) return null;
        if (row.Count == 1) return row[0].Text;

        // Filtrar textos que son headers de sección o nombres de usuario
        var validTexts = new System.Collections.Generic.List<TextWithPosition>();
        foreach (var item in row)
        {
            string t = item.Text;
            // Ignorar headers de sección
            if (t == "Daily Challenge" || t == "Weekly Challenges" || t == "Achievements") continue;
            // Ignorar títulos de fundador/supporter
            if (t.Contains("Founder") || t.Contains("Supporter") || t.Contains("Backer")) continue;
            validTexts.Add(item);
        }

        if (validTexts.Count == 0) return null;
        if (validTexts.Count == 1) return validTexts[0].Text;

        // Detectar patrón de CHALLENGE: descripción con acción + progreso + total
        // Ejemplo: ["Purpose", "200", "Use items 200 times.", "77"]
        // Queremos: "Use items 200 times: 77/200"

        string challengeDescription = null;
        string achievementDescription = null;
        string achievementName = null;
        string current = null;
        string total = null;

        foreach (var item in validTexts)
        {
            string t = item.Text;

            // Detectar descripción de CHALLENGE (tiene "times", "items", verbos de acción + número)
            if (t.Length > 10 && ContainsWords(t) && IsChallengeDescription(t))
            {
                challengeDescription = t.TrimEnd('.');
                continue;
            }

            // Detectar descripción de ACHIEVEMENT (Heal X, Start X days/hours, Win X, etc.)
            if (t.Length > 5 && IsAchievementDescription(t))
            {
                achievementDescription = t.TrimEnd('.');
                continue;
            }

            // Buscar números (progreso y total)
            if (IsStrictValue(t))
            {
                int val = 0;
                int.TryParse(t.Replace(",", ""), out val);

                if (total == null)
                {
                    total = t;
                }
                else if (current == null)
                {
                    int totalVal = 0;
                    int.TryParse(total.Replace(",", ""), out totalVal);
                    if (val < totalVal)
                    {
                        current = t;
                    }
                    else
                    {
                        current = total;
                        total = t;
                    }
                }
                continue;
            }

            // Texto corto sin números = nombre de achievement o challenge
            if (t.Length < 25 && !System.Text.RegularExpressions.Regex.IsMatch(t, @"\d"))
            {
                achievementName = t;
            }
        }

        // Formato para CHALLENGE con progreso
        if (challengeDescription != null)
        {
            if (current != null && total != null)
            {
                return $"{challengeDescription}: {current}/{total}";
            }
            if (total != null)
            {
                return $"{challengeDescription}: {total}";
            }
            return challengeDescription;
        }

        // Formato para ACHIEVEMENT: "Nombre: Descripción"
        if (achievementDescription != null && achievementName != null)
        {
            return $"Achievement {achievementName}: {achievementDescription}";
        }
        if (achievementDescription != null)
        {
            return $"Achievement: {achievementDescription}";
        }

        // Fallback: devolver textos válidos separados
        var result = new System.Collections.Generic.List<string>();
        foreach (var item in validTexts)
        {
            if (!IsOrphanNumber(item.Text))
            {
                result.Add(item.Text);
            }
        }
        return result.Count > 0 ? string.Join(", ", result) : null;
    }

    private bool IsChallengeDescription(string text)
    {
        string lower = text.ToLower();
        // Patrones de desafío: "X times", "X items", verbos de acción
        return lower.Contains(" times") || lower.Contains(" items") ||
               (lower.Contains("use ") && lower.Contains(" ")) ||
               (lower.Contains("buy ") && lower.Contains(" ")) ||
               (lower.Contains("sell ") && lower.Contains(" ")) ||
               (lower.Contains("deal ") && lower.Contains(" ")) ||
               (lower.Contains("apply ") && lower.Contains(" ")) ||
               (lower.Contains("earn ") && lower.Contains(" "));
    }

    private bool IsAchievementDescription(string text)
    {
        string lower = text.ToLower();
        // Patrones de achievement: "Heal X", "Start X days", "Win X"
        return (lower.StartsWith("heal ") || lower.StartsWith("start ") ||
                lower.StartsWith("win ") || lower.StartsWith("play ") ||
                lower.StartsWith("reach ") || lower.StartsWith("collect ") ||
                lower.StartsWith("complete ") || lower.StartsWith("unlock ")) &&
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\d");
    }

    private bool IsSectionHeader(string text)
    {
        return text == "Daily Challenge" || text == "Weekly Challenges" ||
               text == "Achievements" || text == "Challenges";
    }

    private bool IsPlayerTitle(string text)
    {
        string lower = text.ToLower();
        return lower.Contains("founder") || lower.Contains("supporter") ||
               lower.Contains("backer") || lower.Contains("grand ");
    }

    private bool ContainsWords(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return text.Split(' ').Length >= 2;
    }

    private bool IsGenericButtonText(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        string lower = text.ToLower();
        return lower == "continue" || lower == "claim" || lower == "next" ||
               lower == "ok" || lower == "close" || lower == "back";
    }

    private class TextWithPosition
    {
        public string Text;
        public float X;
        public float Y;
        public Transform Parent;
    }

    private class LineWithY
    {
        public string Text;
        public float Y;
    }

    private bool IsUselessText(string text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        if (text.Length > 200) return true;

        // Números romanos solos
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[IVXivx]+$")) return true;

        // Solo símbolos o caracteres especiales
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[\s\-_\.\,\:\;\!\?\*\#\@\$\%\^\&\(\)\[\]\{\}\/\\x\+\.]+$")) return true;

        // Patrones como "+...", "...", "+", "x", etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[\+\.\s]+$")) return true;

        // Solo una o dos letras/símbolos sin números
        if (text.Length <= 2 && !System.Text.RegularExpressions.Regex.IsMatch(text, @"\d")) return true;

        return false;
    }

    private bool IsOrphanNumber(string text)
    {
        // Números solos sin contexto
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d+$");
    }

    private bool IsLabel(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (text.Length < 2) return false;

        // Debe contener al menos una letra
        bool hasLetter = false;
        foreach (char c in text)
        {
            if (char.IsLetter(c)) { hasLetter = true; break; }
        }

        return hasLetter;
    }

    private bool IsStrictValue(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // Solo números (posiblemente con separadores)
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[\d\,\.]+$");
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Remover tags HTML/rich text
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        // Limpiar espacios múltiples y saltos de línea
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public void HandleInput(AccessibleKey key)
    {
        switch (key)
        {
            case AccessibleKey.Confirm:
                ClickContinue();
                break;

            case AccessibleKey.Back:
                ClickContinue();
                break;

            case AccessibleKey.Down:
            case AccessibleKey.Right:
                ReadNextLine();
                break;

            case AccessibleKey.Up:
            case AccessibleKey.Left:
                ReadPreviousLine();
                break;
        }
    }

    private void ReadNextLine()
    {
        RefreshTextLines();

        if (_textLines.Count == 0)
        {
            TolkWrapper.Speak("No information available. Press Enter to continue.");
            return;
        }

        _currentLineIndex++;

        if (_currentLineIndex >= _textLines.Count)
        {
            _currentLineIndex = _textLines.Count - 1;
            TolkWrapper.Speak("End of information. Press Enter to continue.");
            return;
        }

        TolkWrapper.Speak(_textLines[_currentLineIndex]);
    }

    private void ReadPreviousLine()
    {
        RefreshTextLines();

        if (_textLines.Count == 0)
        {
            TolkWrapper.Speak("No information available. Press Enter to continue.");
            return;
        }

        if (_currentLineIndex <= 0)
        {
            _currentLineIndex = 0;
            if (_textLines.Count > 0)
            {
                TolkWrapper.Speak($"Start. {_textLines[0]}");
            }
            return;
        }

        _currentLineIndex--;
        TolkWrapper.Speak(_textLines[_currentLineIndex]);
    }

    private void ClickContinue()
    {
        FindContinueButton();

        if (_continueButton != null && _continueButton.interactable)
        {
            Plugin.Logger.LogInfo("EndOfRunUI: Clicking continue button");
            _continueButton.onClick?.Invoke();
            TolkWrapper.Speak("Continue");

            // Esperar y refrescar después de la transición
            Plugin.Instance.StartCoroutine(DelayedRefresh());
        }
        else
        {
            TolkWrapper.Speak("Please wait");
        }
    }

    private System.Collections.IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(1.0f);
        RefreshScreen();

        // Anunciar la nueva pantalla
        if (_textLines.Count > 0)
        {
            _currentLineIndex = 0;
            TolkWrapper.Speak(_textLines[0]);
        }
        else
        {
            TolkWrapper.Speak("Screen changed. Press Enter to continue or arrows to read.");
        }
    }

    public string GetHelp()
    {
        return "Up/Down arrows: Read info line by line. Enter: Continue to next screen.";
    }

    public void OnFocus()
    {
        RefreshScreen();

        if (_textLines.Count > 0)
        {
            _currentLineIndex = 0;
            TolkWrapper.Speak($"End of run. {_textLines[0]}. Use arrows to read more, Enter to continue.");
        }
        else
        {
            TolkWrapper.Speak("End of run. Press Enter to continue.");
        }
    }

    public bool IsValid()
    {
        return _root != null && _root.gameObject.activeInHierarchy;
    }
}
