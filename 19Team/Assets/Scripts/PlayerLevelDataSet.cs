using UnityEngine;

[CreateAssetMenu(fileName = "PlayerLevelDataSet", menuName = "Game Data/Player Level Data Set")]
public sealed class PlayerLevelDataSet : ScriptableObject
{
    [SerializeField] private int _startingLevel = 1;
    [SerializeField] private int _experiencePerLevel = PlayerLevelStore.ExperiencePerLevel;
    [SerializeField] private int _dogInteractionExperience = 10;
    [SerializeField] private int _miniGameExperience = 30;

    public int Level => PlayerLevelStore.Level;
    public int CurrentExperience => PlayerLevelStore.CurrentExperience;
    public int ExperiencePerLevel => _experiencePerLevel;
}
