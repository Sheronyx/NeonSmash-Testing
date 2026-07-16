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
    [Tooltip("Flatter-Frequenz in Hz (volle Hin-und-Zurück-Bewegung pro Sekunde).")]
    [SerializeField] private float flapSpeed = 2.2f;
    [Tooltip("Zum Testen: Z-Rotation abschalten, um zu sehen, ob die Fold-Skalierung allein die " +
             "Flatter-Illusion trägt. Bei aus bleibt der Flügel auf seinem Rest-Winkel stehen.")]
    [SerializeField] private bool rotationEnabled = true;

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

    private void Awake()
    {
        // Zufälliger Start-Offset, damit mehrere Fairies (gleiche Geschwindigkeit) nicht im
        // exakt gleichen Takt schlagen, sondern sichtbar versetzt zueinander.
        timer = UnityEngine.Random.Range(0f, 100f);

        foreach (var wing in wings)
            if (wing.transform != null) wing.baseScale = wing.transform.localScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float basePhase = timer * flapSpeed * Mathf.PI * 2f;
        LiftPhase = Mathf.Sin(basePhase);

        foreach (var wing in wings)
        {
            if (wing.transform == null) continue;

            // (1 - cos(x)) / 2 läuft durchgehend glatt 0→1→0→1... ohne je an einer Position zu
            // pausieren — im Gegensatz zu einem zweiseitigen Sinus, der bei nur einseitig genutztem
            // Bereich (maxAngle bzw. minAngle == restAngle) die halbe Zeit auf der Ruheposition
            // "einfriert", bis der nächste Schlag beginnt.
            float phase = timer * flapSpeed * wing.speedMultiplier * Mathf.PI * 2f;
            float t     = (1f - Mathf.Cos(phase)) * 0.5f;

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
