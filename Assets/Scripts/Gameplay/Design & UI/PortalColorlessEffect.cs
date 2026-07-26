using System.Collections;
using UnityEngine;

// Dirigent des Colorless-Ankündigungseffekts, sitzt direkt auf dem Portal-Objekt selbst:
// 1) Portal materialisiert von klein auf Originalgröße. PARALLEL fliegen beliebig viele bunte
//    Steinstücke (siehe ColorlessFlyInPiece) von rechts auf einer Sweep-Kurve gegen den Uhrzeigersinn
//    rein und werden am Ende eingesaugt (echter Sog: Radius bleibt lange fast konstant, bricht erst
//    gegen Ende schnell zusammen — siehe angleEaseExponent/radiusEaseExponent).
// 2) Sobald alle Steine da sind (deterministisches Timing, kein Callback-Tracking nötig, da alle
//    gleichzeitig starten und maximal startDelayVariance auseinanderliegen): Portal macht einen kurzen
//    Quetsch-Effekt, PARALLEL schleudern graue Ersatzstücke (siehe ColorlessBurstPiece) alle vom selben
//    Punkt (dem Portal) mit EXAKT DERSELBEN Kurven-Formel wie der Einflug — nur zeitlich/radial
//    umgekehrt (Radius wächst statt schrumpft) — in beliebige Richtungen raus, garantiert über den
//    Bildschirmrand hinaus.
// 3) Portal fadet langsam weg. TotalDuration wartet auf BEIDES — Portal komplett verschwunden UND Burst-
//    Stücke außerhalb des sichtbaren Bereichs — bevor der Spawner freigegeben wird.
public class PortalColorlessEffect : MonoBehaviour
{
    [Header("Portal Erscheinen")]
    [SerializeField] private float spawnInDuration = 0.4f;

    [Header("Steinstücke (Einflug) — dieselbe Kurve gilt gespiegelt auch für den Burst")]
    [Tooltip("Startbereich relativ zum Portal — X positiv = rechts.")]
    [SerializeField] private Vector2 spawnAreaOffset = new Vector2(3.5f, 0f);
    [Tooltip("Größe des rechteckigen Streubereichs für die Startpunkte (X/Y Halbausdehnung) — schmal in " +
             "einer Achse ergibt eine Linie hintereinander statt einer breiten Fläche. Default: schmales " +
             "Y (enge Spur), breiteres X (unterschiedlich weit weg = hintereinander gestaffelt).")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(9f, 2f);
    [SerializeField] private float flyInDuration = 0.9f;
    [Tooltip("Zufällige Extra-Verzögerung pro Stück, damit nicht alle exakt gleichzeitig starten.")]
    [SerializeField] private float startDelayVariance = 0.9f;
    [Tooltip("Gesamter Schwenkwinkel gegen den Uhrzeigersinn in Grad, während die Steine einfliegen — " +
             "größer = größere Runde ums Portal statt fast direktem Reinflug.")]
    [SerializeField] private float sweepDegrees = 320f;
    [Tooltip("Zufällige Streuung auf sweepDegrees pro Stück (+/-), damit nicht alle exakt denselben " +
             "Bogen parallel abfliegen.")]
    [SerializeField] private float sweepDegreesVariance = 60f;
    [Tooltip("1 = Winkel läuft linear, >1 = Kurve setzt später/kräftiger ein (konzentriert sich dann " +
             "aufs Ende, wenn der Radius schon klein ist).")]
    [SerializeField] private float angleEaseExponent = 1.2f;
    [Tooltip("1 = Radius ändert sich linear, >1 = bleibt lange fast konstant und bricht erst gegen Ende " +
             "schnell zusammen (Einflug) bzw. schießt erst gegen Ende schnell raus (Burst) — DAS ist der " +
             "eigentliche Sog-/Schleuder-Effekt (2.5-4 = deutlich spätes, schnelles Rein-/Rausziehen).")]
    [SerializeField] private float radiusEaseExponent = 5f;

    [Header("Quetsch-Effekt (sobald alle Steine eingesogen sind)")]
    [SerializeField] private float squishDuration = 0.25f;
    [SerializeField] private Vector2 squishScale = new Vector2(1.3f, 0.7f);

    [Header("Portal Verschwinden")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Farblos-Burst (parallel zum Quetsch-Effekt) — nutzt dieselbe Kurve wie oben, gespiegelt")]
    [Tooltip("Ein oder mehrere farblose Varianten-Prefabs — pro Burst-Stück wird zufällig eins davon " +
             "gewählt. Leer lassen, um den Burst zu überspringen.")]
    [SerializeField] private GameObject[] colorlessPiecePrefabs;
    [SerializeField] private int burstCount = 6;
    [Tooltip("Eigene Flugdauer für den Burst (statt flyInDuration) — der Burst legt eine viel größere " +
             "Strecke zurück (bis außerhalb des Bildschirms statt nur bis zum Portal), braucht bei " +
             "gleichem Tempogefühl also mehr Zeit. Höher = langsamer.")]
    [SerializeField] private float burstDuration = 1.6f;
    [Tooltip("Wie schnell ein Stück beim Start von 0 auf volle Größe aufpoppt.")]
    [SerializeField] private float burstPopInDuration = 0.15f;
    [SerializeField] private float burstRotationSpeed = 180f;
    [Tooltip("Zusätzliche Distanz über den Bildschirmrand hinaus (Weltunits) — garantiert, dass die " +
             "Stücke wirklich außerhalb des sichtbaren Bereichs landen, nicht nur knapp am Rand.")]
    [SerializeField] private float burstExtraDistance = 1.5f;

    private Vector3 fullScale;

    private bool HasBurst => colorlessPiecePrefabs != null && colorlessPiecePrefabs.Length > 0;

    /// <summary>Gesamtlaufzeit bis das Portal komplett verschwunden UND die Burst-Stücke außerhalb des
    /// sichtbaren Bereichs sind — MysteryBoxEffectSystem wartet darauf, bevor der Spawner wieder
    /// freigegeben wird.</summary>
    public float TotalDuration =>
        Mathf.Max(spawnInDuration, flyInDuration + startDelayVariance) +
        Mathf.Max(squishDuration + fadeOutDuration, HasBurst ? burstDuration : 0f);

    public void Play()
    {
        fullScale = transform.localScale;
        transform.localScale = Vector3.zero;

        Vector3 spawnCenter = transform.position + (Vector3)spawnAreaOffset;
        var pieces = GetComponentsInChildren<ColorlessFlyInPiece>(true);
        foreach (var piece in pieces)
        {
            Vector2 rnd = new Vector2(Random.Range(-spawnAreaSize.x, spawnAreaSize.x), Random.Range(-spawnAreaSize.y, spawnAreaSize.y));
            Vector3 spawnPos = spawnCenter + new Vector3(rnd.x, rnd.y, 0f);
            float delay = Random.Range(0f, startDelayVariance);
            float sweep = sweepDegrees + Random.Range(-sweepDegreesVariance, sweepDegreesVariance);
            piece.FlyIn(spawnPos, transform, flyInDuration, delay, sweep, angleEaseExponent, radiusEaseExponent);
        }

        StartCoroutine(Co_ScaleTo(Vector3.zero, fullScale, spawnInDuration));

        float readyAt = Mathf.Max(spawnInDuration, flyInDuration + startDelayVariance);
        Invoke(nameof(SquishBurstAndFade), readyAt);
    }

    private void SquishBurstAndFade()
    {
        StartCoroutine(Co_SquishThenFade());
        if (HasBurst) SpawnBurst();
    }

    private IEnumerator Co_SquishThenFade()
    {
        Vector3 squished = new Vector3(fullScale.x * squishScale.x, fullScale.y * squishScale.y, fullScale.z);
        float half = squishDuration * 0.5f;
        yield return Co_ScaleTo(fullScale, squished, half);
        yield return Co_ScaleTo(squished, fullScale, half);
        yield return Co_ScaleTo(fullScale, Vector3.zero, fadeOutDuration);
    }

    private IEnumerator Co_ScaleTo(Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        transform.localScale = to;
    }

    private void SpawnBurst()
    {
        float maxDistance = ComputeOffScreenEscapeDistance() + burstExtraDistance;

        for (int i = 0; i < burstCount; i++)
        {
            var prefab = colorlessPiecePrefabs[Random.Range(0, colorlessPiecePrefabs.Length)];
            if (prefab == null) continue;

            float initialAngle = Random.Range(0f, 360f);
            float sweep = sweepDegrees + Random.Range(-sweepDegreesVariance, sweepDegreesVariance);

            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            var burstPiece = go.GetComponent<ColorlessBurstPiece>();
            if (burstPiece == null) burstPiece = go.AddComponent<ColorlessBurstPiece>();
            burstPiece.Play(initialAngle, sweep, angleEaseExponent, radiusEaseExponent,
                             burstDuration, maxDistance, burstRotationSpeed, burstPopInDuration, transform);
        }
    }

    // Halbe Bildschirmdiagonale in Weltunits ab dem Portal — als radiale Distanz in JEDE Richtung
    // ausreichend, um garantiert über den sichtbaren Bereich hinauszukommen (konservative Annahme,
    // da das Portal i.d.R. nahe der Bildschirmmitte liegt).
    private float ComputeOffScreenEscapeDistance()
    {
        Camera cam = Camera.main;
        if (cam == null) return burstExtraDistance + 5f;

        float z = Mathf.Abs(cam.transform.position.z);
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector3 topRight   = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));
        return Vector3.Distance(bottomLeft, topRight) * 0.5f;
    }
}
