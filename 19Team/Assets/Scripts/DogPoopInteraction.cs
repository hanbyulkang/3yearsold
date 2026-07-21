using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Schedules dog bathroom breaks and handles cleaning the spawned poop.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class DogPoopInteraction : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform _dogPoop;
    [SerializeField] private Button _dogPoopButton;
    [SerializeField] private DogWanderAI _dog;
    [SerializeField] private Transform _dogWorldAnchor;
    [Tooltip("Scene Poop object used as the spawn template, including its particle children.")]
    [SerializeField] private Transform _poopTemplate;
    [SerializeField] private DogHeartInteraction _boneRewardSource;
    [SerializeField] private MissionUIController _missionController;

    [Header("Timing")]
    [SerializeField] private Vector2 _poopInterval = new Vector2(6f, 10f);
    [SerializeField] private float _sittingSeconds = 3f;

    [Header("UI Position")]
    [SerializeField] private float _heightOffset = 0.8f;
    [SerializeField] private float _popDuration = 0.4f;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Camera _camera;
    private Transform _activePoop;
    private float _timer;
    private float _shownAt;
    private bool _visible;
    private bool _dogIsPooping;
    private bool _initialized;

    public void Configure(RectTransform dogPoop, Button button, DogWanderAI dog, Transform poopTemplate,
                          DogHeartInteraction boneRewardSource, MissionUIController missionController,
                          Transform dogWorldAnchor)
    {
        _dogPoop = dogPoop;
        _dogPoopButton = button;
        _dog = dog;
        _dogWorldAnchor = dogWorldAnchor;
        _poopTemplate = poopTemplate;
        _boneRewardSource = boneRewardSource;
        _missionController = missionController;
        enabled = true;
        if (Application.isPlaying) InitializeRuntime();
    }

    private void Awake()
    {
        InitializeRuntime();
    }

    private void InitializeRuntime()
    {
        if (_initialized) return;
        _canvas = GetComponent<Canvas>();
        ResolveMissingReferences();
        if (_dogPoop == null || _dog == null || _poopTemplate == null)
        {
            Debug.LogError("[DogPoop] Runtime initialization failed: DogPoop UI, Dog, or Poop is missing.", this);
            return;
        }

        _initialized = true;

        _dogPoop.gameObject.SetActive(true);
        _dogPoopButton ??= _dogPoop.GetComponent<Button>();
        _group = _dogPoop.GetComponent<CanvasGroup>();
        if (_group == null) _group = _dogPoop.gameObject.AddComponent<CanvasGroup>();
        Animator uiAnimator = _dogPoop.GetComponent<Animator>();
        if (uiAnimator != null) uiAnimator.enabled = false;
        if (_dogPoopButton != null) _dogPoopButton.onClick.AddListener(OnPoopClicked);

        _camera = Camera.main;
        _poopTemplate.gameObject.SetActive(false);
        HideUI();
        ScheduleNext();
        Debug.Log("[DogPoop] Initialized and scheduled the next bathroom break.", this);
    }

    private void ResolveMissingReferences()
    {
        if (_dog == null)
            _dog = UnityEngine.Object.FindFirstObjectByType<DogWanderAI>(FindObjectsInactive.Include);
        if (_boneRewardSource == null && _canvas != null)
            _boneRewardSource = _canvas.GetComponent<DogHeartInteraction>();
        if (_missionController == null)
            _missionController = UnityEngine.Object.FindFirstObjectByType<MissionUIController>(FindObjectsInactive.Include);

        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (_dogPoop == null && candidate is RectTransform rect && candidate.name == "DogPoop")
            {
                _dogPoop = rect;
                _dogPoopButton = rect.GetComponent<Button>();
            }
            if (_poopTemplate == null && !(candidate is RectTransform) && candidate.name == "Poop")
                _poopTemplate = candidate;
            if (_dogWorldAnchor == null && !(candidate is RectTransform) && candidate.name == "Dog")
                _dogWorldAnchor = candidate;
        }
    }

    private void OnDestroy()
    {
        if (_dogPoopButton != null) _dogPoopButton.onClick.RemoveListener(OnPoopClicked);
    }

    private void Update()
    {
        if (!_initialized)
        {
            InitializeRuntime();
            return;
        }
        if (_activePoop != null)
        {
            FollowPoop();
            AnimateUI();
            return;
        }
        if (_dogIsPooping) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _dogIsPooping = _dog.PerformPoop(_sittingSeconds, SpawnPoop);
            if (!_dogIsPooping) _timer = 1f;
            else Debug.Log("[DogPoop] Bathroom animation started.", this);
        }
    }

    private void SpawnPoop(Vector3 dogPosition)
    {
        _dogIsPooping = false;
        Vector3 currentDogPosition = _dogWorldAnchor != null ? _dogWorldAnchor.position : dogPosition;
        Vector3 position = currentDogPosition;
        _poopTemplate.SetParent(null, true);
        _poopTemplate.position = position;
        _poopTemplate.gameObject.SetActive(true);
        _activePoop = _poopTemplate;
        Debug.Log($"[DogPoop] Dog world={currentDogPosition}, 3D Poop world={_poopTemplate.position}, parent=null.", this);

        foreach (Renderer renderer in _poopTemplate.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
        foreach (ParticleSystem particles in _poopTemplate.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ShowUI();
    }


    private void OnPoopClicked()
    {
        if (!_visible || _activePoop == null || _group.alpha < 0.5f) return;

        Transform cleanedPoop = _activePoop;
        ParticleSystem[] particles = cleanedPoop.GetComponentsInChildren<ParticleSystem>(true);
        Vector3 rewardOrigin = particles.Length > 0 ? particles[0].transform.position : cleanedPoop.position;
        float destroyDelay = 1.5f;
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
            ParticleSystem.MainModule main = particle.main;
            destroyDelay = Mathf.Max(destroyDelay, main.duration + main.startLifetime.constantMax);
        }
        foreach (Renderer renderer in cleanedPoop.GetComponentsInChildren<Renderer>(true))
            if (!(renderer is ParticleSystemRenderer)) renderer.enabled = false;

        if (_boneRewardSource != null) _boneRewardSource.PlayBoneRewardFromWorld(rewardOrigin);
        if (_missionController != null) _missionController.RegisterCleanPoop();
        PlayerLevelStore.AddExperience(10);

        _activePoop = null;
        HideUI();
        StartCoroutine(DeactivateAfter(cleanedPoop.gameObject, destroyDelay));
        ScheduleNext();
    }

    private IEnumerator DeactivateAfter(GameObject target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target == null) yield break;
        target.SetActive(false);
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
    }

    private void ShowUI()
    {
        FollowPoop();
        _visible = true;
        _shownAt = Time.time;
        _group.alpha = 0f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _dogPoop.localScale = Vector3.zero;
    }

    private void HideUI()
    {
        _visible = false;
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        if (_dogPoop != null) _dogPoop.localScale = Vector3.zero;
    }

    private void AnimateUI()
    {
        if (!_visible) return;
        float t = Mathf.Clamp01((Time.time - _shownAt) / Mathf.Max(0.01f, _popDuration));
        float scale = 1f + 1.7f * Mathf.Pow(t - 1f, 3f) + 0.7f * Mathf.Pow(t - 1f, 2f);
        _dogPoop.localScale = Vector3.one * scale;
        _group.alpha = Mathf.Clamp01(t * 3f);
    }

    private void FollowPoop()
    {
        if (_activePoop == null) return;
        Vector3 worldPosition = _activePoop.position + Vector3.up * _heightOffset;
        if (_canvas.renderMode == RenderMode.WorldSpace) { _dogPoop.position = worldPosition; return; }
        Camera projectionCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? (_camera != null ? _camera : Camera.main) : _canvas.worldCamera;
        if (projectionCamera == null) return;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(projectionCamera, worldPosition);
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, uiCamera, out Vector2 local))
            _dogPoop.anchoredPosition = local;
    }

    private void ScheduleNext()
    {
        float min = Mathf.Max(0.1f, Mathf.Min(_poopInterval.x, _poopInterval.y));
        float max = Mathf.Max(min, Mathf.Max(_poopInterval.x, _poopInterval.y));
        _timer = Random.Range(min, max);
    }
}
