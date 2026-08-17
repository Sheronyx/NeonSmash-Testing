using UnityEngine;

// Lässt den Augen-Gegner oben im Screen sanft schweben — Position via Perlin-Noise, dasselbe bewährte
// Prinzip wie MagneticFragmentFloat (lokale Zeit + Noise-Referenzwert bei "Zeit 0" statt globaler
// Time.time, damit die Schwankung exakt bei der Ausgangsposition beginnt, kein Sprung im ersten Frame).
//
// Die Rotation ist NICHT unabhängig — sie richtet sich immer nach der AKTUELLEN Position aus: die
// untere Spitze zeigt immer Richtung lookAtTarget (z.B. Bildschirmmitte/Spielbereich), wie ein
// Kompass. Schwebt das Auge gerade nach links, kippt es nach rechts-unten (zeigt zurück zur Mitte),
// nach rechts geschwebt kippt es nach links-unten — wirkt dadurch, als würde es die ganze Zeit
// Richtung Mitte unten schauen, egal wo es gerade im Schweben steht.
public class EyeFloat : MonoBehaviour
{
    [Header("Position (Schweben)")]
    [SerializeField] private float moveRadius = 12f;
    [SerializeField] private float moveSpeed  = 0.3f;

    [Header("Blickrichtung")]
    [Tooltip("Wohin die untere Spitze immer zeigen soll (z.B. ein leeres GameObject in der Bildschirm-/" +
             "Spielbereich-Mitte). Leer gelassen = einfach gerade unterhalb der Ausgangsposition.")]
    [SerializeField] private Transform lookAtTarget;

    private Vector3 basePos;

    private float seedX, seedY;
    private float startTime;
    private float zeroX, zeroY;

    private void Start()
    {
        basePos = transform.position;

        seedX = Random.Range(0f, 1000f);
        seedY = Random.Range(1000f, 2000f);

        startTime = Time.time;
        zeroX     = Mathf.PerlinNoise(seedX, 0f);
        zeroY     = Mathf.PerlinNoise(seedY, 0f);
    }

    private void Update()
    {
        float t = Time.time - startTime;

        float offsetX = (Mathf.PerlinNoise(seedX, t * moveSpeed) - zeroX) * 2f * moveRadius;
        float offsetY = (Mathf.PerlinNoise(seedY, t * moveSpeed) - zeroY) * 2f * moveRadius;
        Vector3 newPos = basePos + new Vector3(offsetX, offsetY, 0f);
        transform.position = newPos;

        Vector3 targetPos = lookAtTarget != null ? lookAtTarget.position : basePos + Vector3.down * 100f;

        Vector2 dir = targetPos - newPos;
        if (dir.sqrMagnitude > 0.0001f)
        {
            // Ausgangsrotation 0° wird als "Spitze zeigt nach unten" angenommen — dieselbe Formel wie
            // eine Blickrichtung, nur um 90° verschoben, weil "unten" (0,-1) statt "rechts" (1,0) der
            // Referenzvektor bei Rotation 0 ist.
            float angle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
