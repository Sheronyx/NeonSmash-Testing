using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class PointTimerRing : MonoBehaviour
{
    [Header("Größe")]
    [SerializeField] private float outerScale = 0.25f;  // Start-Größe (anpassen wenn Ring zu groß/klein)
    [SerializeField] private float innerScale = 0.1f;   // End-Größe

    [Header("Farben")]
    [SerializeField] private Color normalColor      = new Color(0f, 1f, 1f, 0.85f);    // Cyan
    [SerializeField] private Color warningColor     = new Color(1f, 0.25f, 0f, 0.9f);  // Orange-Rot
    [SerializeField] [Range(0f, 0.5f)]
    private float warningThreshold = 0.3f;  // ab welchem Restzeit-Anteil Farbe wechselt

    [Header("Sorting")]
    [SerializeField] private int sortingOrder = -1;  // negativ = hinter dem Element

    private SpriteRenderer sr;
    private Coroutine countdown;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        sr.color = normalColor;
        transform.localScale = Vector3.one * outerScale;
    }

    public void StartCountdown(float duration)
    {
        if (countdown != null) StopCoroutine(countdown);
        countdown = StartCoroutine(Co_Countdown(duration));
    }

    private IEnumerator Co_Countdown(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(elapsed / duration);

            transform.localScale = Vector3.one * Mathf.Lerp(innerScale, outerScale, remaining);

            sr.color = remaining <= warningThreshold
                ? Color.Lerp(normalColor, warningColor, 1f - remaining / warningThreshold)
                : normalColor;

            yield return null;
        }

        transform.localScale = Vector3.one * innerScale;
        countdown = null;
    }
}
