using System.Collections;
using UnityEngine;

public class CountdownSquare : MonoBehaviour
{
    private float totalDuration;
    private float elapsedTime = 0f;
    private Vector3 baseScale;
    private bool isCountingDown = false;

    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [Tooltip("Wie lange das Quadrat beim Start smooth von 0 auf volle Größe wächst, bevor der eigentliche Countdown (Schrumpfen) beginnt. Wird von der Reaktionszeit ABGEZOGEN, damit die Skalierung exakt zum tatsächlichen Timeout bei 0 ankommt.")]
    [SerializeField] private float growInDuration = 0.35f;

    private Coroutine growInRoutine;

    private void Awake()
    {
        // Soll erst beim Aufpoppen (StartCountdown) sichtbar werden, nicht schon während des
        // Einflugs (PointFlyIn) — Basisgröße einmalig sichern, dann unsichtbar starten.
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (!isCountingDown) return;

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / totalDuration);  // 0 → 1

        // Easing-Kurve für smoothere Bewegung
        float easedProgress = easingCurve.Evaluate(progress);

        // Scale basierend auf eased progress (1 → 0)
        transform.localScale = baseScale * (1f - easedProgress);

        // Wenn Zeit rum → Fertig
        if (elapsedTime >= totalDuration)
        {
            isCountingDown = false;
            transform.localScale = Vector3.zero;
        }
    }

    public void StartCountdown(float duration)
    {
        if (growInRoutine != null) StopCoroutine(growInRoutine);
        growInRoutine = StartCoroutine(Co_GrowInThenCountdown(duration));
    }

    // Wächst zuerst smooth von 0 auf Basisgröße (statt wie früher hart auf Basisgröße zu springen),
    // dann startet die bestehende Schrumpf-Logik — verkürzt um growInDuration, damit die Skalierung
    // exakt zum selben Zeitpunkt bei 0 ankommt, zu dem Co_SlotTimeout (identische reactionTime)
    // tatsächlich auslöst.
    private IEnumerator Co_GrowInThenCountdown(float duration)
    {
        float t = 0f;
        while (t < growInDuration)
        {
            t += Time.deltaTime;
            transform.localScale = baseScale * Mathf.Clamp01(t / growInDuration);
            yield return null;
        }
        transform.localScale = baseScale;

        totalDuration = Mathf.Max(0.01f, duration - growInDuration);
        elapsedTime = 0f;
        isCountingDown = true;
    }
}
