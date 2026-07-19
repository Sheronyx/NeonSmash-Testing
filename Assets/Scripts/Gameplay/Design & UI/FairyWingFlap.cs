using UnityEngine;

// Steuert das Flügel-Flattern der Fairy: jedes Flügel-Segment schwingt DURCHGEHEND (ohne an einer
// Position zu pausieren) zwischen seiner Ruheposition und EINEM Extrem (Rückschlag) hin und her —
// eine echte Bewegung zur Gegenseite gibt es bewusst nicht (Nutzer-Feedback: wirkte wie Falten nach
// vorne UND hinten zugleich). Eigener Geschwindigkeits-Multiplikator relativ zur Basis-Frequenz
// (kleinere Flügel schlagen realistischerweise schneller als große). Der Auftrieb für FairyFloat
// (LiftPhase) folgt weiterhin dem Basis-Takt (= den großen Flügeln), damit der Körper sichtbar zum
// Haupt-Flügelschlag hebt, nicht zum schnellen Geflatter der kleinen Flügel.
public class FairyWingFlap : MonoBehaviour
{
    [System.Serializable]
    public class Wing
    {
        public Transform transform;
        public float minAngle;
        public float maxAngle;
        public float restAngle;
        [Tooltip("Geschwindigkeit relativ zur Basis-Frequenz — 1 = im Haupttakt, höher = schlägt schneller " +
                 "(für kleinere/äußere Flügelsegmente).")]
        public float speedMultiplier = 1f;
        [Tooltip("Welches Extrem angeflogen wird: true = Max Angle, false = Min Angle. Der Flügel schwingt " +
                 "NUR zwischen Rest Angle und diesem einen Extrem hin und her, nie zur Gegenseite.")]
        public bool foldTowardMaxAngle = true;
        [Tooltip("Wie schmal der Flügel bei voller Auslenkung (am Extrem) wird (1 = kein Effekt, 0.5 = " +
                 "wird halb so breit). Simuliert Nach-hinten-Klappen — reine Rotation wirkt sonst nur wie " +
                 "ein flaches Kippen, kein echtes Falten in die Tiefe.")]
        [Range(0.2f, 1f)]
        public float foldScaleAtExtreme = 0.55f;
        [Tooltip("Falls der Flügel lokal 'liegend' ausgerichtet ist: hier auf die Y- statt X-Achse umschalten.")]
        public bool foldOnYAxis = false;

        [System.NonSerialized] public Vector3 baseScale;
    }

    [Header("Flatter-Tempo (Basis, große Flügel)")]
    [Tooltip("Erlaubter Bereich der Flatter-Frequenz in Hz (volle Hin-und-Zurück-Bewegung pro Sekunde). " +
             "Die tatsächliche Geschwindigkeit driftet sanft (per Perlin Noise) innerhalb dieses Bereichs " +
             "hin und her, statt fest auf einem Wert zu bleiben — verhindert den mechanisch wirkenden " +
             "\"exakt identischer Loop\"-Effekt und ist gleichzeitig deine Kontrolle gegen zu schnelles Flattern.")]
    [SerializeField] private float minFlapSpeed = 1.8f;
    [SerializeField] private float maxFlapSpeed = 2.6f;
    [Tooltip("Wie schnell die Geschwindigkeit innerhalb von Min/Max hin- und herdriftet (höher = schnellere " +
             "Tempowechsel, niedriger = trägere/langsamere Übergänge zwischen langsam und schnell).")]
    [SerializeField] private float speedDriftRate = 0.4f;
    [Tooltip("Zum Testen: Z-Rotation abschalten, um zu sehen, ob die Fold-Skalierung allein die " +
             "Flatter-Illusion trägt. Bei aus bleibt der Flügel auf seinem Rest-Winkel stehen.")]
    [SerializeField] private bool rotationEnabled = true;
    [Tooltip("Anteil der Zykluszeit für den schnellen Ausschlag zum Extrem (Kraftschlag) — der Rest der " +
             "Zeit ist der langsamere Rückschlag zurück zur Ruheposition. 0.5 = symmetrisch, kleiner = " +
             "knackigerer Kraftschlag mit weicherem Rückschlag (wirkt organischer als ein reiner Sinus).")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float strokePhaseFraction = 0.35f;
    [Tooltip("Wie stark der Ausschlag (nicht die Geschwindigkeit) zusätzlich von Schlag zu Schlag leicht " +
             "schwankt (0 = kein Effekt).")]
    [Range(0f, 1f)]
    [SerializeField] private float amplitudeVariation = 0.15f;

    [Header("Flügel-Segmente (Transform pro Eintrag im Editor zuweisen)")]
    [SerializeField] private Wing[] wings = new Wing[]
    {
        new Wing { minAngle = -20f, maxAngle = 0f,   restAngle = -12f, speedMultiplier = 1f    }, // Right Big Wing
        new Wing { minAngle = 0f,   maxAngle = 20f,  restAngle = 12f,  speedMultiplier = 1f    }, // Left Big Wing
        new Wing { minAngle = 10f,  maxAngle = 30f,  restAngle = 20f,  speedMultiplier = 1.6f  }, // Right Small Wing 1
        new Wing { minAngle = -30f, maxAngle = -10f, restAngle = -20f, speedMultiplier = 1.6f  }, // Left Small Wing 1
        new Wing { minAngle = -2f,  maxAngle = 2f,   restAngle = 0f,   speedMultiplier = 2.4f  }, // Right Small Wing 2
        new Wing { minAngle = -2f,  maxAngle = 2f,   restAngle = 0f,   speedMultiplier = 2.4f  }, // Left Small Wing 2
    };

    // Folgt der Basis-Frequenz, 0..1 (0 = Ruheposition, 1 = volles Extrem) — für FairyFloat.LiftPhase
    // in ein -1..1-Signal umgerechnet, damit der Körper weiterhin symmetrisch mit hebt/senkt.
    public float LiftPhase { get; private set; }

    private float timer;
    private float noiseSeed;

    private void Awake()
    {
        // Zufälliger Start-Offset, damit mehrere Fairies (gleiche Geschwindigkeit) nicht im
        // exakt gleichen Takt schlagen, sondern sichtbar versetzt zueinander.
        timer = UnityEngine.Random.Range(0f, 100f);
        // Eigener Seed pro Fairy, damit die Cycle-Variation (siehe Update) bei mehreren Fairies
        // nicht synchron "atmet", sondern jede für sich unregelmäßig wirkt.
        noiseSeed = UnityEngine.Random.Range(0f, 1000f);

        foreach (var wing in wings)
            if (wing.transform != null) wing.baseScale = wing.transform.localScale;
    }

    private float _debugLogTimer;

    private void Update()
    {
        timer += Time.deltaTime;

        // Aktuelle Flatter-Geschwindigkeit driftet sanft zwischen Min/Max (Perlin statt reinem Random,
        // damit sie sich stetig ändert statt zu springen) — verhindert den "perfekt identischer Loop"-
        // Effekt. WICHTIG: Mathf.PerlinNoise bekommt hier bewusst NICHT den unbegrenzt wachsenden
        // timer direkt, sondern einen auf 0..250 umgewickelten Wert — Unitys Perlin-Implementierung
        // driftet bei größeren Eingabewerten (schon nach einigen Minuten Spielzeit) zunehmend Richtung
        // 1, wodurch die Geschwindigkeit über die Zeit immer weiter Richtung maxFlapSpeed gewandert
        // wäre (Bug: "wird immer schneller"). Der Reset alle ~250s erzeugt höchstens einen winzigen,
        // kaum wahrnehmbaren Tempo-Sprung, verhindert aber die dauerhafte Drift zuverlässig.
        float noiseInput      = Mathf.Repeat(timer, 250f) * speedDriftRate;
        float noiseT          = Mathf.PerlinNoise(noiseInput, noiseSeed);
        float currentFlapSpeed = Mathf.Lerp(minFlapSpeed, maxFlapSpeed, noiseT);
        float ampJitter        = (noiseT * 2f - 1f) * amplitudeVariation; // -1..1, für die Ausschlag-Variation unten

        // TEMPORÄR zur Diagnose des "wird immer schneller"-Bugs — einmal pro Sekunde loggen.
        _debugLogTimer += Time.unscaledDeltaTime;
        if (_debugLogTimer >= 1f)
        {
            _debugLogTimer = 0f;
            Debug.Log($"[FairyWingFlap:{name}] timer={timer:F1} noiseInput={noiseInput:F1} noiseT={noiseT:F3} currentFlapSpeed={currentFlapSpeed:F3} timeScale={Time.timeScale:F2}");
        }

        float basePhase = timer * currentFlapSpeed * Mathf.PI * 2f;
        LiftPhase = Mathf.Sin(basePhase);

        foreach (var wing in wings)
        {
            if (wing.transform == null) continue;

            float cyclePos = timer * currentFlapSpeed * wing.speedMultiplier;
            float k = cyclePos - Mathf.Floor(cyclePos); // 0..1 innerhalb des aktuellen Zyklus, läuft durchgehend ohne Pause

            // Asymmetrische Schlag-Kurve statt symmetrischem Kosinus: schneller Ausschlag zum Extrem
            // (Kraftschlag), langsameres Zurückkehren (Rückschlag) — fühlt sich lebendiger an als eine
            // perfekt gleichförmige Hin-und-her-Bewegung. SmoothStep sorgt dafür, dass die Geschwindigkeit
            // an beiden Umkehrpunkten sanft auf 0 geht, kein Ruck beim Richtungswechsel.
            float t = k < strokePhaseFraction
                ? Mathf.SmoothStep(0f, 1f, k / strokePhaseFraction)
                : Mathf.SmoothStep(1f, 0f, (k - strokePhaseFraction) / (1f - strokePhaseFraction));

            // Leichte Ausschlag-Variation pro Zyklus, synchron zur Tempo-Drift oben.
            t = Mathf.Clamp01(t * (1f + ampJitter));

            float extremeAngle = wing.foldTowardMaxAngle ? wing.maxAngle : wing.minAngle;
            float angle        = Mathf.Lerp(wing.restAngle, extremeAngle, t);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, rotationEnabled ? angle : wing.restAngle);

            // "Nach hinten klappen"-Illusion: Flügel wird zum Extrem hin schmaler — simuliert ein
            // Foreshortening/Falten in die Tiefe, das reine Z-Rotation in 2D nicht abbilden kann.
            float factor = Mathf.Lerp(1f, wing.foldScaleAtExtreme, t);
            wing.transform.localScale = wing.foldOnYAxis
                ? new Vector3(wing.baseScale.x, wing.baseScale.y * factor, wing.baseScale.z)
                : new Vector3(wing.baseScale.x * factor, wing.baseScale.y, wing.baseScale.z);
        }
    }
}
