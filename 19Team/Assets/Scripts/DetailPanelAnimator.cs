using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class DetailPanelAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _closeButton;
    [SerializeField] private float _slideDistance = 900f;
    [SerializeField] private float _duration = 0.35f;
    private Vector2 _openPosition;
    private Coroutine _animation;

    private void Awake()
    {
        _panel ??= transform as RectTransform;
        _openPosition = _panel.anchoredPosition;
        if (_closeButton != null) _closeButton.onClick.AddListener(HideAnimated);
    }

    private void OnEnable()
    {
        if (_panel == null) return;
        if (_animation != null) StopCoroutine(_animation);
        _animation = StartCoroutine(Slide(_openPosition - Vector2.up * _slideDistance, _openPosition, false));
    }

    private void OnDestroy()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(HideAnimated);
    }

    public void ShowAnimated()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        else OnEnable();
    }

    public void HideAnimated()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_animation != null) StopCoroutine(_animation);
        _animation = StartCoroutine(Slide(_panel.anchoredPosition, _openPosition - Vector2.up * _slideDistance, true));
    }

    private IEnumerator Slide(Vector2 from, Vector2 to, bool disableAfter)
    {
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _panel.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }
        _panel.anchoredPosition = to;
        _animation = null;
        if (disableAfter) gameObject.SetActive(false);
    }
}
