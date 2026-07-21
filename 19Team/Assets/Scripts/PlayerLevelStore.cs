using System;
using UnityEngine;

public static class PlayerLevelStore
{
    public const int StartingLevel = 1;
    public const int ExperiencePerLevel = 100;
    private const string ExperienceKey = "player_level_total_experience_v2";

    public static event Action Changed;
    public static int TotalExperience => Mathf.Max(0, PlayerPrefs.GetInt(ExperienceKey, 0));
    public static int Level => StartingLevel + TotalExperience / ExperiencePerLevel;
    public static int CurrentExperience => TotalExperience % ExperiencePerLevel;

    public static void AddExperience(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(ExperienceKey, TotalExperience + amount);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
