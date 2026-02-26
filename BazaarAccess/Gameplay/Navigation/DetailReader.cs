using System;
using System.Collections.Generic;
using BazaarGameClient.Domain.Models.Cards;

namespace BazaarAccess.Gameplay.Navigation
{
    /// <summary>
    /// Handles line-by-line detail reading for cards and nav items.
    /// Tracks current position and provides boundary announcements.
    /// </summary>
    public class DetailReader
    {
        private List<string> _lines = new List<string>();
        private int _index = -1;
        private Card _card = null;

        /// <summary>
        /// Whether we have any lines loaded.
        /// </summary>
        public bool HasLines => _lines.Count > 0;

        /// <summary>
        /// The card whose details are currently loaded.
        /// </summary>
        public Card CurrentCard => _card;

        /// <summary>
        /// Initialize with detail lines for a card.
        /// If the card hasn't changed, keeps existing position state.
        /// </summary>
        /// <param name="card">The card to read details for.</param>
        /// <param name="getDetailLines">Function that returns detail lines for a card (e.g. ItemReader.GetDetailLines).</param>
        public void Init(Card card, Func<Card, List<string>> getDetailLines)
        {
            // If same card and already loaded, keep state
            if (card == _card && _lines.Count > 0) return;

            _card = card;
            _lines.Clear();
            _index = -1;

            if (card == null) return;

            _lines = getDetailLines(card);
            _lines.Reverse();
        }

        /// <summary>
        /// Initialize with custom lines (for Exit/Reroll or other non-card nav items).
        /// Always resets state since there is no card to compare against.
        /// </summary>
        /// <param name="lines">The lines to load.</param>
        public void InitCustom(List<string> lines)
        {
            _card = null;
            _lines = lines ?? new List<string>();
            _index = -1;
        }

        /// <summary>
        /// Clear cached state. Call when the selected item changes.
        /// </summary>
        public void Clear()
        {
            _card = null;
            _lines.Clear();
            _index = -1;
        }

        /// <summary>
        /// Read next detail line (Down). Returns the line text to speak, or null if no lines.
        /// Appends "Last line." prefix when at the end of the list.
        /// On first press, starts at index 0.
        /// </summary>
        public string LineDown()
        {
            if (_lines.Count == 0) return null;

            // First press: start at index 0
            if (_index < 0)
            {
                _index = 0;
                return _lines[_index];
            }

            // Already at last line
            if (_index >= _lines.Count - 1)
            {
                return $"Last line. {_lines[_index]}";
            }

            // Move to next line
            _index++;
            return _lines[_index];
        }

        /// <summary>
        /// Read previous detail line (Up). Returns the line text to speak, or null if no lines.
        /// Appends "First line." prefix when at the beginning of the list.
        /// On first press (before any navigation), starts at index 0.
        /// </summary>
        public string LineUp()
        {
            if (_lines.Count == 0) return null;

            // If not started, start at first line
            if (_index < 0)
            {
                _index = 0;
                return _lines[_index];
            }

            // Already at first line
            if (_index <= 0)
            {
                return $"First line. {_lines[_index]}";
            }

            // Move to previous line
            _index--;
            return _lines[_index];
        }
    }
}
