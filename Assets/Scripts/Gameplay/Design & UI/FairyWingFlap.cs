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
    [Tooltip("Wie schnell sich ein Speed-Boost (z.B. von einem Tap-Gimmick) sanft auf- und wieder " +
             "abbaut, statt schlagartig zu springen — höher = schnellerer Übergang.")]
    [SerializeField] private float speedBoostEaseRate = 8f;

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
    private float speedBoostMultiplier = 1f; // sanft interpolierter Ist-Wert, siehe Update()
    private float speedBoostTarget     = 1f; // Ziel-Wert, den SetSpeedBoost setzt
    private bool  poseOverride;              // true = normale Flatter-Animation pausiert, siehe SetPose
    private float poseAmount;                // 0 = Ruheposition, 1 = volle Auslenkung (bei poseOverride)

    // Für ReleasePoseSmoothly: blendet nach Freigabe der Pose sanft zum automatischen Zyklus über,
    // statt hart auf dessen aktuellen (beliebigen) Zyklus-Wert zu springen — siehe dort.
    private bool  _releasingPose;
    private float _releaseElapsed;
    private float _releaseDuration;
    private float _releaseFromT;

    // Temporär die Flatter-Geschwindigkeit skalieren (z.B. für ein Tap-Gimmick, das die Fee
    // kurz schneller flattern lässt). 1 = normales Tempo. Baut sich in Update() sanft auf/ab statt
    // schlagartig zu springen — ein harter Sprung würde die Flügel-Phase (timer * currentFlapSpeed)
    // in einem Frame springen lassen, was wie ein Ruckler aussieht.
    public void SetSpeedBoost(float multiplier) => speedBoostTarget = multiplier;

    // Hält die Flügel manuell in einer bestimmten Auslenkung (0 = Ruheposition, 1 = voll
    // ausgeklappt) und pausiert dabei die normale Flatter-Animation — für Gimmicks, die die
    // Flügel gezielt öffnen/schließen wollen (z.B. "aufplustern"), statt nur schneller/langsamer
    // im normalen Zyklus zu flattern. Der Aufrufer ist selbst dafür verantwortlich, poseAmount
    // über die Zeit sanft zu ändern (z.B. per SmoothStep) statt springen zu lassen.
    public void SetPose(float amount)
    {
        poseOverride   = true;
        poseAmount     = Mathf.Clamp01(amount);
        _releasingPose = false; // eine neue manuell gesetzte Pose bricht einen evtl. laufenden Release ab
    }

    // Gibt die Flügel HART frei — der automatische Zyklus übernimmt sofort mit seinem eigenen,
    // zu diesem Zeitpunkt völlig beliebigen Zwischenstand (timer lief ja während der Pose einfach
    // weiter). Das erzeugt fast immer einen sichtbaren Sprung im Flügel-Winkel. Für einen normalen
    // Übergang IMMER ReleasePoseSmoothly verwenden — diese Methode nur falls ein sofortiger,
    // unanimierter Reset explizit gewünscht ist.
    public void ClearPose() => poseOverride = false;

    // Gibt die Flügel sanft frei: blendet über 'duration' vom zuletzt gehaltenen Pose-Wert zum
    // jeweils aktuellen automatischen Zyklus-Wert über (SmoothStep), statt hart dorthin zu springen.
    // So sieht der Übergang IMMER smooth aus, egal in welchem Zustand die Flügel gerade waren (z.B.
    // mitten in einer unterbrochenen Gimmick-Animation) — genau das macht den Unterschied zwischen
    // "man sieht manchmal keinen Flügelschlag" (harter Sprung wirkt wie ein Freeze-Frame) und einem
    // garantiert glatten Übergang in den Flatterzustand.
    public void ReleasePoseSmoothly(float duration = 0.25f)
    {
        if (poseOverride)
        {
            _releaseFromT = poseAmount;
        }
        else if (!_releasingPose)
        {
            return; // schon voll automatisch, nichts zu tun
        }

        poseOverride     = false;
        _releasingPose   = true;
        _releaseElapsed  = 0f;
        _releaseDuration = Mathf.Max(0.0001f, duration);
    }

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

    private void Update()
    {
        timer += Time.deltaTime;

        // Exponentielle Annäherung an den Ziel-Boost statt hartem Sprung — sonst würde die Phase
        // unten (timer * currentFlapSpeed) in einem einzigen Frame springen und wie ein Ruckler
        // in der Flügelbewegung aussehen.
        speedBoostMultiplier = Mathf.Lerp(speedBoostMultiplier, speedBoostTarget,
            1f - Mathf.Exp(-speedBoostEaseRate * Time.deltaTime));

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
        float currentFlapSpeed = Mathf.Lerp(minFlapSpeed, maxFlapSpeed, noiseT) * speedBoostMultiplier;
        float ampJitter        = (noiseT * 2f - 1f) * amplitudeVariation; // -1..1, für die Ausschlag-Variation unten

        float basePhase = timer * currentFlapSpeed * Mathf.PI * 2f;
        LiftPhase = Mathf.Sin(basePhase);

        // Blend-Faktor für ReleasePoseSmoothly: 0 = noch beim eingefrorenen Pose-Wert, 1 = voll beim
        // automatischen Zyklus. Einmal pro Frame berechnet (nicht pro Wing), da für alle Flügel gleich.
        float releaseBlend = 1f;
        if (_releasingPose)
        {
            _releaseElapsed += Time.deltaTime;
            releaseBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_releaseElapsed / _releaseDuration));
            if (releaseBlend >= 1f) _releasingPose = false;
        }

        foreach (var wing in wings)
        {
            if (wing.transform == null) continue;

            float t;
            if (poseOverride)
            {
                // Manuell gehaltene Pose statt normalem Flatter-Zyklus — z.B. für ein langsames
                // Aufklappen/Zusammenlegen der Flügel unabhängig vom automatischen Takt.
                t = poseAmount;
            }
            else
            {
                float cyclePos = timer * currentFlapSpeed * wing.speedMultiplier;
                float k = cyclePos - Mathf.Floor(cyclePos); // 0..1 innerhalb des aktuellen Zyklus, läuft durchgehend ohne Pause

                // Asymmetrische Schlag-Kurve statt symmetrischem Kosinus: schneller Ausschlag zum Extrem
                // (Kraftschlag), langsameres Zurückkehren (Rückschlag) — fühlt sich lebendiger an als eine
                // perfekt gleichförmige Hin-und-her-Bewegung. SmoothStep sorgt dafür, dass die Geschwindigkeit
                // an beiden Umkehrpunkten sanft auf 0 geht, kein Ruck beim Richtungswechsel.
                t = k < strokePhaseFraction
                    ? Mathf.SmoothStep(0f, 1f, k / strokePhaseFraction)
                    : Mathf.SmoothStep(1f, 0f, (k - strokePhaseFraction) / (1f - strokePhaseFraction));

                // Leichte Ausschlag-Variation pro Zyklus, synchron zur Tempo-Drift oben.
                t = Mathf.Clamp01(t * (1f + ampJitter));

                // Frisch freigegebene Pose: sanft vom eingefrorenen Wert zu diesem automatischen t
                // überblenden statt sofort zu springen (siehe ReleasePoseSmoothly).
                if (_releasingPose || releaseBlend < 1f)
                    t = Mathf.Lerp(_releaseFromT, t, releaseBlend);
            }

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
