using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Handles hero stats and skills navigation, extracted from GameplayNavigator.
/// </summary>
public class HeroNavigator
{
    public static readonly EPlayerAttributeType[] HeroStats = new[]
    {
        EPlayerAttributeType.Health,
        EPlayerAttributeType.HealthMax,
        EPlayerAttributeType.Gold,
        EPlayerAttributeType.Level,
        EPlayerAttributeType.Experience,
        EPlayerAttributeType.Prestige,
        EPlayerAttributeType.Shield,
        EPlayerAttributeType.Poison,
        EPlayerAttributeType.Burn,
        EPlayerAttributeType.HealthRegen,
        EPlayerAttributeType.CritChance,
        EPlayerAttributeType.Income
    };

    private int _statIndex = 0;
    private HeroSubsection _subsection = HeroSubsection.Stats;
    private int _skillIndex = 0;
    private List<SkillCard> _skills = new List<SkillCard>();

    /// <summary>
    /// Callback for visual selection of hero skills (set by GameplayNavigator).
    /// </summary>
    public System.Action<int> OnSkillVisualSelect { get; set; }

    public HeroSubsection CurrentSubsection => _subsection;
    public int StatIndex { get => _statIndex; set => _statIndex = value; }
    public int SkillIndex { get => _skillIndex; set => _skillIndex = value; }
    public List<SkillCard> Skills => _skills;

    /// <summary>
    /// Refreshes the list of player skills from game data.
    /// </summary>
    public void Refresh()
    {
        _skills.Clear();
        try
        {
            var skills = Data.Run?.Player?.Skills;
            if (skills != null)
            {
                _skills.AddRange(skills);
                Plugin.Logger.LogInfo($"HeroNavigator.Refresh: Found {_skills.Count} skills from Player.Skills");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"HeroNavigator.Refresh error: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches to the next hero subsection (Stats -> Skills -> Stats).
    /// </summary>
    public void NextSubsection()
    {
        if (_subsection == HeroSubsection.Stats)
        {
            if (_skills.Count > 0)
            {
                _subsection = HeroSubsection.Skills;
                _skillIndex = 0;
                AnnounceSubsection();
            }
            else
            {
                TolkWrapper.Speak("No skills equipped");
            }
        }
        else
        {
            _subsection = HeroSubsection.Stats;
            _statIndex = 0;
            AnnounceSubsection();
        }
    }

    /// <summary>
    /// Switches to the previous hero subsection.
    /// </summary>
    public void PreviousSubsection()
    {
        if (_subsection == HeroSubsection.Skills)
        {
            _subsection = HeroSubsection.Stats;
            _statIndex = 0;
            AnnounceSubsection();
        }
        else
        {
            if (_skills.Count > 0)
            {
                _subsection = HeroSubsection.Skills;
                _skillIndex = 0;
                AnnounceSubsection();
            }
            else
            {
                TolkWrapper.Speak("No skills equipped");
            }
        }
    }

    /// <summary>
    /// Announces the current hero subsection name and count.
    /// </summary>
    public void AnnounceSubsection()
    {
        if (_subsection == HeroSubsection.Stats)
        {
            int count = GetStatCount();
            string msg = $"Hero stats, {count} stats";
            string rank = ItemReader.GetPlayerRank();
            if (!string.IsNullOrEmpty(rank) && ItemReader.IsRankedMode())
                msg += $". Rank: {rank}";
            TolkWrapper.Speak(msg);
        }
        else
        {
            TolkWrapper.Speak($"Hero skills, {_skills.Count} skills");
        }
    }

    /// <summary>
    /// Returns the total number of hero stats including rank if in ranked mode.
    /// </summary>
    public int GetStatCount()
    {
        int count = HeroStats.Length;
        if (ItemReader.IsRankedMode()) count++;
        return count;
    }

    /// <summary>
    /// Announces the current hero skill with its description.
    /// </summary>
    public void AnnounceSkill()
    {
        if (_skillIndex < 0 || _skillIndex >= _skills.Count)
        {
            TolkWrapper.Speak("No skill");
            return;
        }

        var skill = _skills[_skillIndex];
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

        // Trigger visual selection via callback
        OnSkillVisualSelect?.Invoke(_skillIndex);
    }

    /// <summary>
    /// Reads detailed description of the current hero skill.
    /// </summary>
    public void ReadSkillDetails()
    {
        if (_subsection != HeroSubsection.Skills) return;
        if (_skillIndex < 0 || _skillIndex >= _skills.Count) return;

        var skill = _skills[_skillIndex];
        if (skill == null) return;

        TolkWrapper.Speak(ItemReader.GetDetailedDescription(skill));
    }

    /// <summary>
    /// Navigates to the next stat or skill in the current subsection.
    /// </summary>
    public void Next()
    {
        if (_subsection == HeroSubsection.Stats)
        {
            int maxIndex = GetStatCount() - 1;
            if (_statIndex >= maxIndex)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            _statIndex++;
            AnnounceStat();
        }
        else
        {
            if (_skills.Count == 0) return;
            if (_skillIndex >= _skills.Count - 1)
            {
                TolkWrapper.Speak("End of list");
                return;
            }
            _skillIndex++;
            AnnounceSkill();
        }
    }

    /// <summary>
    /// Navigates to the previous stat or skill in the current subsection.
    /// </summary>
    public void Previous()
    {
        if (_subsection == HeroSubsection.Stats)
        {
            if (_statIndex <= 0)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            _statIndex--;
            AnnounceStat();
        }
        else
        {
            if (_skills.Count == 0) return;
            if (_skillIndex <= 0)
            {
                TolkWrapper.Speak("Start of list");
                return;
            }
            _skillIndex--;
            AnnounceSkill();
        }
    }

    /// <summary>
    /// Gets the current hero skill card for detail reading.
    /// </summary>
    public Card GetCurrentSkill()
    {
        if (_subsection != HeroSubsection.Skills) return null;
        if (_skillIndex < 0 || _skillIndex >= _skills.Count) return null;

        return _skills[_skillIndex];
    }

    /// <summary>
    /// Reads all hero stats as a summary announcement.
    /// </summary>
    public void ReadAllStats()
    {
        var player = Data.Run?.Player;
        if (player == null) { TolkWrapper.Speak("No hero data"); return; }

        var parts = new List<string>();

        var health = player.GetAttributeValue(EPlayerAttributeType.Health);
        var maxHealth = player.GetAttributeValue(EPlayerAttributeType.HealthMax);
        if (health.HasValue && maxHealth.HasValue)
            parts.Add($"Health {health.Value} of {maxHealth.Value}");

        var gold = player.GetAttributeValue(EPlayerAttributeType.Gold);
        if (gold.HasValue) parts.Add($"Gold {gold.Value}");

        var level = player.GetAttributeValue(EPlayerAttributeType.Level);
        if (level.HasValue) parts.Add($"Level {level.Value}");

        var shield = player.GetAttributeValue(EPlayerAttributeType.Shield);
        if (shield.HasValue && shield.Value > 0) parts.Add($"Shield {shield.Value}");

        TolkWrapper.Speak(string.Join(", ", parts));
    }

    /// <summary>
    /// Announces the current hero stat value.
    /// </summary>
    public void AnnounceStat()
    {
        // Check if this is the rank slot (last slot in ranked mode)
        if (ItemReader.IsRankedMode() && _statIndex >= HeroStats.Length)
        {
            string rank = ItemReader.GetPlayerRank();
            TolkWrapper.Speak(!string.IsNullOrEmpty(rank) ? $"Rank: {rank}" : "Rank: unranked");
            return;
        }

        var player = Data.Run?.Player;
        if (player == null) { TolkWrapper.Speak("No hero data"); return; }

        var type = HeroStats[_statIndex];
        var value = player.GetAttributeValue(type);
        string name = GetStatName(type);

        TolkWrapper.Speak(value.HasValue ? $"{name}: {value.Value}" : $"{name}: none");
    }

    /// <summary>
    /// Gets a human-readable name for a player attribute type.
    /// </summary>
    public string GetStatName(EPlayerAttributeType type) => type switch
    {
        EPlayerAttributeType.Health => "Health",
        EPlayerAttributeType.HealthMax => "Max Health",
        EPlayerAttributeType.Gold => "Gold",
        EPlayerAttributeType.Level => "Level",
        EPlayerAttributeType.Experience => "Experience",
        EPlayerAttributeType.Prestige => "Prestige",
        EPlayerAttributeType.Shield => "Shield",
        EPlayerAttributeType.Poison => "Poison",
        EPlayerAttributeType.Burn => "Burn",
        EPlayerAttributeType.HealthRegen => "Regeneration",
        EPlayerAttributeType.CritChance => "Crit Chance",
        EPlayerAttributeType.Income => "Income",
        _ => type.ToString()
    };

    /// <summary>
    /// Resets all navigation state to initial values.
    /// </summary>
    public void Reset()
    {
        _statIndex = 0;
        _subsection = HeroSubsection.Stats;
        _skillIndex = 0;
    }
}
