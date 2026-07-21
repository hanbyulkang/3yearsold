using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class DetailTabController : MonoBehaviour
{
    [SerializeField] private Button _clothsButton;
    [SerializeField] private Button _yardButton;
    [SerializeField] private GameObject _clothsUIs;
    [SerializeField] private GameObject _yardUIs;
    [Header("Tab Visuals")]
    [SerializeField] private Image _clothsImage;
    [SerializeField] private Image _yardImage;
    [SerializeField] private TMP_Text _clothsText;
    [SerializeField] private TMP_Text _yardText;
    [SerializeField] private Sprite _selectedSprite;
    [SerializeField] private Sprite _unselectedSprite;
    [SerializeField] private Color _selectedBackground = new Color(1f, 0.82f, 0f, 1f);
    [SerializeField] private Color _unselectedBackground = Color.black;
    [SerializeField] private bool _showClothsOnOpen = true;

    private void Awake()
    {
        ResolveVisualReferences();
        if (_clothsButton != null) _clothsButton.onClick.AddListener(ShowCloths);
        if (_yardButton != null) _yardButton.onClick.AddListener(ShowYard);
        if (_showClothsOnOpen) ShowCloths(); else ShowYard();
    }

    private void ResolveVisualReferences()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Cloths")
            {
                _clothsImage ??= child.GetComponent<Image>();
                _clothsText ??= child.GetComponentInChildren<TMP_Text>(true);
            }
            else if (child.name == "Yard")
            {
                _yardImage ??= child.GetComponent<Image>();
                _yardText ??= child.GetComponentInChildren<TMP_Text>(true);
            }
        }
        _selectedSprite ??= _clothsImage != null ? _clothsImage.sprite : null;
        _unselectedSprite ??= _yardImage != null ? _yardImage.sprite : null;
    }

    private void OnDestroy()
    {
        if (_clothsButton != null) _clothsButton.onClick.RemoveListener(ShowCloths);
        if (_yardButton != null) _yardButton.onClick.RemoveListener(ShowYard);
    }

    public void ShowCloths()
    {
        if (_clothsUIs != null) _clothsUIs.SetActive(true);
        if (_yardUIs != null) _yardUIs.SetActive(false);
        SetTabVisual(true);
    }

    public void ShowYard()
    {
        if (_clothsUIs != null) _clothsUIs.SetActive(false);
        if (_yardUIs != null) _yardUIs.SetActive(true);
        SetTabVisual(false);
    }

    private void SetTabVisual(bool clothsSelected)
    {
        ApplyVisual(_clothsImage, _clothsText, clothsSelected);
        ApplyVisual(_yardImage, _yardText, !clothsSelected);
    }

    private void ApplyVisual(Image image, TMP_Text text, bool selected)
    {
        if (image != null)
        {
            Sprite sprite = selected ? _selectedSprite : _unselectedSprite;
            if (sprite != null) { image.sprite = sprite; image.color = Color.white; }
            else image.color = selected ? _selectedBackground : _unselectedBackground;
        }
        if (text != null) text.color = selected ? Color.black : Color.white;
    }
}
