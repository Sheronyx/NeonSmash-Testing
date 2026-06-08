using UnityEngine;
using System.Collections;

public class ReactionTimeVisualizer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer outerRect;
    [SerializeField] private SpriteRenderer innerRect;

    [Header("Pulse")]
    [SerializeField] private float minPulseAmount = 0.05f;  // bei viel Zeit
    [SerializeField] private float maxPulseAmount = 0.25f;  // bei wenig Zeit
    [SerializeField] private float minPulseSpeed = 1.5f;    // bei viel Zeit
    [SerializeField] private float maxPulseSpeed = 4f;      // bei wenig Zeit

    [Header("Glow Color")]
    [SerializeField] private Color warningColor = new Color(1f, 0.1f, 0f, 1f);  // Rot

    private Vector3 outerBaseScale;
    private Vector3 innerBaseScale;
    private Material outerMat;
    private Material innerMat;
    private Color originalGlowColor;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        if (outerRect) outerBaseScale = outerRect.transform.localScale;
        if (innerRect) innerBaseScale = innerRect.transform.localScale;

        if (outerRect)
        {
            outerMat = outerRect.material;
            if (outerMat.HasProperty("_GlowColor"))
                originalGlowColor = outerMat.GetColor("_GlowColor");
        }
        if (innerRect && !outerRect)
        {
            innerMat = innerRect.material;
            if (innerMat.HasProperty("_GlowColor"))
                originalGlowColor = innerMat.GetColor("_GlowColor");
        }
    }

    public void StartVisualization(float duration)
    {
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(Co_Visualize(duration));
    }

    public void StopVisualization()
    {
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);

        if (outerRect) outerRect.transform.localScale = outerBaseScale;
        if (innerRect) innerRect.transform.localScale = innerBaseScale;

        if (outerMat && outerMat.HasProperty("_GlowColor"))
            outerMat.SetColor("_GlowColor", originalGlowColor);
    }

    private IEnumerator Co_Visualize(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;  // 0 → 1
            float remaining = 1f - progress;     // 1 → 0

            // Pulsing: schneller bei wenig Zeit
            float pulseAmount = Mathf.Lerp(minPulseAmount, maxPulseAmount, progress);
            float pulseSpeed = Mathf.Lerp(minPulseSpeed, maxPulseSpeed, progress);
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.PI * pulseSpeed) * pulseAmount;

            if (outerRect)
                outerRect.transform.localScale = outerBaseScale * pulse;
            if (innerRect)
                innerRect.transform.localScale = innerBaseScale * pulse;

            // Glow-Farbe: von Original → Rot
            if (outerMat && outerMat.HasProperty("_GlowColor"))
                outerMat.SetColor("_GlowColor", Color.Lerp(originalGlowColor, warningColor, progress));

            yield return null;
        }

        // Finale State
        if (outerRect) outerRect.transform.localScale = outerBaseScale;
        if (innerRect) innerRect.transform.localScale = innerBaseScale;
        if (outerMat && outerMat.HasProperty("_GlowColor"))
            outerMat.SetColor("_GlowColor", warningColor);

        pulseRoutine = null;
    }
}
