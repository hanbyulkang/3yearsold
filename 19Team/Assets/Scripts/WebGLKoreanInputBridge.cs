using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>WebGL 브라우저의 IME를 TMP_InputField에 연결한다.</summary>
public sealed class WebGLKoreanInputBridge : MonoBehaviour
{
    private const string BridgeName = "WebGLKoreanInputBridge";
    private static WebGLKoreanInputBridge _instance;
    private static WebGLKoreanInputTarget _active;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SuntailIMEBegin(string value, int multiline);
    [DllImport("__Internal")] private static extern void SuntailIMEEnd();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (_instance == null)
        {
            var host = new GameObject(BridgeName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<WebGLKoreanInputBridge>();
            SceneManager.sceneLoaded += (_, __) => _instance.WireAll();
        }
        _instance.WireAll();
    }

    private void Start() => WireAll();

    private void WireAll()
    {
#if UNITY_2023_1_OR_NEWER
        var fields = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var fields = FindObjectsOfType<TMP_InputField>(true);
#endif
        foreach (var field in fields)
            if (field.GetComponent<WebGLKoreanInputTarget>() == null)
                field.gameObject.AddComponent<WebGLKoreanInputTarget>();
    }

    internal static void Begin(WebGLKoreanInputTarget target)
    {
        _active = target;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = false;
        SuntailIMEBegin(target.Field.text ?? string.Empty,
            target.Field.lineType == TMP_InputField.LineType.SingleLine ? 0 : 1);
#endif
    }

    internal static void End(WebGLKoreanInputTarget target)
    {
        if (_active != target) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        SuntailIMEEnd();
        WebGLInput.captureAllKeyboardInput = true;
#endif
        _active = null;
    }

    // Called by the .jslib plug-in. Keep these public names unchanged.
    public void OnIMEValue(string value)
    {
        if (_active != null) _active.Apply(value);
    }

    public void OnIMEEnd(string value)
    {
        if (_active != null) _active.Apply(value);
    }
}

[RequireComponent(typeof(TMP_InputField))]
public sealed class WebGLKoreanInputTarget : MonoBehaviour
{
    internal TMP_InputField Field { get; private set; }
    private bool _applying;

    private void Awake() => Field = GetComponent<TMP_InputField>();

    private void OnEnable()
    {
        if (Field == null) Field = GetComponent<TMP_InputField>();
        Field.onSelect.AddListener(OnSelected);
        Field.onDeselect.AddListener(OnDeselected);
    }

    private void OnDisable()
    {
        if (Field == null) return;
        Field.onSelect.RemoveListener(OnSelected);
        Field.onDeselect.RemoveListener(OnDeselected);
        WebGLKoreanInputBridge.End(this);
    }

    private void OnSelected(string _) => WebGLKoreanInputBridge.Begin(this);
    private void OnDeselected(string _) => WebGLKoreanInputBridge.End(this);

    internal void Apply(string value)
    {
        if (_applying || Field == null || Field.text == value) return;
        _applying = true;
        Field.text = value ?? string.Empty;
        Field.caretPosition = Field.text.Length;
        Field.ForceLabelUpdate();
        _applying = false;
    }
}
