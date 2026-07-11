using System.Collections.Generic;
using BazaarAccess.Core;
using BazaarGameClient.Domain.Models;
using BazaarGameClient.Domain.Models.Cards;
using BazaarGameShared.Domain.Core.Types;
using TheBazaar;

namespace BazaarAccess.Gameplay.Navigation;

/// <summary>
/// Handles hero stats and skills navigation, extracted from GameplayNavigator.
/// </summary>
public class HeroNavigator
{
    private const int ShieldStatIndex = 6;

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
            EnterSkillsSubsection();
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
            EnterSkillsSubsection();
        }
    }

    private void EnterSkillsSubsection()
    {
        if (_skills.Count == 0)
        {
            TolkWrapper.Speak("No skills equipped");
            return;
        }

        _subsection = HeroSubsection.Skills;

        if (_skillIndex < 0)
            _skillIndex = 0;
        else if (_skillIndex >= _skills.Count)
            _skillIndex = _skills.Count - 1;

        AnnounceSubsection();
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
            string skillAnnouncement = GetCurrentSkillAnnouncement();
            TolkWrapper.Speak($"Hero skills, {_skills.Count} skills. {skillAnnouncement}");
            OnSkillVisualSelect?.Invoke(_skillIndex);
        }
    }

    /// <summary>
    /// Returns the total number of hero stats including rank if in ranked mode.
    /// </summary>
    public int GetStatCount()
    {
        return GetStatCount(Data.Run?.Player, includeRank: ItemReader.IsRankedMode());
    }

    public int GetStatCount(Player player, bool includeRank)
    {
        return GetStatCountInternal(player, includeRank);
    }

    private int GetStatCountInternal(Player player, bool includeRank)
    {
        int count = HeroStats.Length;
        if (SupportsRage(player)) count++;
        if (includeRank) count++;
        return count;
    }

    public static bool SupportsRage(Player player)
    {
        return (player?.GetAttributeValue(EPlayerAttributeType.RageMax) ?? 0) > 0;
    }

    /// <summary>
    /// Announces the current hero skill with its description.
    /// </summary>
    public void AnnounceSkill()
    {
        TolkWrapper.Speak(GetCurrentSkillAnnouncement());
        // Trigger visual selection via callback
        OnSkillVisualSelect?.Invoke(_skillIndex);
    }

    private string GetCurrentSkillAnnouncement()
    {
        if (_skillIndex < 0 || _skillIndex >= _skills.Count)
            return "No skill";

        var skill = _skills[_skillIndex];
        if (skill == null)
            return "Empty slot";

        string name = ItemReader.GetCardName(skill);
        string desc = ItemReader.GetFullDescription(skill);
        return !string.IsNullOrEmpty(desc) ? $"{name}: {desc}" : name;
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

        if (SupportsRage(player))
        {
            int rageMax = player.GetAttributeValue(EPlayerAttributeType.RageMax) ?? 0;
            int rage = player.GetAttributeValue(EPlayerAttributeType.Rage) ?? 0;
            parts.Add($"Rage {rage} / {rageMax}");
        }

        var shield = player.GetAttributeValue(EPlayerAttributeType.Shield);
        if (shield.HasValue && shield.Value > 0) parts.Add($"Shield {shield.Value}");

        TolkWrapper.Speak(string.Join(", ", parts));
    }

    /// <summary>
    /// Announces the current hero stat value.
    /// </summary>
    public void AnnounceStat()
    {
        var player = Data.Run?.Player;
        AnnounceStat(
            player,
            _statIndex,
            includeRank: ItemReader.IsRankedMode(),
            rankText: ItemReader.GetPlayerRank());
    }

    public void AnnounceStat(Player player, int statIndex, bool includeRank, string rankText = null)
    {
        if (player == null) { TolkWrapper.Speak("No hero data"); return; }

        bool isRageStat;
        bool isRankStat;
        int baseStatIndex = GetBaseStatIndex(player, statIndex, includeRank, out isRageStat, out isRankStat);

        if (isRankStat)
        {
            TolkWrapper.Speak(!string.IsNullOrEmpty(rankText) ? $"Rank: {rankText}" : "Rank: unranked");
            return;
        }

        if (isRageStat)
        {
            int rage = player.GetAttributeValue(EPlayerAttributeType.Rage) ?? 0;
            int rageMax = player.GetAttributeValue(EPlayerAttributeType.RageMax) ?? 0;
            TolkWrapper.Speak($"Rage: {rage} / {rageMax}");
            return;
        }

        var type = HeroStats[baseStatIndex];
        var value = player.GetAttributeValue(type);
        string name = GetStatName(type);

        TolkWrapper.Speak(value.HasValue ? $"{name}: {value.Value}" : $"{name}: none");
    }

    private static int GetBaseStatIndex(Player player, int statIndex, bool includeRank, out bool isRageStat, out bool isRankStat)
    {
        isRageStat = false;
        isRankStat = false;

        bool shouldShowRage = SupportsRage(player);
        int rankIndex = HeroStats.Length + (shouldShowRage ? 1 : 0);

        if (shouldShowRage && statIndex == ShieldStatIndex)
        {
            isRageStat = true;
            return -1;
        }

        if (includeRank && statIndex == rankIndex)
        {
            isRankStat = true;
            return -1;
        }

        if (shouldShowRage && statIndex > ShieldStatIndex)
        {
            return statIndex - 1;
        }

        return statIndex;
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
