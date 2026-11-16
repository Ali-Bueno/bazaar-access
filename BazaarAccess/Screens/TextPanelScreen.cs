using System.Collections.Generic;
using System.Linq;
using BazaarAccess.Accessibility;
using BazaarAccess.Core;
using TMPro;
using UnityEngine;

namespace BazaarAccess.Screens;

/// <summary>
/// Read-only screen for informational panels (player profile...): every visible text under the root,
/// top to bottom, with the texts of one container (label + value) joined into one line. Backspace goes home.
/// </summary>
public class TextPanelScreen : BaseScreen
{
    private readonly string _title;

    public TextPanelScreen(Transform root, string title) : base(root)
    {
        _title = title;
        Menu.Clear();
        BuildMenu();
    }

    public override string ScreenName => _title;

    public int LineCount { get; private set; }

    protected override void BuildMenu()
    {
        var lines = ReadLines(Root);
        LineCount = lines.Count;
        foreach (var line in lines)
        {
            string text = line;
            Menu.AddOption(() => text, () => TolkWrapper.Speak(text));
        }
    }

    /// <summary>Re-reads the panel (its data often loads after the view opens).</summary>
    public void Rebuild()
    {
        Menu.Clear();
        BuildMenu();
    }

    public override void HandleInput(AccessibleKey key)
    {
        if (key == AccessibleKey.Back)
        {
            NavigationMenuHelper.TryGoHome();
            return;
        }
        base.HandleInput(key);
    }

    public static List<string> ReadLines(Transform root)
    {
        var items = new List<TextItem>();
        var corners = new Vector3[4];

        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
        {
            if (!tmp.isActiveAndEnabled) continue;
            string text = TextHelper.CleanText(tmp.text);
            if (string.IsNullOrWhiteSpace(text)) continue;

            tmp.rectTransform.GetWorldCorners(corners); // 0 = bottom-left, 1 = top-left
            items.Add(new TextItem { Y = (corners[0].y + corners[1].y) / 2f, X = corners[0].x, Text = text.Trim(), Group = tmp.transform.parent });
        }

        // Texts sharing a container are one entry (a stat tile's value and label, a header and its value);
        // entries are read top to bottom, left to right.
        var lines = new List<string>();
        foreach (var group in items.GroupBy(i => i.Group)
                     .OrderByDescending(g => Mathf.Round(g.Max(i => i.Y) / RowSnap))
                     .ThenBy(g => g.Min(i => i.X)))
        {
            var texts = group.OrderByDescending(i => i.Y).ThenBy(i => i.X).Select(i => i.Text).Distinct().ToList();
            // A tile shows its number above its label: read "label: number".
            string line = texts.Count == 2 && IsNumber(texts[0]) && !IsNumber(texts[1])
                ? $"{texts[1]}: {texts[0]}"
                : string.Join(", ", texts);
            if (lines.Count == 0 || lines[lines.Count - 1] != line) lines.Add(line);
        }
        return lines;
    }

    // Entries whose tops are this close (world units) count as the same row when ordering.
    private const float RowSnap = 10f;

    // "39", "10:35", "1,18K" (the game abbreviates thousands/millions with K/M/B).
    private static bool IsNumber(string text)
    {
        string body = text.TrimEnd('K', 'M', 'B', 'k', 'm', 'b');
        return body.Any(char.IsDigit) && !body.Any(char.IsLetter);
    }

    private class TextItem
    {
        public float Y, X;
        public string Text;
        public Transform Group;
    }
}
