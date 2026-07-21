using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DecorationPlacementDataSet", menuName = "Game Data/Decoration Placement Data Set")]
public sealed class DecorationPlacementDataSet : ScriptableObject
{
    [Serializable]
    public sealed class ItemState
    {
        public string id;
        public bool applied;
        public Vector3 position;
        public Vector3 eulerAngles;
    }

    [SerializeField] private string _saveKey = "Decoration";
    [SerializeField] private List<ItemState> _items = new List<ItemState>();
    public IReadOnlyList<ItemState> Items => _items;

    public void ConfigureItem(int index, string id)
    {
        while (_items.Count <= index) _items.Add(new ItemState());
        _items[index].id = id;
    }

    public void Load()
    {
        foreach (ItemState item in _items)
        {
            // A missing save key must always mean "not placed". Using the
            // serialized asset value here resurrected old editor placements
            // immediately after PlayerPrefs had been cleared.
            item.applied = PlayerPrefs.GetInt(Key(item, "Applied"), 0) == 1;
            if (!item.applied) continue;
            item.position = new Vector3(
                PlayerPrefs.GetFloat(Key(item, "X"), item.position.x),
                PlayerPrefs.GetFloat(Key(item, "Y"), item.position.y),
                PlayerPrefs.GetFloat(Key(item, "Z"), item.position.z));
            item.eulerAngles = new Vector3(0f, PlayerPrefs.GetFloat(Key(item, "RY"), item.eulerAngles.y), 0f);
        }
    }

    public bool IsApplied(int index) => index >= 0 && index < _items.Count && _items[index].applied;

    public void SetApplied(int index, Vector3 position, Quaternion rotation)
    {
        if (index < 0 || index >= _items.Count) return;
        ItemState item = _items[index];
        item.applied = true;
        item.position = position;
        item.eulerAngles = rotation.eulerAngles;
        PlayerPrefs.SetInt(Key(item, "Applied"), 1);
        PlayerPrefs.SetFloat(Key(item, "X"), position.x);
        PlayerPrefs.SetFloat(Key(item, "Y"), position.y);
        PlayerPrefs.SetFloat(Key(item, "Z"), position.z);
        PlayerPrefs.SetFloat(Key(item, "RY"), item.eulerAngles.y);
        PlayerPrefs.Save();
    }

    public void ClearSavedState()
    {
        foreach (ItemState item in _items)
        {
            PlayerPrefs.DeleteKey(Key(item, "Applied"));
            PlayerPrefs.DeleteKey(Key(item, "X"));
            PlayerPrefs.DeleteKey(Key(item, "Y"));
            PlayerPrefs.DeleteKey(Key(item, "Z"));
            PlayerPrefs.DeleteKey(Key(item, "RY"));
            item.applied = false;
            item.position = Vector3.zero;
            item.eulerAngles = Vector3.zero;
        }
    }

    private string Key(ItemState item, string suffix) => _saveKey + "." + item.id + "." + suffix;
}
