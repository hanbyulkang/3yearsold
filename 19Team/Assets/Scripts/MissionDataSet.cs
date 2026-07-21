using System;
using System.Collections.Generic;
using UnityEngine;

public enum MissionAction { None, Feed, Walk, CleanPoop, PetOrPlay, MiniGame }

[CreateAssetMenu(fileName = "MissionDataSet", menuName = "Game Data/Mission Data Set")]
public class MissionDataSet : ScriptableObject
{
    [Serializable]
    public class MissionState
    {
        public string id;
        public MissionAction action;
        [Min(1)] public int requiredCount = 1;
        [Min(0)] public int currentCount;
        public bool completed;
    }

    [SerializeField] private string _saveKey = "Mission";
    [SerializeField] private List<MissionState> _missions = new List<MissionState>();
    public IReadOnlyList<MissionState> Missions => _missions;

    public void EnsureCount(int count)
    {
        while (_missions.Count < count)
            _missions.Add(new MissionState { id = "Mission" + (_missions.Count + 1).ToString("00") });
        if (_missions.Count > count) _missions.RemoveRange(count, _missions.Count - count);
    }

    public bool GetCompleted(int index) => index >= 0 && index < _missions.Count && _missions[index].completed;

    public void SetCompleted(int index, bool completed)
    {
        EnsureCount(index + 1);
        _missions[index].completed = completed;
        PlayerPrefs.SetInt(Key(index), completed ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ConfigureMission(int index, string id, MissionAction action, int requiredCount)
    {
        EnsureCount(index + 1);
        MissionState mission = _missions[index];
        mission.id = id;
        mission.action = action;
        mission.requiredCount = Mathf.Max(1, requiredCount);
        mission.currentCount = Mathf.Clamp(mission.currentCount, 0, mission.requiredCount);
        mission.completed = mission.currentCount >= mission.requiredCount || mission.completed;
    }

    public bool AddProgress(MissionAction action, int amount = 1)
    {
        bool changed = false;
        foreach (MissionState mission in _missions)
        {
            if (mission.action != action || mission.completed)
                continue;
            mission.currentCount = Mathf.Clamp(mission.currentCount + Mathf.Max(0, amount), 0, mission.requiredCount);
            mission.completed = mission.currentCount >= mission.requiredCount;
            SaveMission(mission);
            changed = true;
        }
        if (changed) PlayerPrefs.Save();
        return changed;
    }

    public void LoadSavedState()
    {
        for (int i = 0; i < _missions.Count; i++)
        {
            MissionState mission = _missions[i];
            mission.currentCount = PlayerPrefs.GetInt(ProgressKey(mission), mission.currentCount);
            mission.completed = PlayerPrefs.GetInt(Key(i), mission.completed ? 1 : 0) == 1
                             || mission.currentCount >= mission.requiredCount;
        }
    }

    string Key(int index) => _saveKey + "." + _missions[index].id + ".Completed";
    string ProgressKey(MissionState mission) => _saveKey + "." + mission.id + ".Progress";
    void SaveMission(MissionState mission)
    {
        PlayerPrefs.SetInt(_saveKey + "." + mission.id + ".Progress", mission.currentCount);
        PlayerPrefs.SetInt(_saveKey + "." + mission.id + ".Completed", mission.completed ? 1 : 0);
    }
}
