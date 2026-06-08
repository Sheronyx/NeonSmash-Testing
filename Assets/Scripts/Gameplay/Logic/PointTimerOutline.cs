using UnityEngine;
using System.Collections;

public class PointTimerOutline : MonoBehaviour
{
    [SerializeField] private SpriteRenderer timerOuterRect;

    [Header("Timer Outline Größe")]
    [SerializeField] private float startScale = 1.5f;  // 50% größer beim Start
    [SerializeField] private float endScale = 1.0f;    // Normal-Größe am Ende

    [Header("Element Pulse")]
    [SerializeField] private bool doPulse = true;
    [SerializeField] private float pulseAmount = 0.15f;  // 15% größer/kleiner
    [SerializeField] private float pulseSpeed = 2.5f;    // wie schnell pulsiert

    [Header("Farbe (optional)")]
    [SerializeField] private bool changeColor = false;
    [SerializeField] private Color warningColor = new Color(1f, 0.2f, 0f, 1f);  // Rot

    private Vector3 baseScale;
    private Vector3 elementBaseScale;
    private Color originalColor;
    private Coroutine countdown;

    private void Awake()
    {
        if (timerOuterRect == null)
            timerOuterRect = GetComponentInChildren<SpriteRenderer>();

        if (timerOuterRect)
        {
            baseScale = timerOuterRect.transform.localScale;
            originalColor = timerOuterRect.color;
        }

        elementBaseScale = transform.localScale;
    }

    public void StartCountdown(float duration)
    {
        if (countdown != null) StopCoroutine(countdown);
        countdown = StartCoroutine(Co_Countdown(duration));
    }

    private IEnumerator Co_Countdown(float duration)
    {
        if (timerOuterRect == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(elapsed / duration);

            // Outline schrumpfen + pulsieren
            float shrinkScale = Mathf.Lerp(endScale, startScale, remaining);
            float pulse = doPulse ? (1f + Mathf.Sin(Time.time * Mathf.PI * pulseSpeed) * pulseAmount) : 1f;
            float finalScale = shrinkScale * pulse;
            timerOuterRect.transform.localScale = baseScale * finalScale;

            // Farbe wechseln
            if (changeColor)
            {
                timerOuterRect.color = Color.Lerp(originalColor, warningColor, 1f - remaining);
            }

            yield return null;
        }

        timerOuterRect.transform.localScale = baseScale * endScale;
        if (changeColor)
            timerOuterRect.color = originalColor;

        countdown = null;
    }
}
