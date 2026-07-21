using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>Randomly presents the DogHeart UI above the dog and handles clicks.</summary>
[RequireComponent(typeof(Canvas))]
public class DogHeartInteraction : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform _dogHeart;
    [SerializeField] private Button _dogHeartButton;
    [SerializeField] private Animator _dogAnimator;
    [SerializeField] private MissionUIController _missionController;

    [Header("Timing")]
    [Tooltip("Random minimum/maximum delay before the heart appears.")]
    [SerializeField] private Vector2 _appearInterval = new Vector2(2f, 5f);
    [SerializeField] private float _visibleDuration = 4f;

    [Header("Position")]
    [Tooltip("World-space height above the Dog root position.")]
    [SerializeField] private float _headOffset = 1.2f;

    [Header("Appearance")]
    [SerializeField] private float _popDuration = 0.45f;

    [Header("Bone Reward (assign in Inspector)")]
    [Tooltip("Image that already displays the bone sprite from your dataset.")]
    [SerializeField] private Image _boneIconSource;
    [Tooltip("The Bone UI RectTransform that the spawned icons fly into.")]
    [SerializeField] private RectTransform _boneTarget;
    [Tooltip("Optional full-canvas RectTransform for reward icons. Uses this Canvas when empty.")]
    [SerializeField] private RectTransform _rewardLayer;
    [SerializeField] private Vector2Int _boneRewardCount = new Vector2Int(5, 6);
    [SerializeField] private float _boneFlyDuration = 0.75f;
    [SerializeField] private float _boneScatterRadius = 70f;
    [SerializeField] private Vector2 _boneIconSize = new Vector2(54f, 54f);
    [Tooltip("Text displaying the current bone amount.")]
    [SerializeField] private TMP_Text _boneCountText;
    [SerializeField] private int _boneCount;
    [Tooltip("Connect your dataset update method here. The new total bone count is passed as an int.")]
    [SerializeField] private UnityEvent<int> _onBoneCountChanged;

    [Header("Jerky / Meat Data")]
    [Tooltip("Text displaying the current jerky amount.")]
    [SerializeField] private TMP_Text _meatCountText;
    [SerializeField] private int _meatCount;
    [Tooltip("Connect your dataset meat-count setter here.")]
    [SerializeField] private UnityEvent<int> _onMeatCountChanged;

    [Header("Game Hearts")]
    [Tooltip("Assign the five heart UI objects in display order.")]
    [SerializeField] private GameObject[] _gameHearts = new GameObject[5];
    [Range(0, 5)] [SerializeField] private int _heartCount = 5;
    [Tooltip("Connect your dataset heart-count setter here.")]
    [SerializeField] private UnityEvent<int> _onHeartCountChanged;

    private RectTransform _rect;
    private CanvasGroup _group;
    private Canvas _canvas;
    private DogWanderAI _dogAI;
    private Transform _dogRoot;
    private ParticleSystem[] _dogParticles = Array.Empty<ParticleSystem>();
    private Camera _camera;
    private Vector3 _headPosition;
    private float _timer;
    private float _shownAt;
    private bool _visible;

    public void Initialize(Animator dogAnimator)
    {
        if (!IsActualDogAnimator(dogAnimator))
            dogAnimator = ResolveDogAnimator();

        _dogAnimator = dogAnimator;
        _dogAI = dogAnimator != null ? dogAnimator.GetComponent<DogWanderAI>() : null;
        _dogRoot = dogAnimator != null ? FindDogRoot(dogAnimator.transform) : null;
        _dogParticles = dogAnimator != null
            ? _dogRoot.GetComponentsInChildren<ParticleSystem>(true)
            : Array.Empty<ParticleSystem>();
        EnsureRaycastTarget();
        UpdateHeadPosition();
    }

    private bool IsActualDogAnimator(Animator candidate)
    {
        if (candidate == null || candidate.transform is RectTransform)
            return false;
        if (_dogHeart != null && (candidate.transform == _dogHeart || candidate.transform.IsChildOf(_dogHeart)))
            return false;
        RuntimeAnimatorController controller = candidate.runtimeAnimatorController;
        return controller != null && controller.name.IndexOf("DogAnimator", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Animator ResolveDogAnimator()
    {
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Animator candidate in animators)
            if (IsActualDogAnimator(candidate))
                return candidate;

        Debug.LogError("[DogHeart] A real Dog Animator using DogAnimatorController was not found.", this);
        return null;
    }

    private void Awake()
    {
        _rect = _dogHeart;
        _canvas = GetComponent<Canvas>();
        if (_rect == null)
        {
            Debug.LogError("[DogHeart] Assign the Dog Heart RectTransform on the Canvas component.", this);
            enabled = false;
            return;
        }

        _rect.gameObject.SetActive(true);
        if (_dogHeartButton == null)
        {
            _dogHeartButton = _rect.GetComponent<Button>();
            if (_dogHeartButton == null)
                _dogHeartButton = _rect.gameObject.AddComponent<Button>();
        }

        _group = _rect.GetComponent<CanvasGroup>();
        if (_group == null)
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
        _camera = Camera.main;
        _dogHeartButton.onClick.AddListener(OnHeartClicked);
        Initialize(_dogAnimator);
        RefreshBoneText();
        RefreshMeatText();
        RefreshHearts();
        HideImmediately();
        ScheduleNext();
    }

    private void OnDestroy()
    {
        if (_dogHeartButton != null)
            _dogHeartButton.onClick.RemoveListener(OnHeartClicked);
    }

    private void Update()
    {
        if (_dogAnimator == null || _canvas == null)
            return;

        UpdateHeadPosition();
        FollowDog();

        if (!_visible)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                Show();
            return;
        }

        float age = Time.time - _shownAt;
        float pop = Mathf.Clamp01(age / Mathf.Max(0.01f, _popDuration));
        float eased = 1f + 1.7f * Mathf.Pow(pop - 1f, 3f) + 0.7f * Mathf.Pow(pop - 1f, 2f);
        _rect.localScale = Vector3.one * eased;

        float remaining = _visibleDuration - age;
        _group.alpha = Mathf.Clamp01(Mathf.Min(pop * 3f, remaining * 3f));
        if (remaining <= 0f)
            HideAndReschedule();
    }

    private void OnHeartClicked()
    {
        if (!_visible || _group.alpha < 0.5f)
            return;

        if (_dogAI == null && _dogAnimator != null)
            _dogAI = _dogAnimator.GetComponent<DogWanderAI>();
        if (_dogAI != null)
            _dogAI.PlayHappyReaction();
        else if (_dogAnimator != null)
            _dogAnimator.SetTrigger("Jump");

        PlayDogParticles();
        PlayBoneReward();
        if (_missionController != null)
            _missionController.RegisterPetOrPlay();

        HideAndReschedule();
    }

    private void Show()
    {
        _visible = true;
        _shownAt = Time.time;
        _group.alpha = 0f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _rect.localScale = Vector3.zero;
    }

    private void HideAndReschedule()
    {
        HideImmediately();
        ScheduleNext();
    }

    private void HideImmediately()
    {
        _visible = false;
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _rect.localScale = Vector3.zero;
    }

    private void ScheduleNext()
    {
        _timer = UnityEngine.Random.Range(_appearInterval.x, _appearInterval.y);
    }

    private void EnsureRaycastTarget()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        if (graphics.Length > 0)
        {
            graphics[0].raycastTarget = true;
            return;
        }

        Image hitArea = gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
    }

    private void PlayDogParticles()
    {
        foreach (ParticleSystem particles in _dogParticles)
        {
            if (particles == null)
                continue;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }
    }

    public void PlayBoneReward()
    {
        PlayBoneRewardAt(_rect.position);
    }

    /// <summary>Spawns the bone reward from a world object such as the food plate.</summary>
    public void PlayBoneRewardFromWorld(Vector3 worldPosition)
    {
        RectTransform layer = _rewardLayer != null
            ? _rewardLayer
            : (_canvas != null ? (RectTransform)_canvas.transform : null);
        if (layer == null)
            return;

        Camera projectionCamera = _camera != null ? _camera : Camera.main;
        if (projectionCamera == null)
            return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(projectionCamera, worldPosition);
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(layer, screenPoint, uiCamera, out Vector3 uiPosition))
            PlayBoneRewardAt(uiPosition);
    }

    private void PlayBoneRewardAt(Vector3 startPosition)
    {
        if (_boneIconSource == null || _boneIconSource.sprite == null || _boneTarget == null)
        {
            Debug.LogWarning("[DogHeart] Assign Bone Icon Source and Bone Target in the Inspector.", this);
            return;
        }

        RectTransform layer = _rewardLayer != null
            ? _rewardLayer
            : (_canvas != null ? (RectTransform)_canvas.transform : null);
        if (layer == null)
            return;

        int min = Mathf.Min(_boneRewardCount.x, _boneRewardCount.y);
        int max = Mathf.Max(_boneRewardCount.x, _boneRewardCount.y);
        int count = UnityEngine.Random.Range(Mathf.Max(1, min), Mathf.Max(1, max) + 1);
        for (int i = 0; i < count; i++)
            StartCoroutine(FlyBoneIcon(layer, startPosition, i * 0.055f));
    }

    private IEnumerator FlyBoneIcon(RectTransform layer, Vector3 startPosition, float delay)
    {
        GameObject iconObject = new GameObject("Flying Bone Reward", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.SetParent(layer, false);
        iconRect.SetAsLastSibling();
        iconRect.sizeDelta = _boneIconSize;
        iconRect.position = startPosition;

        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = _boneIconSource.sprite;
        icon.color = _boneIconSource.color;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        Vector2 scatter = UnityEngine.Random.insideUnitCircle * _boneScatterRadius;
        Vector3 start = iconRect.position;
        Vector3 scatterWorld = start;
        Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                layer, RectTransformUtility.WorldToScreenPoint(uiCamera, start) + scatter,
                uiCamera,
                out Vector3 converted))
            scatterWorld = converted;

        float scatterTime = 0.18f;
        float elapsed = 0f;
        while (elapsed < scatterTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scatterTime);
            iconRect.position = Vector3.LerpUnclamped(start, scatterWorld, 1f - Mathf.Pow(1f - t, 3f));
            iconRect.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, t);
            yield return null;
        }

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        start = iconRect.position;
        elapsed = 0f;
        while (elapsed < _boneFlyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _boneFlyDuration));
            float eased = t * t * (3f - 2f * t);
            Vector3 target = _boneTarget.position;
            Vector3 arc = Vector3.up * (Mathf.Sin(t * Mathf.PI) * 35f);
            iconRect.position = Vector3.Lerp(start, target, eased) + arc;
            iconRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, eased);
            yield return null;
        }

        Destroy(iconObject);
        AddBoneReward(1);
    }

    private void AddBoneReward(int amount)
    {
        _boneCount += amount;
        RefreshBoneText();
        _onBoneCountChanged?.Invoke(_boneCount);
    }

    /// <summary>Call this from the dataset loader to initialize or refresh the displayed total.</summary>
    public void SetBoneCount(int value)
    {
        _boneCount = Mathf.Max(0, value);
        RefreshBoneText();
    }

    public void SetMeatCount(int value)
    {
        _meatCount = Mathf.Max(0, value);
        RefreshMeatText();
    }

    public void AddMeat(int amount)
    {
        _meatCount = Mathf.Max(0, _meatCount + amount);
        RefreshMeatText();
        _onMeatCountChanged?.Invoke(_meatCount);
    }

    public void SetHeartCount(int value)
    {
        _heartCount = Mathf.Clamp(value, 0, 5);
        RefreshHearts();
    }

    /// <summary>Call this once when a game actually starts. It is not called automatically yet.</summary>
    public bool ConsumeHeartForGame()
    {
        if (_heartCount <= 0)
            return false;

        _heartCount--;
        RefreshHearts();
        _onHeartCountChanged?.Invoke(_heartCount);
        return true;
    }

    private void RefreshBoneText()
    {
        if (_boneCountText != null)
            _boneCountText.text = _boneCount.ToString();
    }

    private void RefreshMeatText()
    {
        if (_meatCountText != null)
            _meatCountText.text = _meatCount.ToString();
    }

    private void RefreshHearts()
    {
        if (_gameHearts == null)
            return;

        for (int i = 0; i < _gameHearts.Length; i++)
        {
            if (_gameHearts[i] != null)
                _gameHearts[i].SetActive(i < _heartCount);
        }
    }

    private static Transform FindDogRoot(Transform start)
    {
        Transform dogRoot = start;
        for (Transform current = start; current != null; current = current.parent)
        {
            if (current.name.Equals("Dog", StringComparison.OrdinalIgnoreCase))
                dogRoot = current;
        }
        return dogRoot;
    }

    private void UpdateHeadPosition()
    {
        if (_dogAnimator == null)
            return;

        Transform anchor = _dogRoot != null ? _dogRoot : _dogAnimator.transform;
        _headPosition = anchor.position + Vector3.up * _headOffset;
    }

    private void FollowDog()
    {
        if (_canvas.renderMode == RenderMode.WorldSpace)
        {
            _rect.position = _headPosition;
            return;
        }

        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        Camera projectionCamera = canvasCamera != null ? canvasCamera : (_camera != null ? _camera : Camera.main);
        if (projectionCamera == null) return;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(projectionCamera, _headPosition);
        RectTransform canvasRect = (RectTransform)_canvas.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, canvasCamera, out Vector2 local))
            _rect.anchoredPosition = local;
    }
}
