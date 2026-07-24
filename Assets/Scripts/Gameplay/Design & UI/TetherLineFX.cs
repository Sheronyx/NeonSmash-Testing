using UnityEngine;

// Elektromagnetfeld-artige Verbindung zwischen zwei Objekten: ein LineRenderer, dessen Zwischenpunkte
// jeden Frame per Perlin-Noise seitlich zur Verbindungslinie zittern (organisches Wellenzittern statt
// reinem Frame-zu-Frame-Zufallsrauschen), während beide Enden exakt an pointA/pointB "haften" bleiben
// und ihnen live folgen, auch wenn sich die Objekte bewegen. Material/Farbe kommt vom LineRenderer
// selbst (z.B. ein additives Glow-/Elektro-Material).
[RequireComponent(typeof(LineRenderer))]
public class TetherLineFX : MonoBehaviour
{
    [Header("Anker")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Segmente / Zittern")]
    [Tooltip("Wie viele Zwischenpunkte die Linie hat (mehr = weicherer Verlauf, aber mehr Rechenaufwand).")]
    [SerializeField] private int segmentCount = 12;
    [Tooltip("Wie weit die Punkte maximal seitlich zur Verbindungslinie ausschlagen.")]
    [SerializeField] private float jitterAmount = 0.15f;
    [Tooltip("Wie schnell sich das Zittern-Muster über die Zeit verändert.")]
    [SerializeField] private float jitterSpeed = 8f;
    [Tooltip("Skaliert die Frequenz des Noise-Musters entlang der Linie (höher = mehr kleine Wellen statt eines großen Bogens).")]
    [SerializeField] private float noiseFrequency = 2f;

    private LineRenderer lr;
    private float seed;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        seed = Random.Range(0f, 1000f); // pro Instanz ein eigenes Zitter-Muster
    }

    private void Update()
    {
        if (pointA == null || pointB == null)
        {
            if (lr.positionCount != 0) lr.positionCount = 0;
            return;
        }

        if (lr.positionCount != segmentCount) lr.positionCount = segmentCount;

        Vector3 a = pointA.position;
        Vector3 b = pointB.position;
        Vector3 dir = b - a;
        float length = dir.magnitude;

        if (length < 0.0001f)
        {
            for (int i = 0; i < segmentCount; i++) lr.SetPosition(i, a);
            return;
        }

        Vector3 forward = dir / length;
        Vector3 perpendicular = Vector3.Cross(forward, Vector3.forward);
        if (perpendicular.sqrMagnitude < 0.0001f) perpendicular = Vector3.Cross(forward, Vector3.up);
        perpendicular.Normalize();

        float time = Time.time * jitterSpeed;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 basePos = Vector3.Lerp(a, b, t);

            // Klingt an beiden Enden (t=0/t=1) auf 0 ab, damit die Linie wirklich AN den Punkten
            // haftet, statt daneben zu enden.
            float edgeFade = Mathf.Sin(t * Mathf.PI);

            float noise = (Mathf.PerlinNoise(seed + t * noiseFrequency, time) - 0.5f) * 2f;
            Vector3 offset = perpendicular * (noise * jitterAmount * edgeFade);

            lr.SetPosition(i, basePos + offset);
        }
    }

    public void SetPoints(Transform a, Transform b)
    {
        pointA = a;
        pointB = b;
    }
}
