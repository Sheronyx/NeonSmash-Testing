using UnityEngine;

// Bewegt die IK-Ziele der Beine (Fuß-Position) kontinuierlich und asynchron zwischen den Beinen sanft
// nach oben -- nie stillstehend, wie ein leichtes, durchgehendes Wippen im Stand. Der 2D-IK-Solver
// (Limb Solver 2D, siehe IK Manager 2D in der Szene) berechnet daraus automatisch plausible Thigh/Calve-
// Winkel -- kein manuelles Gelenk-Rechnen mehr nötig, nur noch eine Zielposition sanft verschieben.
public class FairyLegIKSway : MonoBehaviour
{
    [System.Serializable]
    public class LegTarget
    {
        [Tooltip("Das IK-Ziel-Objekt für dieses Bein (z.B. LeftFoot_Target).")]
        public Transform target;

        [Tooltip("Wie weit das Ziel maximal Richtung Hüfte gezogen wird (lokale Einheiten) -- die " +
                 "Ruheposition (0) ist das gestreckte Bein, dieser Wert steuert NUR, wie stark das Knie " +
                 "dabei zusätzlich einknickt/hochgezogen wird. Größer = stärkere Beugung, NICHT stärkeres Strecken.")]
        public float kneeLiftAmount = 0.08f;
        [Tooltip("Mindest-Anteil von kneeLiftAmount, der IMMER angewendet bleibt (0-1) -- verhindert, dass " +
                 "das Bein je komplett voll durchstreckt. Nahe voller Streckung sieht eine kleine " +
                 "Winkeländerung optisch kaum anders aus (eine fast gerade Linie bleibt für das Auge " +
                 "'gerade'), auch wenn sich die Zahlen dahinter stetig ändern -- das liest sich als " +
                 "Stehenbleiben. Mit einer Mindest-Beugung bewegt sich das Bein immer in einem Bereich, wo " +
                 "die Änderung auch sichtbar ist.")]
        [Range(0f, 0.9f)] public float minBendFraction = 0.35f;
        [Tooltip("Dauer eines vollen Sway-Zyklus in Sekunden.")]
        public float period = 3f;
        [Tooltip("Start-Phase (0-1) -- rechtes und linkes Bein sollten unterschiedliche Werte haben, " +
                 "damit sie nicht synchron pendeln (z.B. 0 und 0.5 für gegenläufig).")]
        [Range(0f, 1f)] public float phaseOffset = 0f;

        [System.NonSerialized] public Vector3 restLocalPos;
        [System.NonSerialized] public bool     restCaptured;
        [System.NonSerialized] public float    noiseSeed;
    }

    [SerializeField] private LegTarget leftLeg  = new LegTarget { phaseOffset = 0f };
    [SerializeField] private LegTarget rightLeg = new LegTarget { phaseOffset = 0.5f };

    [Tooltip("Wie stark die Periode pro Bein leicht zufällig driftet (Anteil von 'period') -- verhindert " +
             "einen mechanisch wirkenden, exakt identischen Loop. 0 = kein Drift.")]
    [Range(0f, 0.3f)] [SerializeField] private float periodVariance = 0.15f;
    [Tooltip("Wie schnell die Perioden-Drift-Noise-Spur wandert.")]
    [SerializeField] private float driftRate = 0.15f;
    [Tooltip("Wie stark die Umkehrpunkte (ganz gestreckt / am weitesten gebeugt) abgerundet werden " +
             "(Anteil des Zyklus, 0-0.49) -- klein halten für fast durchgehend konstante Geschwindigkeit " +
             "(kein spürbares 'Stehenbleiben' an den Enden), 0.49 käme einem reinen Sinus nahe (mehr " +
             "Verweilen an den Extremen).")]
    [Range(0f, 0.49f)] [SerializeField] private float cornerRoundness = 0.02f;

    private float timer;

    private void Awake()
    {
        leftLeg.noiseSeed  = Random.Range(0f, 1000f);
        rightLeg.noiseSeed = Random.Range(0f, 1000f);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        ApplyLeg(leftLeg);
        ApplyLeg(rightLeg);
    }

    private void ApplyLeg(LegTarget leg)
    {
        if (leg.target == null) return;

        // Ruhe-Position EINMALIG sichern -- die im Editor gesetzte Platzierung (dort, wo der Fuß
        // natürlich stehen soll) ist die Basis, um die herum sanft geschwankt wird.
        if (!leg.restCaptured)
        {
            leg.restLocalPos = leg.target.localPosition;
            leg.restCaptured = true;
        }

        // Sanfte Perioden-Drift, damit sich der Loop nicht exakt wiederholt.
        float noiseInput = Mathf.Repeat(timer, 250f) * driftRate;
        float driftT = Mathf.PerlinNoise(noiseInput, leg.noiseSeed);
        float effectivePeriod = leg.period * (1f + (driftT * 2f - 1f) * periodVariance);

        // Abgerundete Dreieckswelle statt reinem Sinus: reiner Sinus hat an den Umkehrpunkten (ganz
        // gestreckt / ganz gebeugt) naturgemäß eine Geschwindigkeit nahe 0 über einen spürbaren Teil des
        // Zyklus -- das liest sich wie "kurz stehenbleiben" statt durchgehend zu tanzen. Die abgerundete
        // Dreieckswelle bewegt sich über den GROSSTEIL des Zyklus mit fast konstanter Geschwindigkeit und
        // rundet nur ganz kurz an den beiden Enden ab (kein harter Richtungswechsel, aber auch kein langes
        // Verweilen).
        float cycle = timer / Mathf.Max(0.0001f, effectivePeriod) + leg.phaseOffset;
        float rawT  = Mathf.PingPong(cycle * 2f, 1f); // 0..1..0, konstante Steigung
        float rounded = RoundTriangleCorner(rawT, cornerRoundness); // 0..1, an den Enden weich abgerundet

        // Nie unter minBendFraction fallen -- hält das Bein permanent in einem Bereich, wo Beugung
        // optisch sichtbar bleibt, statt je die (optisch "eingefroren" wirkende) volle Streckung zu erreichen.
        float t = Mathf.Lerp(leg.minBendFraction, 1f, rounded);

        Vector3 offset = new Vector3(0f, leg.kneeLiftAmount * t, 0f);
        leg.target.localPosition = leg.restLocalPos + offset;
    }

    // Rundet nur die Ecken (k=0 und k=1) einer 0..1-Dreieckswelle sanft ab -- rein positionsbasiert
    // (state-los, kein Gedächtnis zwischen Frames, kann daher nicht nachfedern/überschießen). Am Rand ist
    // die Steigung exakt 0 (kein ruckartiger Richtungswechsel), am Übergang zur linearen Mitte stimmt die
    // Steigung exakt mit der Geraden überein (kein sichtbarer Knick).
    private static float RoundTriangleCorner(float k, float cornerFraction)
    {
        if (cornerFraction <= 0.0001f) return k;
        cornerFraction = Mathf.Min(cornerFraction, 0.49f);

        if (k < cornerFraction)
        {
            float local = k / cornerFraction;
            return cornerFraction * (local * local * (2f - local));
        }
        if (k > 1f - cornerFraction)
        {
            float local = (1f - k) / cornerFraction;
            return 1f - cornerFraction * (local * local * (2f - local));
        }
        return k;
    }
}
