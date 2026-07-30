using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

public sealed class GameUIController : MonoBehaviour
{
    [Serializable]
    public sealed class PlayEntry
    {
        public Button button;
        [Tooltip("씬 파일명. 첫 게임은 mini-game-1, 두 번째 게임은 minigame02입니다.")]
        public string sceneName;
        public UnityEvent onPlayAccepted;
    }

    [Header("Data")]
    [SerializeField] private GameCurrencyDataSet _dataSet;
    [Header("Top HUD")]
    [SerializeField] private TMP_Text _pawCountText;
    [SerializeField] private TMP_Text _boneCountText;
    [SerializeField] private TMP_Text _recoveryTimerText;
    [SerializeField] private Image[] _pawImages = new Image[5];
    [SerializeField] private Color _availablePawColor = Color.white;
    [SerializeField] private Color _emptyPawColor = Color.black;
    [Header("Game Entry")]
    [SerializeField] private PlayEntry[] _playEntries = Array.Empty<PlayEntry>();
    [SerializeField] private UnityEvent _onNotEnoughPaws;
    [Header("Bottom Game Tab")]
    [SerializeField] private Button _homeTabButton;
    [SerializeField] private Button _gameTabButton;
    [SerializeField] private Image _gameTabImage;
    [SerializeField] private RectTransform _gameTabRect;
    [SerializeField] private Sprite _gameNormalSprite;
    [SerializeField] private Sprite _gameSelectedSprite;
    [SerializeField] private Image _homeTabImage;
    [SerializeField] private RectTransform _homeTabRect;
    [SerializeField] private Sprite _homeNormalSprite;
    [SerializeField] private Button _inventoryTabButton;
    [SerializeField] private Image _inventoryTabImage;
    [SerializeField] private RectTransform _inventoryTabRect;
    [SerializeField] private Sprite _inventoryNormalSprite;
    [SerializeField] private float _selectedScale = 1.1f;
    [SerializeField] private float _tabAnimationDuration = 0.18f;
    [Header("Game View Layer")]
    [SerializeField] private GameObject _gameView;
    [SerializeField] private GameObject[] _dogFloatingUis = Array.Empty<GameObject>();

    private Coroutine _tabAnimation;
    private int _lastPaws = -1;
    private int _lastBones = -1;
    private int _lastSeconds = -1;
    private string _recoveryTextTemplate;
    private bool[] _dogFloatingUiStates;
    private bool _floatingStatesCaptured;

    private void Awake()
    {
        ResolveRuntimeInventoryTab();
        ResolveGameBoneCountText();
        _recoveryTextTemplate = _recoveryTimerText != null ? _recoveryTimerText.text : string.Empty;
        ResolveRuntimePlayButtons();
        for (int i = 0; i < _playEntries.Length; i++)
        {
            int captured = i;
            if (_playEntries[i]?.button != null) _playEntries[i].button.onClick.AddListener(() => TryPlay(captured));
        }
        if (_homeTabButton != null) _homeTabButton.onClick.AddListener(SelectHomeTab);
        if (_gameTabButton != null) _gameTabButton.onClick.AddListener(SelectGameTab);
        if (_inventoryTabButton != null) _inventoryTabButton.onClick.AddListener(SelectInventoryTab);
        GameCurrencyStore.Changed += RefreshImmediately;
        RefreshImmediately();
    }

    private void ResolveRuntimeInventoryTab()
    {
        if (_inventoryTabButton != null && _inventoryTabImage != null && _inventoryTabRect != null) return;
        foreach (Transform candidate in transform.GetComponentsInChildren<Transform>(true))
        {
            if (!candidate.name.Equals("Horizontal", StringComparison.OrdinalIgnoreCase)) continue;
            Transform inventory = candidate.Find("Inventory");
            if (inventory == null) continue;
            _inventoryTabButton = inventory.GetComponent<Button>();
            _inventoryTabImage = inventory.GetComponent<Image>();
            _inventoryTabRect = inventory as RectTransform;
            if (_inventoryNormalSprite == null && _inventoryTabImage != null)
                _inventoryNormalSprite = _inventoryTabImage.sprite;
            return;
        }
    }

    private void ResolveGameBoneCountText()
    {
        if (_gameView == null) return;
        foreach (Transform candidate in _gameView.GetComponentsInChildren<Transform>(true))
        {
            if (!candidate.name.Equals("Bone", StringComparison.OrdinalIgnoreCase)) continue;
            _boneCountText = candidate.GetComponentInChildren<TMP_Text>(true);
            if (_boneCountText != null) return;
        }
    }

    private void ResolveRuntimePlayButtons()
    {
        if (_playEntries != null && _playEntries.Length > 0 || _gameView == null) return;
        var entries = new List<PlayEntry>();
        foreach (Transform child in _gameView.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf("PlayBtn", StringComparison.OrdinalIgnoreCase) < 0) continue;
            Button button = child.GetComponent<Button>();
            if (button == null) button = child.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null) button.targetGraphic = child.GetComponent<Graphic>();
            button.interactable = true;
            entries.Add(new PlayEntry { button = button, sceneName = ResolveMiniGameScene(child) });
        }
        _playEntries = entries.ToArray();
    }

    private static string ResolveMiniGameScene(Transform playButton)
    {
        Transform card = playButton.parent;
        if (card != null)
            foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && text.text.IndexOf("2048", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "minigame02";
        return "mini-game-1";
    }

    private void OnDestroy()
    {
        GameCurrencyStore.Changed -= RefreshImmediately;
        for (int i = 0; i < _playEntries.Length; i++)
        {
            int captured = i;
            if (_playEntries[i]?.button != null) _playEntries[i].button.onClick.RemoveListener(() => TryPlay(captured));
        }
        if (_homeTabButton != null) _homeTabButton.onClick.RemoveListener(SelectHomeTab);
        if (_gameTabButton != null) _gameTabButton.onClick.RemoveListener(SelectGameTab);
        if (_inventoryTabButton != null) _inventoryTabButton.onClick.RemoveListener(SelectInventoryTab);
    }

    private void Update() => RefreshIfChanged();

    public void TryPlay(int index)
    {
        if (_dataSet == null || index < 0 || index >= _playEntries.Length) return;
        if (!_dataSet.TryEnterGame())
        {
            Debug.LogWarning("[Game UI] 발바닥이 부족해서 게임에 입장할 수 없습니다.", this);
            _onNotEnoughPaws?.Invoke();
            StartCoroutine(FlashEmptyPaws());
            return;
        }
        RefreshImmediately();
        _playEntries[index].onPlayAccepted?.Invoke();
        if (!string.IsNullOrWhiteSpace(_playEntries[index].sceneName))
            SceneManager.LoadScene(_playEntries[index].sceneName);
    }

    public void SelectGameTab()
    {
        ShowGameView();
        if (_homeTabImage != null && _homeNormalSprite != null) _homeTabImage.sprite = _homeNormalSprite;
        if (_homeTabRect != null) _homeTabRect.localScale = Vector3.one;
        ResetInventoryTab();
        if (_gameTabImage != null && _gameSelectedSprite != null) _gameTabImage.sprite = _gameSelectedSprite;
        AnimateTab(_gameTabRect);
    }

    public void SelectHomeTab()
    {
        HideGameView();
        if (_gameTabImage != null && _gameNormalSprite != null) _gameTabImage.sprite = _gameNormalSprite;
        if (_gameTabRect != null) _gameTabRect.localScale = Vector3.one;
        ResetInventoryTab();
        if (_homeTabImage != null && _gameSelectedSprite != null) _homeTabImage.sprite = _gameSelectedSprite;
        AnimateTab(_homeTabRect);
    }

    public void SelectInventoryTab()
    {
        HideGameView();
        if (_gameTabImage != null && _gameNormalSprite != null) _gameTabImage.sprite = _gameNormalSprite;
        if (_gameTabRect != null) _gameTabRect.localScale = Vector3.one;
        ResetInventoryTab();
        if (_homeTabImage != null && _gameSelectedSprite != null) _homeTabImage.sprite = _gameSelectedSprite;
        AnimateTab(_homeTabRect);
    }

    private void ResetInventoryTab()
    {
        Sprite normal = _inventoryNormalSprite != null ? _inventoryNormalSprite : _gameNormalSprite;
        if (_inventoryTabImage != null && normal != null) _inventoryTabImage.sprite = normal;
        if (_inventoryTabRect != null) _inventoryTabRect.localScale = Vector3.one;
    }

    private void AnimateTab(RectTransform target)
    {
        if (_tabAnimation != null) StopCoroutine(_tabAnimation);
        _tabAnimation = StartCoroutine(AnimateSelectedTab(target));
    }

    private IEnumerator AnimateSelectedTab(RectTransform target)
    {
        if (target == null) yield break;
        float elapsed = 0f;
        Vector3 start = Vector3.one;
        Vector3 overshoot = Vector3.one * (_selectedScale + 0.06f);
        Vector3 end = Vector3.one * _selectedScale;
        while (elapsed < _tabAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _tabAnimationDuration));
            target.localScale = t < 0.65f
                ? Vector3.LerpUnclamped(start, overshoot, 1f - Mathf.Pow(1f - t / 0.65f, 3f))
                : Vector3.LerpUnclamped(overshoot, end, (t - 0.65f) / 0.35f);
            yield return null;
        }
        target.localScale = end;
        _tabAnimation = null;
    }

    private void ShowGameView()
    {
        if (!_floatingStatesCaptured)
        {
            _dogFloatingUiStates = new bool[_dogFloatingUis.Length];
            for (int i = 0; i < _dogFloatingUis.Length; i++)
                _dogFloatingUiStates[i] = _dogFloatingUis[i] != null && _dogFloatingUis[i].activeSelf;
            _floatingStatesCaptured = true;
        }
        foreach (GameObject floatingUi in _dogFloatingUis)
            if (floatingUi != null) floatingUi.SetActive(false);
        if (_gameView != null)
            _gameView.SetActive(true);
    }

    private void HideGameView()
    {
        if (_gameView != null) _gameView.SetActive(false);
        if (_floatingStatesCaptured)
        {
            for (int i = 0; i < _dogFloatingUis.Length; i++)
                if (_dogFloatingUis[i] != null) _dogFloatingUis[i].SetActive(_dogFloatingUiStates[i]);
        }
        _floatingStatesCaptured = false;
    }

    private void RefreshIfChanged()
    {
        if (_dataSet == null) return;
        int paws = _dataSet.Paws;
        int bones = GameCurrencyStore.GetBones();
        int seconds = _dataSet.SecondsUntilNextPaw;
        if (paws == _lastPaws && bones == _lastBones && seconds == _lastSeconds) return;
        Refresh(paws, bones, seconds);
    }

    private void RefreshImmediately()
    {
        if (_dataSet == null) return;
        Refresh(_dataSet.Paws, GameCurrencyStore.GetBones(), _dataSet.SecondsUntilNextPaw);
    }

    private void Refresh(int paws, int bones, int seconds)
    {
        _lastPaws = paws; _lastBones = bones; _lastSeconds = seconds;
        if (_pawCountText != null) _pawCountText.text = paws + "/" + GameCurrencyStore.MaxPaws;
        if (_boneCountText != null) _boneCountText.text = bones.ToString();
        if (_recoveryTimerText != null)
        {
            _recoveryTimerText.gameObject.SetActive(true);
            _recoveryTimerText.text = FormatRecoveryText(seconds);
        }
        for (int i = 0; i < _pawImages.Length; i++)
            if (_pawImages[i] != null) _pawImages[i].color = i < paws ? _availablePawColor : _emptyPawColor;
    }

    private string FormatRecoveryText(int seconds)
    {
        string clock = $"{seconds / 60}:{seconds % 60:00}";
        if (string.IsNullOrEmpty(_recoveryTextTemplate)) return clock;
        if (Regex.IsMatch(_recoveryTextTemplate, @"\d{1,2}:\d{2}"))
            return new Regex(@"\d{1,2}:\d{2}").Replace(_recoveryTextTemplate, clock, 1);
        return _recoveryTextTemplate + " " + clock;
    }

    private IEnumerator FlashEmptyPaws()
    {
        for (int repeat = 0; repeat < 2; repeat++)
        {
            foreach (Image image in _pawImages) if (image != null) image.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            yield return new WaitForSecondsRealtime(0.1f);
            RefreshImmediately();
            yield return new WaitForSecondsRealtime(0.1f);
        }
    }
}
