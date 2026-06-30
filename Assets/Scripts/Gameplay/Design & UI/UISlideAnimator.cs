using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Wiederverwendbare Slide-Animation für UI RectTransforms.
/// SlideIn: von rechts/links mit BackEaseOut (Overshoot-Gummieffekt).
/// SlideOut: nach links/rechts mit EaseInQuad (beschleunigt raus).
/// Nutzt unscaledDeltaTime — funktioniert auch bei timeScale=0.
/// </summary>
public class UISlideAnimator : MonoBehaviour
{
    [SerializeField] public float slideInDuration  = 0.45f;
    [SerializeField] public float slideOutDuration = 0.30f;
    [SerializeField] private float slideDistance   = 1600f;
    [SerializeField] private float overshootAmount = 1.70158f;
    [Tooltip("Startet das Element beim Awake rechts außerhalb des Bildschirms (versteckt).")]
    [SerializeField] private bool startHidden = false;

    private RectTransform _rt;
    private Vector2       _homePos;
    private Coroutine     _current;

    private void Awake()
    {
        _rt      = GetComponent<RectTransform>();
        _homePos = _rt.anchoredPosition;
        if (startHidden)
            _rt.anchoredPosition = _homePos + new Vector2(slideDistance, 0f);
    }

    public void SlideInFromRight(Action onComplete = null) => Trigger(Co_SlideIn(slideDistance,  onComplete));
    public void SlideInFromLeft(Action onComplete = null)  => Trigger(Co_SlideIn(-slideDistance, onComplete));

    public void SlideOutToLeft(Action onComplete = null)  => Trigger(Co_SlideOut(-slideDistance, onComplete));
    public void SlideOutToRight(Action onComplete = null) => Trigger(Co_SlideOut(slideDistance,  onComplete));

    private void Trigger(IEnumerator routine)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(routine);
    }

    private IEnumerator Co_SlideIn(float fromOffsetX, Action onComplete)
    {
        Vector2 from = _homePos + new Vector2(fromOffsetX, 0f);
        _rt.anchoredPosition = from;

        float t = 0f;
        while (t < slideInDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = BackEaseOut(Mathf.Clamp01(t / slideInDuration));
            _rt.anchoredPosition = Vector2.Lerp(from, _homePos, k);
            yield return null;
        }
        _rt.anchoredPosition = _homePos;
        onComplete?.Invoke();
        _current = null;
    }

    private IEnumerator Co_SlideOut(float toOffsetX, Action onComplete)
    {
        Vector2 from   = _rt.anchoredPosition;
        Vector2 target = _homePos + new Vector2(toOffsetX, 0f);

        float t = 0f;
        while (t < slideOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseInQuad(Mathf.Clamp01(t / slideOutDuration));
            _rt.anchoredPosition = Vector2.Lerp(from, target, k);
            yield return null;
        }
        _rt.anchoredPosition = target;
        onComplete?.Invoke();
        _current = null;
    }

    // Overshoot: geht kurz zu weit, federt zurück auf Zielposition
    private float BackEaseOut(float t)
    {
        float c3 = overshootAmount + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshootAmount * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseInQuad(float t) => t * t;
}
