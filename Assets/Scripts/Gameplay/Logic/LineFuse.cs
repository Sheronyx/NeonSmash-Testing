using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class LineFuse : MonoBehaviour
{
    [SerializeField] private float lineLength = 1.6f;  // Länge der Linie (z.B. width des InnerRect)
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color fuseColor = new Color(1f, 0.2f, 0.8f, 1f);
    [SerializeField] private bool isHorizontal = true;  // Horizontal oder Vertikal

    private LineRenderer lr;
    private Coroutine burnRoutine;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = fuseColor;
        lr.endColor = fuseColor;

        // Volle (unverbrannte) Zündschnur sofort zeichnen — sichtbar von Anfang an (auch während
        // PointFlyIn noch einfliegt), das eigentliche "Abbrennen" beginnt erst mit StartBurn().
        DrawLine(1f);
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

            DrawLine(remaining);

            yield return null;
        }

        lr.positionCount = 0;
        burnRoutine = null;
    }

    private void DrawLine(float remaining)
    {
        if (remaining < 0.01f)
        {
            lr.positionCount = 0;
            return;
        }

        lr.positionCount = 2;

        if (isHorizontal)
        {
            // Horizontal: von beiden Seiten zur Mitte abbrennen
            float distance = (lineLength * 0.5f) * remaining;
            lr.SetPosition(0, new Vector3(-distance, 0, 0));
            lr.SetPosition(1, new Vector3(distance, 0, 0));
        }
        else
        {
            // Vertikal: von beiden Enden zur Mitte abbrennen
            float distance = (lineLength * 0.5f) * remaining;
            Vector3 top = new Vector3(0, distance, 0);
            Vector3 bottom = new Vector3(0, -distance, 0);
            lr.SetPosition(0, top);
            lr.SetPosition(1, bottom);
        }
    }
}
