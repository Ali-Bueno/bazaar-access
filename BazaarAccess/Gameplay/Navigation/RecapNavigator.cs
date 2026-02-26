using System;
using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Handles all recap mode navigation (post-combat with E key):
/// hero stats/skills, enemy stats/skills, player board, and combat stats.
/// Extracted from GameplayNavigator to reduce file size.
/// </summary>
public class RecapNavigator
{
    private readonly HeroNavigator _hero;
    private readonly EnemyNavigator _enemy;
    private readonly Action _onEnterPlayerBoard;

    private RecapSection _currentSection = RecapSection.None;
    private int _enemyStatIndex = 0;
    private int _enemyHeroSkillIndex = 0;
    private List<string> _combatStatsLines = new List<string>();
    private int _combatStatsIndex = 0;

    public RecapSection CurrentSection => _currentSection;

    /// <summary>
    /// Creates a new RecapNavigator.
    /// </summary>
    /// <param name="hero">HeroNavigator for accessing hero stats/skills arrays and announcements.</param>
    /// <param name="enemy">EnemyNavigator for refreshing enemy items and accessing enemy skills.</param>
    /// <param name="onEnterPlayerBoard">Callback invoked when entering player board recap mode.
    /// The caller should set NavigationSection.Board, reset index, clear detail cache, refresh board,
    /// and announce the board state.</param>
    public RecapNavigator(HeroNavigator hero, EnemyNavigator enemy, Action onEnterPlayerBoard)
    {
        _hero = hero;
        _enemy = enemy;
        _onEnterPlayerBoard = onEnterPlayerBoard;
    }

    // ===============================================
    // HERO RECAP (V key in recap mode)
    // ===============================================

    /// <summary>
    /// Enter Hero stats mode in recap (V key).
    /// </summary>
    public void EnterHeroMode()
    {
        _currentSection = RecapSection.HeroStats;
        _hero.StatIndex = 0;

        int statCount = _hero.GetStatCount();
        int skillCount = _hero.Skills.Count;

        string msg = $"Your hero. Stats: {statCount}";
        if (skillCount > 0)
            msg += $", Skills: {skillCount}. Right arrow for skills.";

        // Include rank in recap hero announcement
        string rank = ItemReader.GetPlayerRank();
        if (!string.IsNullOrEmpty(rank) && ItemReader.IsRankedMode())
            msg += $" Rank: {rank}";

        TolkWrapper.Speak(msg);
    }

    /// <summary>
    /// Navigate to previous hero stat/skill in recap (Up key).
    /// </summary>
    public void HeroPrevious()
    {
        if (_currentSection == RecapSection.HeroStats)
        {
            if (_hero.StatIndex <= 0)
            {
                _hero.StatIndex = 0;
                _hero.AnnounceStat();
                return;
            }
            _hero.StatIndex--;
            _hero.AnnounceStat();
        }
        else if (_currentSection == RecapSection.HeroSkills)
        {
            if (_hero.SkillIndex <= 0)
            {
                _hero.SkillIndex = 0;
                _hero.AnnounceSkill();
                return;
            }
            _hero.SkillIndex--;
            _hero.AnnounceSkill();
        }
    }

    /// <summary>
    /// Navigate to next hero stat/skill in recap (Down key).
    /// </summary>
    public void HeroNext()
    {
        if (_currentSection == RecapSection.HeroStats)
        {
            int maxIndex = _hero.GetStatCount() - 1;
            if (_hero.StatIndex >= maxIndex)
            {
                _hero.AnnounceStat();
                return;
            }
            _hero.StatIndex++;
            _hero.AnnounceStat();
        }
        else if (_currentSection == RecapSection.HeroSkills)
        {
            if (_hero.SkillIndex >= _hero.Skills.Count - 1)
            {
                _hero.AnnounceSkill();
                return;
            }
            _hero.SkillIndex++;
            _hero.AnnounceSkill();
        }
    }

    /// <summary>
    /// Switch to hero stats in recap (Left key).
    /// </summary>
    public void HeroToStats()
    {
        if (_currentSection == RecapSection.HeroSkills)
        {
            _currentSection = RecapSection.HeroStats;
            _hero.StatIndex = 0;
            TolkWrapper.Speak($"Stats, {_hero.GetStatCount()}");
        }
    }

    /// <summary>
    /// Switch to hero skills in recap (Right key).
    /// </summary>
    public void HeroToSkills()
    {
        if (_currentSection == RecapSection.HeroStats)
        {
            if (_hero.Skills.Count == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            _currentSection = RecapSection.HeroSkills;
            _hero.SkillIndex = 0;
            TolkWrapper.Speak($"Skills, {_hero.Skills.Count}");
        }
    }

    // ===============================================
    // ENEMY STATS RECAP (F key in recap/combat mode)
    // ===============================================

    /// <summary>
    /// Enter Enemy stats mode in recap (F key).
    /// </summary>
    public void EnterEnemyStatsMode()
    {
        _currentSection = RecapSection.EnemyStats;
        _enemyStatIndex = 0;
        _enemyHeroSkillIndex = 0;

        _enemy.Refresh(); // Also loads enemy skills

        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy");
            _currentSection = RecapSection.None;
            return;
        }

        TolkWrapper.Speak("Enemy stats");
    }

    /// <summary>
    /// Enter Enemy stats mode during combat (F key).
    /// Allows navigation of enemy stats and skills with arrow keys.
    /// </summary>
    public void EnterCombatEnemyStatsMode()
    {
        _currentSection = RecapSection.EnemyStats;
        _enemyStatIndex = 0;
        _enemyHeroSkillIndex = 0;

        _enemy.Refresh(); // Also loads enemy skills

        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy");
            _currentSection = RecapSection.None;
            return;
        }

        TolkWrapper.Speak("Enemy stats");
    }

    /// <summary>
    /// Navigate to previous enemy stat/skill in recap (Up key).
    /// </summary>
    public void EnemyStatsPrevious()
    {
        if (_currentSection == RecapSection.EnemyStats)
        {
            if (_enemyStatIndex <= 0)
            {
                _enemyStatIndex = 0;
                AnnounceEnemyStat();
                return;
            }
            _enemyStatIndex--;
            AnnounceEnemyStat();
        }
        else if (_currentSection == RecapSection.EnemySkills)
        {
            if (_enemyHeroSkillIndex <= 0)
            {
                _enemyHeroSkillIndex = 0;
                AnnounceEnemyHeroSkill();
                return;
            }
            _enemyHeroSkillIndex--;
            AnnounceEnemyHeroSkill();
        }
    }

    /// <summary>
    /// Navigate to next enemy stat/skill in recap (Down key).
    /// </summary>
    public void EnemyStatsNext()
    {
        if (_currentSection == RecapSection.EnemyStats)
        {
            if (_enemyStatIndex >= HeroNavigator.HeroStats.Length - 1)
            {
                AnnounceEnemyStat();
                return;
            }
            _enemyStatIndex++;
            AnnounceEnemyStat();
        }
        else if (_currentSection == RecapSection.EnemySkills)
        {
            if (_enemyHeroSkillIndex >= _enemy.Skills.Count - 1)
            {
                AnnounceEnemyHeroSkill();
                return;
            }
            _enemyHeroSkillIndex++;
            AnnounceEnemyHeroSkill();
        }
    }

    /// <summary>
    /// Switch to enemy stats in recap (Left key).
    /// </summary>
    public void EnemyToStats()
    {
        if (_currentSection == RecapSection.EnemySkills)
        {
            _currentSection = RecapSection.EnemyStats;
            _enemyStatIndex = 0;
            TolkWrapper.Speak("Stats");
        }
    }

    /// <summary>
    /// Switch to enemy skills in recap (Right key).
    /// </summary>
    public void EnemyToSkills()
    {
        if (_currentSection == RecapSection.EnemyStats)
        {
            if (_enemy.Skills.Count == 0)
            {
                TolkWrapper.Speak("No skills");
                return;
            }
            _currentSection = RecapSection.EnemySkills;
            _enemyHeroSkillIndex = 0;
            TolkWrapper.Speak($"Skills, {_enemy.Skills.Count}");
        }
    }

    // ===============================================
    // PLAYER BOARD RECAP (B key in recap mode)
    // ===============================================

    /// <summary>
    /// Enter player board mode in recap (B key).
    /// Delegates to the parent navigator via callback since board state
    /// (NavigationSection, indices, detail cache) lives on GameplayNavigator.
    /// </summary>
    public void EnterPlayerBoardMode()
    {
        _currentSection = RecapSection.PlayerBoard;
        _onEnterPlayerBoard?.Invoke();
    }

    // ===============================================
    // COMBAT STATS RECAP (H key in recap mode)
    // ===============================================

    /// <summary>
    /// Enter combat stats mode in recap (H key).
    /// </summary>
    public void EnterCombatStatsMode()
    {
        _combatStatsLines = CombatDescriber.GetCombatStatsLines();
        _combatStatsIndex = 0;
        _currentSection = RecapSection.CombatStats;

        if (_combatStatsLines.Count > 0)
        {
            TolkWrapper.Speak(_combatStatsLines[0]);
        }
        else
        {
            TolkWrapper.Speak("No combat data");
        }
    }

    /// <summary>
    /// Navigate to previous combat stat line (Up key).
    /// </summary>
    public void CombatStatsPrevious()
    {
        if (_combatStatsLines.Count == 0) return;

        if (_combatStatsIndex <= 0)
        {
            _combatStatsIndex = 0;
            TolkWrapper.Speak(_combatStatsLines[0]);
            return;
        }

        _combatStatsIndex--;
        TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
    }

    /// <summary>
    /// Navigate to next combat stat line (Down key).
    /// </summary>
    public void CombatStatsNext()
    {
        if (_combatStatsLines.Count == 0) return;

        if (_combatStatsIndex >= _combatStatsLines.Count - 1)
        {
            TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
            return;
        }

        _combatStatsIndex++;
        TolkWrapper.Speak(_combatStatsLines[_combatStatsIndex]);
    }

    // ===============================================
    // UTILITIES
    // ===============================================

    /// <summary>
    /// Directly sets the current recap section.
    /// Used by GameplayNavigator when entering enemy board mode during recap.
    /// </summary>
    public void SetSection(RecapSection section)
    {
        _currentSection = section;
    }

    /// <summary>
    /// Resets all recap navigation state.
    /// </summary>
    public void Reset()
    {
        _currentSection = RecapSection.None;
        _enemyStatIndex = 0;
        _enemyHeroSkillIndex = 0;
        _combatStatsLines.Clear();
        _combatStatsIndex = 0;
    }

    /// <summary>
    /// Announce current enemy hero stat.
    /// Uses HeroNavigator.HeroStats array for stat types and GetStatName for display.
    /// </summary>
    private void AnnounceEnemyStat()
    {
        var opponent = Data.Run?.Opponent;
        if (opponent == null)
        {
            TolkWrapper.Speak("No enemy data");
            return;
        }

        var type = HeroNavigator.HeroStats[_enemyStatIndex];
        string name = _hero.GetStatName(type);

        if (opponent.Attributes.TryGetValue(type, out int value))
        {
            TolkWrapper.Speak($"{name}: {value}");
        }
        else
        {
            TolkWrapper.Speak($"{name}: none");
        }
    }

    /// <summary>
    /// Announce current enemy hero skill.
    /// Uses EnemyNavigator.Skills for skill data.
    /// </summary>
    private void AnnounceEnemyHeroSkill()
    {
        if (_enemyHeroSkillIndex < 0 || _enemyHeroSkillIndex >= _enemy.Skills.Count)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        var skill = _enemy.Skills[_enemyHeroSkillIndex];
        if (skill == null)
        {
            TolkWrapper.Speak("Empty slot");
            return;
        }

        string name = ItemReader.GetCardName(skill);
        string desc = ItemReader.GetFullDescription(skill);

        if (!string.IsNullOrEmpty(desc))
        {
            TolkWrapper.Speak($"{name}: {desc}");
        }
        else
        {
            TolkWrapper.Speak(name);
        }
    }
}
