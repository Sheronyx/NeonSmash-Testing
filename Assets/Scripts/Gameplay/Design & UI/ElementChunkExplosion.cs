using System.Collections;
using UnityEngine;

// 3D-Ersatz für die alte 2D-Partikel-Explosion: die Kind-"Brocken" (kleine Fels-Meshes, gleiches
// Comic-Material + schwarze Outline-Shell wie die Tap-/Swipe-Elemente) fliegen beim Spawnen
// auseinander, drehen sich und schrumpfen gegen Ende auf 0 — dann räumt sich das ganze Prefab selbst
// auf. Wird wie das alte explodeVFXPrefab einfach per Instantiate(...) an der Trefferposition erzeugt
// (siehe BasePoint.SpawnExplosion). Optional kann ein zusätzliches leuchtendes Glow-Partikelsystem als
// Kind-Objekt angehängt werden (z.B. "Energy Explosion Tap/Swipe Element <Farbe>") — jedes
// ParticleSystem, das NICHT selbst ein Brocken-Mesh trägt, wird beim Start einfach abgespielt und
// zählt nicht zu den fliegenden/rotierenden/schrumpfenden Brocken. Dieses Skript verwaltet seinen
// eigenen Lebenszyklus komplett selbst (siehe BasePoint.SpawnExplosion: kein Destroy(fx, dur) von
// außen, wenn eine ElementChunkExplosion-Komponente vorhanden ist).
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
        // Optionales Glow-Partikelsystem (z.B. "Energy Explosion Tap/Swipe Element <Farbe>") einfach
        // abspielen — läuft komplett unabhängig von den Brocken weiter (eigenes Simulationsraum/-timing).
        var glowSystems = GetComponentsInChildren<ParticleSystem>(true);
        float maxParticleLifetime = 0f;
        foreach (var ps in glowSystems)
        {
            ps.Play();
            var main = ps.main;
            maxParticleLifetime = Mathf.Max(maxParticleLifetime, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        StartCoroutine(Co_Explode(Mathf.Max(duration, maxParticleLifetime)));
    }

    private IEnumerator Co_Explode(float totalLifetime)
    {
        // Nur echte Brocken (mit eigenem Mesh) fliegen/rotieren/schrumpfen — ein evtl. angehängtes
        // Glow-Partikelsystem (kein MeshFilter) bleibt davon unberührt und läuft einfach für sich.
        var chunkList = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.GetComponent<MeshFilter>() != null)
                chunkList.Add(child);
        }

        int count = chunkList.Count;
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
            var chunk = chunkList[i];
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

        // Falls ein angehängtes Glow-Partikelsystem länger läuft als die Brocken-Animation, hier noch
        // warten, statt es mitten in seiner Lebenszeit abzuwürgen.
        float remaining = totalLifetime - duration;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        Destroy(gameObject);
    }
}
