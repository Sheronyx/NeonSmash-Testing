using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class CountdownFuse : MonoBehaviour
{
    [SerializeField] private float radius = 0.8f;  // Größe des Quad-Outline
    [SerializeField] private int segments = 64;     // Auflösung der Outline
    [SerializeField] private Color normalColor = new Color(0f, 1f, 1f, 0.9f);    // Cyan
    [SerializeField] private Color warningColor = new Color(1f, 0.2f, 0f, 0.9f);  // Rot

    private LineRenderer lr;
    private Coroutine countdown;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.startColor = normalColor;
        lr.endColor = normalColor;
        lr.positionCount = 0;
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
            float progress = elapsed / duration;
            float remaining = 1f - progress;

            int filledSegments = Mathf.RoundToInt(segments * remaining);
            DrawQuadOutline(filledSegments, progress);

            yield return null;
        }

        lr.positionCount = 0;
        countdown = null;
    }

    private void DrawQuadOutline(int filledSegments, float progress)
    {
        if (filledSegments < 2)
        {
            lr.positionCount = 0;
            return;
        }

        lr.positionCount = filledSegments;

        for (int i = 0; i < filledSegments; i++)
        {
            float normalizedPos = i / (float)segments;
            Vector3 pos = GetQuadOutlinePoint(normalizedPos);
            lr.SetPosition(i, pos);
        }

        Color currentColor = Color.Lerp(normalColor, warningColor, progress);
        lr.startColor = currentColor;
        lr.endColor = currentColor;
    }

    private Vector3 GetQuadOutlinePoint(float t)
    {
        t = t % 1f;

        if (t < 0.25f)
        {
            float x = Mathf.Lerp(-radius, radius, t / 0.25f);
            return new Vector3(x, radius, 0f);
        }
        else if (t < 0.5f)
        {
            float y = Mathf.Lerp(radius, -radius, (t - 0.25f) / 0.25f);
            return new Vector3(radius, y, 0f);
        }
        else if (t < 0.75f)
        {
            float x = Mathf.Lerp(radius, -radius, (t - 0.5f) / 0.25f);
            return new Vector3(x, -radius, 0f);
        }
        else
        {
            float y = Mathf.Lerp(-radius, radius, (t - 0.75f) / 0.25f);
            return new Vector3(-radius, y, 0f);
        }
    }
}
