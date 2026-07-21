using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MissionUIController : MonoBehaviour
{
    [Serializable]
    public class MissionItem
    {
        public string id;
        [Tooltip("Quest image whose sprite changes when this mission completes.")]
        public Image questImage;
        public Sprite incompleteQuestSprite;
        public Sprite completedQuestSprite;
        public GameObject confirmIncomplete;
        public GameObject confirmComplete;
        public GameObject confirmDeco;
        public GameObject go;
        public Button goButton;
    }

    [SerializeField] private MissionDataSet _dataSet;
    [SerializeField] private MissionItem[] _missions = new MissionItem[5];
    [Header("Completed Count")]
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private string _countFormat = "{0}/{1}";
    [Header("Events")]
    [SerializeField] private UnityEvent<int, bool> _onMissionChanged;

    public MissionDataSet DataSet => _dataSet;

    private void Awake()
    {
        if (_dataSet == null) { Debug.LogError("[Mission] Assign MissionDataSet.", this); return; }
        _dataSet.EnsureCount(_missions.Length);
        _dataSet.LoadSavedState();
        for (int i = 0; i < _missions.Length; i++)
        {
            int index = i;
            if (_missions[i].goButton != null)
                _missions[i].goButton.onClick.AddListener(() => OnGoClicked(index));
        }
        RefreshAll();
    }

    public void SetMissionCompleted(int index, bool completed)
    {
        if (_dataSet == null || index < 0 || index >= _missions.Length) return;
        _dataSet.SetCompleted(index, completed);
        RefreshItem(index);
        RefreshCount();
        _onMissionChanged?.Invoke(index, completed);
    }

    public void CompleteMission(int index) => SetMissionCompleted(index, true);
    public void RegisterAction(MissionAction action, int amount = 1)
    {
        if (_dataSet != null && _dataSet.AddProgress(action, amount))
            RefreshAll();
    }
    public void RegisterFeed() => RegisterAction(MissionAction.Feed);
    public void RegisterWalk() => RegisterAction(MissionAction.Walk);
    public void RegisterCleanPoop() => RegisterAction(MissionAction.CleanPoop);
    public void RegisterPetOrPlay() => RegisterAction(MissionAction.PetOrPlay);
    public void RegisterMiniGame() => RegisterAction(MissionAction.MiniGame);
    public bool IsMissionCompleted(int index) => _dataSet != null && _dataSet.GetCompleted(index);
    public void RefreshAll()
    {
        for (int i = 0; i < _missions.Length; i++) RefreshItem(i);
        RefreshCount();
    }

    private void RefreshItem(int index)
    {
        MissionItem item = _missions[index];
        bool completed = _dataSet.GetCompleted(index);
        if (item.confirmIncomplete != null) item.confirmIncomplete.SetActive(!completed);
        if (item.confirmComplete != null) item.confirmComplete.SetActive(completed);
        if (item.confirmDeco != null) item.confirmDeco.SetActive(completed);
        if (item.go != null) item.go.SetActive(!completed);
        if (item.questImage != null)
        {
            Sprite target = completed ? item.completedQuestSprite : item.incompleteQuestSprite;
            if (target != null) item.questImage.sprite = target;
        }
    }

    private void RefreshCount()
    {
        int completed = 0;
        for (int i = 0; i < _missions.Length; i++) if (_dataSet.GetCompleted(i)) completed++;
        if (_countText != null) _countText.text = string.Format(_countFormat, completed, _missions.Length);
    }

    private void OnGoClicked(int index)
    {
        // Navigation can be connected through OnClick; completion happens only via CompleteMission.
    }
}
