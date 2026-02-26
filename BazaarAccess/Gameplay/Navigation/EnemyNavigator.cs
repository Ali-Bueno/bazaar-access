using System;
using System.Collections.Generic;
using System.Reflection;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using BazaarGameShared.Domain.Runs;
using TheBazaar;
using TheBazaar.AppFramework;

namespace BazaarAccess.Gameplay.Navigation
{
    /// <summary>
    /// Handles all enemy/opponent navigation: items, skills, detail reading.
    /// Extracted from GameplayNavigator to reduce file size.
    /// </summary>
    public class EnemyNavigator
    {
        // State
        private bool _active = false;
        private List<Card> _cards = new List<Card>();
        private List<int> _skillIndices = new List<int>();
        private List<SkillCard> _skills = new List<SkillCard>();
        private int _itemIndex = 0;
        private int _skillIndex = 0;
        private EnemySubsection _subsection = EnemySubsection.Items;

        // Detail reading for enemy items
        private List<string> _detailLines = new List<string>();
        private int _detailIndex = -1;
        private Card _detailCard = null;

        // Properties
        public bool IsActive => _active;
        public EnemySubsection CurrentSubsection => _subsection;
        public bool IsInSkillsSubsection => _active && _subsection == EnemySubsection.Skills;
        public List<Card> Cards => _cards;
        public List<SkillCard> Skills => _skills;
        public int ItemIndex => _itemIndex;

        /// <summary>
        /// Reads enemy info and enters enemy mode if not in combat.
        /// If inCombat is true, only reads stats without entering navigation mode.
        /// </summary>
        public void ReadInfo(bool inCombat)
        {
            try
            {
                var opponent = Data.Run?.Opponent;
                if (opponent == null)
                {
                    TolkWrapper.Speak("No enemy");
                    _active = false;
                    return;
                }

                var parts = new List<string>();

                // Opponent name: only use SimPvpOpponent if actually in PvP combat
                var currentState = Data.CurrentState?.StateName;
                bool isPvpCombat = currentState == ERunState.PVPCombat;

                if (isPvpCombat)
                {
                    var pvpOpponent = Data.SimPvpOpponent;
                    if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
                    {
                        parts.Add($"Opponent: {pvpOpponent.Name}");
                    }
                    else
                    {
                        parts.Add("Enemy");
                    }
                }
                else
                {
                    parts.Add("Enemy");
                }

                // Health
                if (opponent.Attributes.TryGetValue(EPlayerAttributeType.Health, out int health))
                {
                    parts.Add($"Health: {health}");
                }
                if (opponent.Attributes.TryGetValue(EPlayerAttributeType.HealthMax, out int maxHealth))
                {
                    parts.Add($"of {maxHealth}");
                }

                // Shield
                if (opponent.Attributes.TryGetValue(EPlayerAttributeType.Shield, out int shield) && shield > 0)
                {
                    parts.Add($"Shield: {shield}");
                }

                // Only allow item navigation outside of combat
                if (!inCombat)
                {
                    _active = true;
                    Refresh();

                    // Enemy items
                    int enemyItemCount = _cards.Count;
                    if (enemyItemCount > 0)
                    {
                        parts.Add($"{enemyItemCount} items");
                    }
                    if (_skillIndices.Count > 0)
                    {
                        parts.Add($"{_skillIndices.Count} skills");
                    }
                }

                TolkWrapper.Speak(string.Join(", ", parts));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"EnemyNavigator.ReadInfo error: {ex.Message}");
                TolkWrapper.Speak("Cannot read enemy info");
                _active = false;
            }
        }

        /// <summary>
        /// Refreshes the list of enemy items and skills.
        /// Uses Data.GetCards API (source of truth) instead of socket array.
        /// </summary>
        public void Refresh()
        {
            _cards.Clear();
            _skillIndices.Clear();
            _skills.Clear();
            _itemIndex = 0;
            _skillIndex = 0;

            // Use Data.GetCards API (source of truth) instead of socket array
            // Sockets are UI-only and retain stale references after combat
            try
            {
                var cards = Data.GetCards<Card>(ECombatantId.Opponent, EInventorySection.Hand);
                foreach (var card in cards)
                {
                    if (card is ItemCard)
                        _cards.Add(card);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"EnemyNavigator.Refresh: Data.GetCards failed: {ex.Message}");
            }

            // Skills from sockets for visual feedback
            var bm = GetBoardManager();
            if (bm?.opponentSkillSockets != null)
            {
                for (int i = 0; i < bm.opponentSkillSockets.Length; i++)
                {
                    if (bm.opponentSkillSockets[i]?.CardController?.CardData != null)
                        _skillIndices.Add(i);
                }
            }

            // Always try to load skills from Data.Run.Opponent.Skills as fallback
            try
            {
                var opponent = Data.Run?.Opponent;
                if (opponent?.Skills != null)
                {
                    _skills.AddRange(opponent.Skills);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"EnemyNavigator.Refresh: Failed to get Opponent.Skills: {ex.Message}");
            }

            Plugin.Logger.LogInfo($"EnemyNavigator.Refresh: {_cards.Count} items (Data.GetCards), {_skillIndices.Count} socket skills, {_skills.Count} data skills");
        }

        /// <summary>
        /// Exits enemy navigation mode and resets all state.
        /// </summary>
        public void Exit()
        {
            _active = false;
            _itemIndex = 0;
            _skillIndex = 0;
            _subsection = EnemySubsection.Items;
            ClearDetailReading();
        }

        /// <summary>
        /// Enters opponent board navigation mode.
        /// Used during ReplayMode with the G key.
        /// Best used during Recap (E) for static view without Combat Describer.
        /// Sets _active to true if items are available.
        /// Returns true if recap section should be set to EnemyBoard (caller must set it).
        /// </summary>
        public bool EnterBoardMode(bool inRecapMode)
        {
            try
            {
                // Check if Replay is actively running (combat animation playing)
                bool isReplayRunning = IsReplayAnimationActive();

                if (isReplayRunning)
                {
                    TolkWrapper.Speak("Replay in progress. Press E for Recap to view opponent board without interruptions.");
                    return false;
                }

                Refresh();

                int itemCount = _cards.Count;
                int skillCount = GetSkillCount();
                if (itemCount == 0 && skillCount == 0)
                {
                    TolkWrapper.Speak("No opponent items");
                    return false;
                }

                _active = true;
                _itemIndex = 0;
                _skillIndex = 0;
                _subsection = EnemySubsection.Items;
                ClearDetailReading();

                // Get opponent name if available
                string opponentName = "Opponent";
                var pvpOpponent = Data.SimPvpOpponent;
                if (pvpOpponent != null && !string.IsNullOrEmpty(pvpOpponent.Name))
                {
                    opponentName = pvpOpponent.Name;
                }

                // Announce board
                TolkWrapper.Speak($"{opponentName}'s board, {itemCount} items");

                // Tell caller to set recap section if in recap mode
                return inRecapMode;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"EnemyNavigator.EnterBoardMode error: {ex.Message}");
                TolkWrapper.Speak("Cannot access opponent board");
                return false;
            }
        }

        /// <summary>
        /// Navigate to next item in current subsection (Right arrow).
        /// </summary>
        public void NavigateRight()
        {
            if (!_active) return;
            ClearDetailReading();

            if (_subsection == EnemySubsection.Items)
            {
                int itemCount = _cards.Count;
                if (itemCount == 0)
                {
                    TolkWrapper.Speak("No items");
                    return;
                }
                if (_itemIndex >= itemCount - 1)
                {
                    AnnounceCurrentItem(); // Read current at limit
                    return;
                }
                _itemIndex++;
            }
            else // Skills
            {
                int skillCount = GetSkillCount();
                if (skillCount == 0)
                {
                    TolkWrapper.Speak("No skills");
                    return;
                }
                if (_skillIndex >= skillCount - 1)
                {
                    AnnounceCurrentItem();
                    return;
                }
                _skillIndex++;
            }
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Navigate to previous item in current subsection (Left arrow).
        /// </summary>
        public void NavigateLeft()
        {
            if (!_active) return;
            ClearDetailReading();

            if (_subsection == EnemySubsection.Items)
            {
                if (_cards.Count == 0)
                {
                    TolkWrapper.Speak("No items");
                    return;
                }
                if (_itemIndex <= 0)
                {
                    AnnounceCurrentItem();
                    return;
                }
                _itemIndex--;
            }
            else // Skills
            {
                int skillCount = GetSkillCount();
                if (skillCount == 0)
                {
                    TolkWrapper.Speak("No skills");
                    return;
                }
                if (_skillIndex <= 0)
                {
                    AnnounceCurrentItem();
                    return;
                }
                _skillIndex--;
            }
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Switch to next subsection (Ctrl+Right: Items -> Skills).
        /// </summary>
        public void NextSubsection()
        {
            if (!_active) return;
            ClearDetailReading();

            int skillCount = GetSkillCount();
            if (_subsection == EnemySubsection.Items && skillCount > 0)
            {
                _subsection = EnemySubsection.Skills;
                _skillIndex = 0;
                // Announce first skill with description (like hero skills)
                AnnounceSkill();
            }
            else if (_subsection == EnemySubsection.Items)
            {
                TolkWrapper.Speak("No skills");
            }
            else
            {
                // Already in Skills, re-announce current skill
                AnnounceSkill();
            }
        }

        /// <summary>
        /// Switch to previous subsection (Ctrl+Left: Skills -> Items).
        /// </summary>
        public void PreviousSubsection()
        {
            if (!_active) return;
            ClearDetailReading();

            int itemCount = _cards.Count;
            if (_subsection == EnemySubsection.Skills && itemCount > 0)
            {
                _subsection = EnemySubsection.Items;
                _itemIndex = 0;
                TolkWrapper.Speak($"Items, {itemCount}");
            }
            else
            {
                TolkWrapper.Speak(_subsection == EnemySubsection.Items ? "Items" : "Skills");
            }
        }

        /// <summary>
        /// Navigate to next skill (Ctrl+Up in Skills subsection).
        /// </summary>
        public void SkillNext()
        {
            if (!_active || _subsection != EnemySubsection.Skills) return;

            int skillCount = GetSkillCount();
            if (skillCount == 0) return;

            if (_skillIndex >= skillCount - 1)
            {
                // Already at end, just re-announce current skill
                AnnounceSkill();
                return;
            }

            _skillIndex++;
            AnnounceSkill();
        }

        /// <summary>
        /// Navigate to previous skill (Ctrl+Down in Skills subsection).
        /// </summary>
        public void SkillPrevious()
        {
            if (!_active || _subsection != EnemySubsection.Skills) return;

            int skillCount = GetSkillCount();
            if (skillCount == 0) return;

            if (_skillIndex <= 0)
            {
                // Already at start, just re-announce current skill
                AnnounceSkill();
                return;
            }

            _skillIndex--;
            AnnounceSkill();
        }

        /// <summary>
        /// Read next detail line of current enemy item (Ctrl+Up).
        /// </summary>
        public void DetailNext()
        {
            if (!_active) return;

            var card = GetCurrentCard();
            if (card == null)
            {
                TolkWrapper.Speak("No item selected");
                return;
            }

            // Refresh detail lines if card changed - use enemy-focused order
            if (_detailCard != card)
            {
                _detailLines = ItemReader.GetEnemyDetailLines(card);
                _detailIndex = -1;
                _detailCard = card;
            }

            if (_detailLines.Count == 0)
            {
                TolkWrapper.Speak("No details");
                return;
            }

            // Advance to next line (no wrap)
            if (_detailIndex >= _detailLines.Count - 1)
            {
                _detailIndex = _detailLines.Count - 1;
                TolkWrapper.Speak(_detailLines[_detailIndex]);
                return;
            }

            _detailIndex++;
            TolkWrapper.Speak(_detailLines[_detailIndex]);
        }

        /// <summary>
        /// Read previous detail line of current enemy item (Ctrl+Down).
        /// </summary>
        public void DetailPrevious()
        {
            if (!_active) return;

            var card = GetCurrentCard();
            if (card == null)
            {
                TolkWrapper.Speak("No item selected");
                return;
            }

            // Refresh detail lines if card changed - use enemy-focused order
            if (_detailCard != card)
            {
                _detailLines = ItemReader.GetEnemyDetailLines(card);
                _detailIndex = -1;
                _detailCard = card;
            }

            if (_detailLines.Count == 0)
            {
                TolkWrapper.Speak("No details");
                return;
            }

            // On first press or at start, read first line
            if (_detailIndex <= 0)
            {
                _detailIndex = 0;
                TolkWrapper.Speak(_detailLines[0]);
                return;
            }

            _detailIndex--;
            TolkWrapper.Speak(_detailLines[_detailIndex]);
        }

        /// <summary>
        /// Reads detailed description of current enemy item (Enter key).
        /// </summary>
        public void ReadCurrentItemDetails()
        {
            if (!_active) return;

            var card = GetCurrentCard();
            if (card == null)
            {
                TolkWrapper.Speak("No item selected");
                return;
            }

            TolkWrapper.Speak(ItemReader.GetDetailedDescription(card));
        }

        /// <summary>
        /// Gets the current enemy card based on subsection and index.
        /// </summary>
        public Card GetCurrentCard()
        {
            if (_subsection == EnemySubsection.Items)
            {
                if (_itemIndex < _cards.Count)
                    return _cards[_itemIndex];
            }
            else // Skills
            {
                // Try from sockets first
                var bm = GetBoardManager();
                if (_skillIndices.Count > 0 && bm != null)
                {
                    if (_skillIndex < _skillIndices.Count)
                    {
                        int idx = _skillIndices[_skillIndex];
                        var card = bm.opponentSkillSockets[idx]?.CardController?.CardData;
                        if (card != null) return card;
                    }
                }
                // Fallback to data
                if (_skillIndex < _skills.Count)
                {
                    return _skills[_skillIndex];
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the total count of enemy skills from either sockets or data.
        /// </summary>
        public int GetSkillCount()
        {
            // Return the max of both sources since we fall back between them
            return Math.Max(_skillIndices.Count, _skills.Count);
        }

        // Backward compatibility aliases
        public void Next() => NavigateRight();
        public void Previous() => NavigateLeft();

        /// <summary>
        /// Announces the current enemy skill with name and description.
        /// </summary>
        private void AnnounceSkill()
        {
            int skillCount = GetSkillCount();
            if (_skillIndex < 0 || _skillIndex >= skillCount)
            {
                TolkWrapper.Speak("No skill");
                return;
            }

            var card = GetCurrentCard();
            if (card == null)
            {
                TolkWrapper.Speak("Empty slot");
                return;
            }

            string name = ItemReader.GetCardName(card);
            string desc = ItemReader.GetFullDescription(card);

            if (!string.IsNullOrEmpty(desc))
            {
                TolkWrapper.Speak($"{name}: {desc}");
            }
            else
            {
                TolkWrapper.Speak(name);
            }
        }

        /// <summary>
        /// Announces the current enemy item with position.
        /// For skills, includes description like player's own skills.
        /// Uses VisualSelector for visual feedback.
        /// </summary>
        private void AnnounceCurrentItem()
        {
            CardController controller = GetCurrentCardController();
            Card card = controller?.CardData ?? GetCurrentCard(); // Fallback to data if no controller

            if (card == null)
            {
                TolkWrapper.Speak("Empty");
                return;
            }

            string name = ItemReader.GetCardName(card);

            // For skills, include description like player's own skills
            if (_subsection == EnemySubsection.Skills)
            {
                string desc = ItemReader.GetFullDescription(card);
                if (!string.IsNullOrEmpty(desc))
                {
                    TolkWrapper.Speak($"{name}: {desc}");
                }
                else
                {
                    TolkWrapper.Speak(name);
                }
            }
            else
            {
                // Use compact combat-focused description for enemy items
                TolkWrapper.Speak(ItemReader.GetEnemyCompactDescription(card));
            }

            // Visual feedback via VisualSelector
            if (controller != null)
            {
                VisualSelector.SelectSocket(controller);
            }
        }

        /// <summary>
        /// Clears the detail reading state for enemy items.
        /// </summary>
        private void ClearDetailReading()
        {
            _detailLines.Clear();
            _detailIndex = -1;
            _detailCard = null;
        }

        /// <summary>
        /// Checks if the replay animation is currently playing.
        /// </summary>
        private bool IsReplayAnimationActive()
        {
            try
            {
                var currentState = AppState.CurrentState;
                if (currentState == null) return false;

                var replayStateType = typeof(AppState).Assembly.GetType("TheBazaar.ReplayState");
                if (replayStateType == null || !replayStateType.IsInstanceOfType(currentState))
                    return false;

                // Check IsReplaying property
                var isReplayingProp = replayStateType.GetProperty("IsReplaying");
                if (isReplayingProp != null)
                {
                    return (bool)isReplayingProp.GetValue(currentState);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"EnemyNavigator.IsReplayAnimationActive error: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Gets the current enemy card controller based on subsection and index.
        /// Uses Data.CardAndSkillLookup for items (reliable), sockets for skills.
        /// </summary>
        private CardController GetCurrentCardController()
        {
            if (_subsection == EnemySubsection.Items)
            {
                // Use CardAndSkillLookup to find controller from Card object
                var card = GetCurrentCard();
                if (card != null)
                    return Data.CardAndSkillLookup.GetCardController(card);
            }
            else // Skills
            {
                var bm = GetBoardManager();
                if (bm != null && _skillIndex < _skillIndices.Count)
                {
                    int idx = _skillIndices[_skillIndex];
                    return bm.opponentSkillSockets[idx]?.CardController;
                }
            }
            return null;
        }

        private static BoardManager GetBoardManager()
        {
            try { return Singleton<BoardManager>.Instance; }
            catch { return null; }
        }
    }
}
