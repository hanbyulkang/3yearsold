using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PlayerLevelUIController : MonoBehaviour
{
    [SerializeField] private PlayerLevelDataSet _dataSet;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private Slider _experienceSlider;
    [SerializeField] private Image _experienceFill;

    private void Awake()
    {
        PlayerLevelStore.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy() => PlayerLevelStore.Changed -= Refresh;

    public void Refresh()
    {
        int level = _dataSet != null ? _dataSet.Level : PlayerLevelStore.Level;
        int currentExperience = _dataSet != null ? _dataSet.CurrentExperience : PlayerLevelStore.CurrentExperience;
        int experiencePerLevel = _dataSet != null ? _dataSet.ExperiencePerLevel : PlayerLevelStore.ExperiencePerLevel;

        if (_levelText != null) _levelText.text = "Lv." + level;
        if (_experienceSlider != null)
        {
            _experienceSlider.minValue = 0f;
            _experienceSlider.maxValue = experiencePerLevel;
            _experienceSlider.wholeNumbers = true;
            _experienceSlider.SetValueWithoutNotify(currentExperience);
        }
        if (_experienceFill != null)
            _experienceFill.fillAmount = currentExperience / Mathf.Max(1f, experiencePerLevel);
    }

    public void Bind(TMP_Text levelText, Slider experienceSlider, Image experienceFill)
    {
        _levelText = levelText;
        _experienceSlider = experienceSlider;
        _experienceFill = experienceFill;
        Refresh();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneBinding()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.name.Equals("Suntail Village", StringComparison.OrdinalIgnoreCase)) return;

        Transform top = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            top = FindDescendantExact(root.transform, "TOP");
            if (top != null) break;
        }
        Transform profile = FindDirect(top, "Profile");
        if (profile == null) return;

        TMP_Text levelText = null;
        foreach (TMP_Text text in profile.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!text.text.TrimStart().StartsWith("Lv", StringComparison.OrdinalIgnoreCase)) continue;
            levelText = text;
            break;
        }

        Transform sliderRoot = FindDescendantExact(profile, "Slider");
        Slider slider = sliderRoot != null ? sliderRoot.GetComponent<Slider>() : null;
        Image fill = null;
        if (sliderRoot != null)
        {
            foreach (Image image in sliderRoot.GetComponentsInChildren<Image>(true))
            {
                if (image.transform == sliderRoot) continue;
                fill = image;
                break;
            }
        }

        if (levelText == null || (slider == null && fill == null)) return;
        PlayerLevelUIController controller = profile.GetComponent<PlayerLevelUIController>();
        if (controller == null) controller = profile.gameObject.AddComponent<PlayerLevelUIController>();
        controller.Bind(levelText, slider, fill);
    }

    private static Transform FindDirect(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return parent.GetChild(i);
        return null;
    }

    private static Transform FindDescendantExact(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDescendantExact(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
