using System.Collections;
using UnityEngine;

public sealed class SurveyDogReveal : MonoBehaviour
{
    private Coroutine _animation;

    public void Play(Transform borderCollie)
    {
        if (borderCollie == null) return;
        if (_animation != null) StopCoroutine(_animation);
        _animation = StartCoroutine(ScaleUp(borderCollie));
    }

    private IEnumerator ScaleUp(Transform target)
    {
        Vector3 finalScale = target.localScale;
        target.localScale = finalScale * 0.12f;
        const float duration = 0.34f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = Mathf.Sin(t * Mathf.PI) * 0.08f;
            target.localScale = finalScale * (eased + overshoot);
            yield return null;
        }
        target.localScale = finalScale;
        _animation = null;
    }
}
