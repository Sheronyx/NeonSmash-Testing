using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class SpawnPulse : MonoBehaviour
{
    [Header("Target (Visual Parent!)")]
    public Transform visualRoot;

    [Header("Scale Pulse")]
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private float startScale = 0.4f;
    [SerializeField] private float overshootScale = 0.6f;
    [SerializeField] private AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Fade")]
    [SerializeField] private bool doFade = true;
    [SerializeField] private float fadeDuration = 0.14f;

    private SpriteRenderer[] spriteRenderers;
    private Vector3 originalScale;

    void Reset()
    {
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    void Awake()
    {
        if (visualRoot == null)
            visualRoot = transform;

        spriteRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        originalScale = visualRoot.localScale;
    }

    void OnEnable()
    {
        Play();
    }

    public void Play()
    {
        if (!visualRoot) return;

        StopAllCoroutines();

        // sauberer Start
        visualRoot.localScale = originalScale * startScale;

        if (doFade)
            SetAlphaInstant(0f);

        StartCoroutine(Co_Pulse());

        if (doFade)
            StartCoroutine(Co_FadeIn());
    }

    private IEnumerator Co_Pulse()
    {
        float half = duration * 0.55f;
        float t = 0f;

        // Start → Overshoot
        while (t < half)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half);
            float s = Mathf.Lerp(startScale, overshootScale, easeIn.Evaluate(p));
            visualRoot.localScale = originalScale * s;
            yield return null;
        }

        // Overshoot → Normal
        float half2 = duration - half;
        t = 0f;

        while (t < half2)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / half2);
            float s = Mathf.Lerp(overshootScale, 1f, easeOut.Evaluate(p));
            visualRoot.localScale = originalScale * s;
            yield return null;
        }

        visualRoot.localScale = originalScale;
    }

    private IEnumerator Co_FadeIn()
    {
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            SetAlphaInstant(a);
            yield return null;
        }

        SetAlphaInstant(1f);
    }

    private void SetAlphaInstant(float alpha)
    {
        foreach (var sr in spriteRenderers)
        {
            if (!sr) continue;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}