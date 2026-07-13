using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class FuseCountdown : MonoBehaviour
{
    [SerializeField] private float radius = 0.75f;
    [SerializeField] private int segments = 120;
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private Color fuseColor = new Color(1f, 0.2f, 0.8f, 1f);

    private LineRenderer lr;
    private Coroutine burnRoutine;
    private Vector3[] quadOutlinePoints;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = fuseColor;
        lr.endColor = fuseColor;

        GenerateQuadOutline();

        // Volle (unverbrannte) Zündschnur sofort zeichnen — sichtbar von Anfang an (auch während
        // PointFlyIn noch einfliegt), das eigentliche "Abbrennen" beginnt erst mit StartBurn().
        DrawFuse(segments);
    }

    private void GenerateQuadOutline()
    {
        quadOutlinePoints = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            quadOutlinePoints[i] = GetQuadPoint(t);
        }
    }

    public void StartBurn(float duration)
    {
        if (burnRoutine != null) StopCoroutine(burnRoutine);
        burnRoutine = StartCoroutine(Co_Burn(duration));
    }

    private IEnumerator Co_Burn(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - (elapsed / duration);
            int visibleSegments = Mathf.RoundToInt(segments * remaining);
            DrawFuse(visibleSegments);
            yield return null;
        }
        lr.positionCount = 0;
        burnRoutine = null;
    }

    private void DrawFuse(int visibleSegments)
    {
        if (visibleSegments < 2)
        {
            lr.positionCount = 0;
            return;
        }
        lr.positionCount = visibleSegments;
        for (int i = 0; i < visibleSegments; i++)
        {
            lr.SetPosition(i, quadOutlinePoints[i]);
        }
    }

    private Vector3 GetQuadPoint(float t)
    {
        t = t % 1f;
        float side = t * 4f;

        if (side < 1f)
        {
            float x = Mathf.Lerp(-radius, radius, side);
            return new Vector3(x, radius, 0f);
        }
        else if (side < 2f)
        {
            float y = Mathf.Lerp(radius, -radius, side - 1f);
            return new Vector3(radius, y, 0f);
        }
        else if (side < 3f)
        {
            float x = Mathf.Lerp(radius, -radius, side - 2f);
            return new Vector3(x, -radius, 0f);
        }
        else
        {
            float y = Mathf.Lerp(-radius, radius, side - 3f);
            return new Vector3(-radius, y, 0f);
        }
    }
}
