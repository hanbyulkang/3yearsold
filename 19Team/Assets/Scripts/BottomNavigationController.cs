using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class BottomNavigationController : MonoBehaviour
{
    [Serializable]
    public sealed class NavigationItem
    {
        public Button button;
        public Image image;
        public RectTransform rect;
    }

    [SerializeField] private NavigationItem[] _items = Array.Empty<NavigationItem>();
    [SerializeField] private Sprite _normalSprite;
    [Tooltip("Assign the dedicated selected navigation sprite here.")]
    [SerializeField] private Sprite _selectedSprite;
    [SerializeField] private Color _selectedFallbackColor = new Color(1f, 0.82f, 0f, 1f);
    [SerializeField] private float _selectedScale = 1.1f;
    [SerializeField] private int _defaultSelectedIndex;
    private int _selectedIndex = -1;

    private void Awake()
    {
        // Navigation selection visuals are intentionally disabled.
    }

    public void Select(int index)
    {
        // Kept as a no-op so existing Button events remain safe.
    }
}
