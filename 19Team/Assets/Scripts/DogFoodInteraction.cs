using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Shows the DogFood UI above the plate when it is time to eat.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class DogFoodInteraction : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private RectTransform _dogFood;
    [SerializeField] private Button _dogFoodButton;
    [SerializeField] private Transform _plate;
    [SerializeField] private DogWanderAI _dog;
    [Tooltip("Uses the existing bone UI, count, and dataset event from DogHeartInteraction.")]
    [SerializeField] private DogHeartInteraction _boneRewardSource;
    [SerializeField] private MissionUIController _missionController;

    [Header("Meal Timing")]
    [Tooltip("Random minimum/maximum delay before DogFood appears.")]
    [SerializeField] private Vector2 _mealInterval = new Vector2(5f, 10f);
    [Tooltip("0 keeps DogFood visible until it is clicked.")]
    [SerializeField] private float _visibleDuration;

    [Header("Position")]
    [Tooltip("World-space Y offset from the Plate position.")]
    [SerializeField] private float _heightOffset = 1f;

    [Header("Appearance")]
    [SerializeField] private float _popDuration = 0.4f;

    [Header("Food Event")]
    [Tooltip("Connect the feeding/dataset method that should run when DogFood is clicked.")]
    [SerializeField] private UnityEvent _onFoodClicked;
    [Tooltip("How far from the Plate the dog stops before eating.")]
    [SerializeField] private float _approachDistance = 1.5f;
    [SerializeField] private float _eatingSeconds = 2.5f;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Camera _camera;
    private float _timer;
    private float _shownAt;
    private bool _visible;
    private bool _feedQueued;

    public void SetDog(DogWanderAI dog) => _dog = dog;

    private void Awake()
    {
        if (_dog == null)
            _dog = UnityEngine.Object.FindFirstObjectByType<DogWanderAI>(FindObjectsInactive.Include);
        _canvas = GetComponent<Canvas>();
        if (_dogFood == null || _plate == null)
        {
            Debug.LogError("[DogFood] Assign DogFood and Plate references on the Canvas.", this);
            enabled = false;
            return;
        }

        _dogFood.gameObject.SetActive(true);
        _dogFoodButton ??= _dogFood.GetComponent<Button>();
        _group = _dogFood.GetComponent<CanvasGroup>();
        if (_group == null)
            _group = _dogFood.gameObject.AddComponent<CanvasGroup>();

        // The UI object previously had the dog's world animation controller attached.
        // It must not drive the UI RectTransform while this component positions it.
        Animator uiAnimator = _dogFood.GetComponent<Animator>();
        if (uiAnimator != null)
            uiAnimator.enabled = false;

        _camera = Camera.main;
        if (_dogFoodButton != null)
            _dogFoodButton.onClick.AddListener(OnFoodClicked);

        FollowPlate();
        HideImmediately();
        ScheduleNextMeal();
    }

    private void OnDestroy()
    {
        if (_dogFoodButton != null)
            _dogFoodButton.onClick.RemoveListener(OnFoodClicked);
    }

    private void Update()
    {
        FollowPlate();

        if (!_visible)
        {
            if (_feedQueued)
                return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                ShowNow();
            return;
        }

        float age = Time.time - _shownAt;
        float pop = Mathf.Clamp01(age / Mathf.Max(0.01f, _popDuration));
        float overshoot = 1f + 1.7f * Mathf.Pow(pop - 1f, 3f) + 0.7f * Mathf.Pow(pop - 1f, 2f);
        _dogFood.localScale = Vector3.one * overshoot;
        _group.alpha = Mathf.Clamp01(pop * 3f);

        if (_visibleDuration > 0f && age >= _visibleDuration)
            HideAndReschedule();
    }

    public void ShowNow()
    {
        if (_dogFood == null || _group == null)
            return;

        FollowPlate();
        _visible = true;
        _shownAt = Time.time;
        _group.alpha = 0f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _dogFood.localScale = Vector3.zero;
    }

    public void SetMealReady(bool ready)
    {
        if (ready)
            ShowNow();
        else
            HideAndReschedule();
    }

    public void HideAndReschedule()
    {
        HideImmediately();
        ScheduleNextMeal();
    }

    private void OnFoodClicked()
    {
        if (!_visible)
            return;

        if (_dog == null)
            _dog = UnityEngine.Object.FindFirstObjectByType<DogWanderAI>(FindObjectsInactive.Include);

        // Button feedback must never be swallowed by another dog animation.
        _onFoodClicked?.Invoke();
        Vector3 rewardOrigin = PlayPlateParticles();
        if (_boneRewardSource != null)
            _boneRewardSource.PlayBoneRewardFromWorld(rewardOrigin);
        if (_missionController != null)
            _missionController.RegisterFeed();
        PlayerLevelStore.AddExperience(10);
        HideImmediately();
        _timer = float.PositiveInfinity;
        if (!_feedQueued)
            StartCoroutine(FeedWhenDogIsReady());
    }

    private IEnumerator FeedWhenDogIsReady()
    {
        _feedQueued = true;
        while (_dog == null)
        {
            _dog = UnityEngine.Object.FindFirstObjectByType<DogWanderAI>(FindObjectsInactive.Include);
            yield return null;
        }
        while (_dog.IsPerformingSpecialAction)
            yield return null;
        while (!_dog.PerformFeed(_plate, _approachDistance, _eatingSeconds, null))
            yield return null;
        HomeAudioManager.PlayEat();
        _feedQueued = false;
        ScheduleNextMeal();
    }

    private Vector3 PlayPlateParticles()
    {
        if (_plate == null)
            return Vector3.zero;

        ParticleSystem[] particleSystems = _plate.GetComponentsInChildren<ParticleSystem>(true);
        Vector3 origin = particleSystems.Length > 0 ? particleSystems[0].transform.position : _plate.position;
        foreach (ParticleSystem particles in particleSystems)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }
        return origin;
    }

    private void HideImmediately()
    {
        _visible = false;
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }
        if (_dogFood != null)
            _dogFood.localScale = Vector3.zero;
    }

    private void ScheduleNextMeal()
    {
        float min = Mathf.Max(0.1f, Mathf.Min(_mealInterval.x, _mealInterval.y));
        float max = Mathf.Max(min, Mathf.Max(_mealInterval.x, _mealInterval.y));
        _timer = Random.Range(min, max);
    }

    private void FollowPlate()
    {
        if (_plate == null || _dogFood == null || _canvas == null)
            return;

        Vector3 worldPosition = _plate.position + Vector3.up * _heightOffset;
        if (_canvas.renderMode == RenderMode.WorldSpace)
        {
            _dogFood.position = worldPosition;
            return;
        }

        if (_camera == null)
            _camera = Camera.main;
        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _camera;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(_camera, worldPosition);
        RectTransform canvasRect = (RectTransform)_canvas.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
            _dogFood.anchoredPosition = localPoint;
    }
}
