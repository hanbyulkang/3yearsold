using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class HomeAudioManager : MonoBehaviour
{
    private static HomeAudioManager _instance;
    private AudioSource _bgmSource;
    private AudioSource _sfxSource;
    private AudioClip _happyClip;
    private AudioClip _eatClip;
    private AudioClip _buttonClip;
    private AudioClip _boneClip;
    private bool _bgmStarted;
    private readonly HashSet<Button> _wiredButtons = new HashSet<Button>();
    private float _nextButtonScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;
        GameObject root = new GameObject("Home Audio Manager");
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<HomeAudioManager>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.clip = Resources.Load<AudioClip>("BGM");
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.volume = 0.65f;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;
        _happyClip = Resources.Load<AudioClip>("Happy");
        _eatClip = Resources.Load<AudioClip>("Eat");
        _buttonClip = Resources.Load<AudioClip>("Button");
        _boneClip = Resources.Load<AudioClip>("Bone");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplySceneAudio(SceneManager.GetActiveScene());
        WireAllButtons();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextButtonScan) return;
        _nextButtonScan = Time.unscaledTime + 0.5f;
        WireAllButtons();
    }
    private void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _wiredButtons.RemoveWhere(button => button == null);
        ApplySceneAudio(scene);
        WireAllButtons();
    }

    private void WireAllButtons()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button == null || !_wiredButtons.Add(button)) continue;
            button.onClick.AddListener(PlayButton);
        }
    }

    private void ApplySceneAudio(Scene scene)
    {
        bool homeFlow = scene.name.Equals("Main", StringComparison.OrdinalIgnoreCase) ||
                        scene.name.Equals("Survey", StringComparison.OrdinalIgnoreCase) ||
                        scene.name.Equals("Suntail Village", StringComparison.OrdinalIgnoreCase);
        if (!homeFlow)
        {
            if (_bgmSource != null && _bgmSource.isPlaying) _bgmSource.Pause();
            return;
        }

        if (_bgmSource == null || _bgmSource.clip == null) return;
        if (!_bgmStarted)
        {
            _bgmSource.Play();
            _bgmStarted = true;
        }
        else if (!_bgmSource.isPlaying)
        {
            _bgmSource.UnPause();
        }
    }

    public static void PlayHappy()
    {
        if (_instance != null && _instance._happyClip != null)
            _instance._sfxSource.PlayOneShot(_instance._happyClip);
    }

    public static void PlayEat()
    {
        if (_instance != null && _instance._eatClip != null)
            _instance._sfxSource.PlayOneShot(_instance._eatClip);
    }

    public static void PlayButton()
    {
        if (_instance != null && _instance._buttonClip != null)
            _instance._sfxSource.PlayOneShot(_instance._buttonClip);
    }

    public static void PlayBone()
    {
        if (_instance != null && _instance._boneClip != null)
            _instance._sfxSource.PlayOneShot(_instance._boneClip);
    }
}
