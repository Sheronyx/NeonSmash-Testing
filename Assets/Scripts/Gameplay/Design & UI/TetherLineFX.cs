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

    [Header("Tiefe (Sorting)")]
    [Tooltip("Überschreibt die Z-Tiefe ALLER Linienpunkte fest auf diesen Wert — pointA/pointB liefern " +
             "wegen useWorldSpace sonst live ihre eigene Z-Position, die des Tether-Objekts selbst wird " +
             "dabei komplett ignoriert. Über dieses Feld bekommst du echte, wirksame Kontrolle.")]
    [SerializeField] private float zOverride = 0f;

    [Tooltip("Render Queue wird zur Laufzeit hart auf diesen Wert gesetzt — Outline-Shader setzen ihre " +
             "eigene Queue oft bewusst hoch (zeichnen absichtlich zuletzt/oben), was Sorting Layer/Order " +
             "in Layer komplett übergehen kann. Muss niedriger sein als die Queue vom Fairy Energy Orb " +
             "(laut Inspector 3000), damit die Linie garantiert dahinter gezeichnet wird.")]
    [SerializeField] private int forceRenderQueue = 2999;

    private LineRenderer lr;
    private float seed;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        seed = Random.Range(0f, 1000f); // pro Instanz ein eigenes Zitter-Muster

        Debug.Log($"[TetherLineFX] {name}: Material-Render-Queue VOR Override = {lr.material.renderQueue}");
        lr.material.renderQueue = forceRenderQueue;
        Debug.Log($"[TetherLineFX] {name}: Material-Render-Queue NACH Override = {lr.material.renderQueue}");
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

            Vector3 finalPos = basePos + offset;
            finalPos.z = zOverride;
            lr.SetPosition(i, finalPos);
        }
    }

    public void SetPoints(Transform a, Transform b)
    {
        pointA = a;
        pointB = b;
    }
}
