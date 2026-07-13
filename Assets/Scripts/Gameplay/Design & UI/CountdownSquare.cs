using UnityEngine;

public class CountdownSquare : MonoBehaviour
{
    private float totalDuration;
    private float elapsedTime = 0f;
    private Vector3 baseScale;
    private bool isCountingDown = false;

    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

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
        transform.localScale = baseScale;

        totalDuration = duration;
        elapsedTime = 0f;
        isCountingDown = true;
    }
}
