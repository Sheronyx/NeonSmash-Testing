using UnityEngine;

// Steuert das Flügel-Flattern der Fairy: jedes Flügel-Segment rotiert zwischen seinem eigenen
// Min/Max um seine eigene Ruheposition, mit eigenem Geschwindigkeits-Multiplikator relativ zur
// Basis-Frequenz (kleinere Flügel schlagen realistischerweise schneller als große). Der Auftrieb
// für FairyFloat (LiftPhase) folgt weiterhin dem Basis-Takt (= den großen Flügeln), damit der
// Körper sichtbar zum Haupt-Flügelschlag hebt, nicht zum schnellen Geflatter der kleinen Flügel.
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
        [Tooltip("Falls die Bewegung dieses Flügels visuell falsch herum wirkt: hier umkehren, statt " +
                 "Min/Max/Rest neu einzutragen.")]
        public bool invertPhase = false;
        [Tooltip("Simuliert Nach-hinten-Klappen: wie schmal der Flügel bei voller Auslenkung wird " +
                 "(1 = kein Effekt, 0.5 = wird halb so breit). Reine Rotation wirkt sonst nur wie ein " +
                 "flaches Kippen, kein echtes Falten in die Tiefe.")]
        [Range(0.2f, 1f)]
        public float foldScaleAtExtreme = 0.55f;
        [Tooltip("Falls der Flügel lokal 'liegend' ausgerichtet ist: hier auf die Y- statt X-Achse umschalten.")]
        public bool foldOnYAxis = false;

        [System.NonSerialized] public Vector3 baseScale;
    }

    [Header("Flatter-Tempo (Basis, große Flügel)")]
    [Tooltip("Flatter-Frequenz in Hz (volle Auf-/Ab-Bewegung pro Sekunde).")]
    [SerializeField] private float flapSpeed = 2.2f;

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

    // -1 = volle Aufwärtsbewegung (Aufschlag), +1 = volle Abwärtsbewegung (Abschlag/Downstroke —
    // erzeugt beim echten Flug Auftrieb, siehe FairyFloat.liftAmount). Folgt der Basis-Frequenz.
    public float LiftPhase { get; private set; }

    private float timer;

    private void Awake()
    {
        foreach (var wing in wings)
            if (wing.transform != null) wing.baseScale = wing.transform.localScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        LiftPhase = Mathf.Sin(timer * flapSpeed * Mathf.PI * 2f);

        foreach (var wing in wings)
        {
            if (wing.transform == null) continue;
            float wingPhase = Mathf.Sin(timer * flapSpeed * wing.speedMultiplier * Mathf.PI * 2f);
            float p = wing.invertPhase ? -wingPhase : wingPhase;
            float angle = p >= 0f
                ? Mathf.Lerp(wing.restAngle, wing.maxAngle, p)
                : Mathf.Lerp(wing.restAngle, wing.minAngle, -p);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // "Nach hinten klappen"-Illusion: Flügel wird bei voller Auslenkung schmaler — simuliert
            // ein Foreshortening/Falten in die Tiefe, das reine Z-Rotation in 2D nicht abbilden kann.
            float foldK  = Mathf.Abs(p); // 0 = Ruheposition (volle Breite), 1 = volle Auslenkung (schmal)
            float factor = Mathf.Lerp(1f, wing.foldScaleAtExtreme, foldK);
            wing.transform.localScale = wing.foldOnYAxis
                ? new Vector3(wing.baseScale.x, wing.baseScale.y * factor, wing.baseScale.z)
                : new Vector3(wing.baseScale.x * factor, wing.baseScale.y, wing.baseScale.z);
        }
    }
}
