using System.Collections;
using UnityEngine;

// 3D-Ersatz für die alte 2D-Partikel-Explosion: die Kind-"Brocken" (kleine Fels-Meshes, gleiches
// Comic-Material + schwarze Outline-Shell wie die Tap-/Swipe-Elemente) fliegen beim Spawnen
// auseinander, drehen sich und schrumpfen gegen Ende auf 0 — dann räumt sich das ganze Prefab selbst
// auf. Wird wie das alte explodeVFXPrefab einfach per Instantiate(...) an der Trefferposition erzeugt
// (siehe BasePoint.SpawnExplosion) — kein ParticleSystem nötig, daher zerstört sich dieses Skript
// sich selbst statt sich auf Partikel-Laufzeiten zu verlassen.
public class ElementChunkExplosion : MonoBehaviour
{
    [Tooltip("Wie lange die Brocken insgesamt sichtbar sind, bevor sie verschwinden.")]
    [SerializeField] private float duration = 0.4f;
    [Tooltip("Konstante Fluggeschwindigkeit der Brocken nach außen (Welteinheiten/Sek) — bleibt über die " +
             "GESAMTE Dauer gleich (kein Abbremsen, kein Beschleunigen), damit der Flugweg absolut " +
             "geradlinig bleibt statt weich auszulaufen.")]
    [SerializeField] private Vector2 speedRange = new Vector2(2.2f, 3.2f);
    [Tooltip("Rotationsgeschwindigkeit der Brocken um die EIGENE Achse (Grad/Sek) — rein kosmetisch, hat " +
             "keinerlei Einfluss auf die (geradlinige) Flugrichtung.")]
    [SerializeField] private float spinSpeedDegrees = 720f;
    [Tooltip("Anteil der Gesamtdauer, ab dem die Brocken anfangen zu schrumpfen (0.75 = nur die letzten " +
             "25% der Zeit) — die Brocken bleiben also die meiste Zeit in voller Größe sichtbar und " +
             "verschwinden erst ganz am Ende schnell, statt die ganze Zeit langsam wegzufaden.")]
    [Range(0.1f, 0.95f)]
    [SerializeField] private float shrinkStart = 0.3f;

    private void Start()
    {
        StartCoroutine(Co_Explode());
    }

    private IEnumerator Co_Explode()
    {
        int count = transform.childCount;
        var chunks       = new Transform[count];
        var startDir     = new Vector3[count];
        var startSpeed   = new float[count];
        var spinAxis     = new Vector3[count];
        var spinRot      = new Quaternion[count];
        var baseRot      = new Quaternion[count];
        var startScale   = new Vector3[count];
        // Optischer Mesh-Mittelpunkt relativ zum Pivot (lokal) — die Rock-Meshes haben KEINEN
        // zentrierten Pivot (bei manchen sitzt er fast am Rand des Steins). Rotiert man einfach um den
        // Pivot, schwingt der sichtbare Stein dabei in einem Kreis mit — das sah wie eine weiche Kurve
        // aus, obwohl der Pivot selbst schnurgerade fliegt. Fix: wir rechnen die Position so um, dass
        // der SICHTBARE Mittelpunkt (nicht der Pivot) exakt der geraden Linie folgt.
        var localVisualCenter = new Vector3[count];

        Vector3 center = transform.position;

        for (int i = 0; i < count; i++)
        {
            var chunk = transform.GetChild(i);
            chunks[i] = chunk;
            startScale[i] = chunk.localScale;
            baseRot[i] = chunk.rotation;
            spinRot[i] = Quaternion.identity;

            var mf = chunk.GetComponent<MeshFilter>();
            localVisualCenter[i] = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;

            // Rein zufällige Richtung nach außen (voller Kreis, etwas Streuung in Z). Position wird
            // unten JEDEN Frame direkt aus center + startDir * startSpeed * t neu berechnet (kein
            // aufakkumuliertes += mit sich änderndem Vektor) — das garantiert einen absolut geraden
            // Strahl ohne jede Kurve.
            float angle = Random.value * Mathf.PI * 2f;
            startDir[i]   = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Random.Range(-0.3f, 0.3f)).normalized;
            startSpeed[i] = Random.Range(speedRange.x, speedRange.y);
            spinAxis[i]   = Random.onUnitSphere;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);

            for (int i = 0; i < count; i++)
            {
                if (chunks[i] == null) continue;

                if (p > shrinkStart)
                {
                    float shrinkP = (p - shrinkStart) / (1f - shrinkStart);
                    chunks[i].localScale = Vector3.Lerp(startScale[i], Vector3.zero, shrinkP);
                }

                // Eigenrotation aufakkumulieren (unabhängig von der Ausgangsrotation des Meshes).
                spinRot[i] = Quaternion.AngleAxis(spinSpeedDegrees * Time.deltaTime, spinAxis[i]) * spinRot[i];
                chunks[i].rotation = spinRot[i] * baseRot[i];

                // Zielpunkt für den SICHTBAREN Mittelpunkt: schnurgerade Linie ab center.
                Vector3 visualCenterTarget = center + startDir[i] * startSpeed[i] * t;

                // Weltraum-Versatz zwischen Pivot und sichtbarem Mittelpunkt bei aktueller Rotation/Skalierung.
                Vector3 worldOffset = chunks[i].rotation * Vector3.Scale(localVisualCenter[i], chunks[i].localScale);

                // Pivot so setzen, dass der sichtbare Mittelpunkt exakt auf der geraden Linie liegt.
                chunks[i].position = visualCenterTarget - worldOffset;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
