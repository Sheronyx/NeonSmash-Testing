using UnityEngine;

// Flügel-Flattern: die Wurzel (wing.transform, jetzt der Wurzel-BONE aus dem Sprite-Skinning-Rig statt
// des ganzen flachen Sprites) rotiert als DREIECKSWELLE (Mathf.PingPong) um ihre Scharnier-Achse (Y)
// zwischen Ruhe- und Extremwinkel -- konstante Winkelgeschwindigkeit die GANZE Zeit, exakt symmetrisch in
// beide Richtungen, mit einem sofortigen Richtungswechsel am jeweiligen Umkehrpunkt (kein Abbremsen/keine
// Rundung -- das führte in Tests reproduzierbar zu einem sichtbaren Hängenbleiben an der Ruhe-Pose).
// Echte Y-Rotation: die Verjüngung zum Extrem hin (Foreshortening) entsteht automatisch aus der
// Transform-Geometrie.
//
// Zusätzlich biegen sich die Spitzen-Äste (lagChains, z.B. oberer/unterer Ast aus dem Sprite-Skinning-
// Rig) sanft nach vorn/hinten nach -- jeder Bone in einer Kette wertet dieselbe Dreieckswelle wie die
// Wurzel aus, nur um einen mit dem Abstand zur Wurzel wachsenden Zeitversatz verzögert (KEIN SmoothDamp,
// rein zeitliche Verschiebung, siehe ApplyChains), wodurch am Umkehrpunkt ein natürlicher, zur Spitze hin
// zunehmender Peitschen-Nachlauf entsteht -- ohne Shader, ohne geteilte Sprites, rein über echtes
// Bone-Skinning.
public class FairyWingFlap : MonoBehaviour
{
    [System.Serializable]
    public class BoneChain
    {
        [Tooltip("Bones dieses Astes von der Wurzel Richtung Spitze, in Reihenfolge (NICHT den Wurzel-" +
                 "Bone selbst reinpacken, nur die Kette danach, z.B. Mitte, Spitze).")]
        public Transform[] bones;

        [Tooltip("Verteilt die Nachlauf-Verzögerung über die Kette -- X-Achse = Position in der Kette " +
                 "(0 = Bone direkt an der Wurzel, 1 = Spitze), Y-Achse = Anteil der maximalen Verzögerung " +
                 "(siehe Chain Smooth Time Step) an dieser Position. Standardmäßig linear (0->1), also wie " +
                 "bisher: jeder weitere Bone bekommt gleichmäßig mehr Verzögerung. Um z.B. die Spitze " +
                 "weniger und die mittleren Bones dafür etwas mehr wackeln zu lassen, die Kurve in der " +
                 "Mitte anheben und Richtung 1 wieder abflachen (aber nie über 1 hinaus).")]
        public AnimationCurve delayCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [System.NonSerialized] public float[] bindEulerZ;
        [System.NonSerialized] public bool    initialized;
    }

    [System.Serializable]
    public class Wing
    {
        [Tooltip("Der Wurzel-Bone aus dem Sprite-Skinning-Rig -- bekommt den großen Haupt-Flügelschlag.")]
        public Transform transform;
        [Tooltip("Ruhewinkel (Y-Achse) bei ausgebreiteten Flügeln (Pose 0).")]
        public float restAngle;
        [Tooltip("Winkel (Y-Achse) am Extrem des Flügelschlags (Pose 1, gefaltet) — Vorzeichen bestimmt " +
                 "die Richtung (rechter/linker Flügel entgegengesetzt).")]
        public float extremeAngle = 60f;
        [Tooltip("Geschwindigkeit relativ zur Basis-Frequenz — 1 = im Haupttakt, höher = schlägt " +
                 "schneller (für kleinere/äußere Flügelsegmente).")]
        public float speedMultiplier = 1f;
        [Tooltip("Wie weit dieses Segment dem Haupttakt zeitlich hinterherhinkt (0-1 Anteil eines vollen " +
                 "Zyklus). Erzeugt einen Nachlauf-/Peitschen-Effekt zwischen Segmenten (z.B. äußere " +
                 "kleine Flügel etwas später als der große Hauptflügel) statt dass alles exakt synchron " +
                 "schlägt.")]
        [Range(0f, 1f)]
        public float phaseOffset = 0f;

        [Tooltip("Äste aus dem Sprite-Skinning-Rig, die zusätzlich zur Wurzel nachlaufend biegen sollen " +
                 "(z.B. oberer Ast, unterer Ast, ggf. weitere) -- jeder Ast eine geordnete Kette von " +
                 "Bones Richtung Spitze. Bewegen sich alle synchron nach demselben Nachlauf-Muster.")]
        public BoneChain[] lagChains;

        [System.NonSerialized] public float bindEulerZ;
        [System.NonSerialized] public bool  bindCaptured;
    }

    [Header("Flatter-Tempo")]
    [Tooltip("Nur zum Testen/Kalibrieren: feste Geschwindigkeit (Constant Flap Speed) statt Bereich mit " +
             "Drift -- ignoriert Min/Max Flap Speed und die Tempo-Drift komplett, solange aktiv.")]
    [SerializeField] private bool useConstantFlapSpeed = false;
    [Tooltip("Feste Geschwindigkeit in Hz, wenn Use Constant Flap Speed aktiv ist.")]
    [SerializeField] private float constantFlapSpeed = 0.5f;
    [Tooltip("Erlaubter Bereich der Flatter-Frequenz in Hz (volle Hin-und-Zurück-Bewegung pro Sekunde). " +
             "Die tatsächliche Geschwindigkeit driftet sanft (Perlin Noise) innerhalb dieses Bereichs, " +
             "statt fest auf einem Wert zu bleiben — verhindert den mechanisch wirkenden \"exakt " +
             "identischer Loop\"-Effekt. Wird ignoriert, solange Use Constant Flap Speed aktiv ist.")]
    [SerializeField] private float minFlapSpeed = 1.8f;
    [SerializeField] private float maxFlapSpeed = 2.6f;
    [Tooltip("Wie schnell die Geschwindigkeit innerhalb von Min/Max hin- und herdriftet (höher = " +
             "schnellere Tempowechsel, niedriger = trägere Übergänge zwischen langsam und schnell).")]
    [SerializeField] private float speedDriftRate = 0.4f;
    [Tooltip("Der Ausschlag driftet sanft (eigene Perlin-Noise-Spur, unabhängig von der Tempo-Drift) " +
             "zwischen diesem Anteil der vollen Range und 100% — NIE darunter. Ein fliegender " +
             "Schmetterling schlägt fast immer nah am vollen Ausschlag, nicht mal groß/mal winzig.")]
    [Range(0f, 1f)]
    [SerializeField] private float minAmplitudeFraction = 0.7f;
    [Tooltip("Wie schnell der Ausschlag innerhalb von minAmplitudeFraction..100% hin- und herdriftet.")]
    [SerializeField] private float amplitudeDriftRate = 0.25f;
    [Header("Nachlauf-Biegung der Äste (Sprite-Skinning-Bones)")]
    [Tooltip("Maximale Nachlauf-VERZÖGERUNG (Sekunden) an der Spitze EINER Kette, wenn deren Delay Curve " +
             "bei 1 den Wert 1 hat (Standard-Kurve, linear). Wird pro Bone mit dem Kurvenwert an dessen " +
             "Position in der Kette (siehe BoneChain.delayCurve) multipliziert -- als zeitlicher Versatz " +
             "zur selben Dreieckswelle wie die Wurzel (kein SmoothDamp, siehe ApplyChains).")]
    [SerializeField] private float chainSmoothTimeStep = 0.05f;

    [Header("Flügel-Segmente (Wurzel-Bone pro Eintrag im Editor zuweisen)")]
    [SerializeField] private Wing[] wings = new Wing[]
    {
        new Wing { restAngle = 0f, extremeAngle = 60f,  speedMultiplier = 1f   },
        new Wing { restAngle = 0f, extremeAngle = -60f, speedMultiplier = 1f   },
    };

    // Folgt der Basis-Frequenz, -1..1 — für FairyFloat.LiftPhase, damit der Körper sichtbar zum
    // Flügelschlag hebt/senkt.
    public float LiftPhase { get; private set; }

    private float timer;
    // Echte Phasen-Akkumulation (Integral der Geschwindigkeit über die Zeit), NICHT timer*currentFlapSpeed
    // -- bei driftender Geschwindigkeit wäre timer*currentFlapSpeed falsch: der Fehler timer*Δgeschwindigkeit
    // wächst mit der Zeit, da timer unbegrenzt weiterläuft. Nur bei KONSTANTER Geschwindigkeit sind beide
    // Formeln identisch (weshalb "Use Constant Flap Speed" bugfrei aussah, die Drift-Variante aber mit der
    // Zeit zunehmend wild/zu schnell wurde).
    private float phaseAccumulator;
    private float noiseSeed;
    private float ampNoiseSeed;
    private float speedBoostMultiplier = 1f; // sanft interpolierter Ist-Wert, siehe Update()
    private float speedBoostTarget     = 1f; // Ziel-Wert, den SetSpeedBoost setzt
    private const float SpeedBoostEaseRate = 8f;

    private bool  poseOverride; // true = normale Flatter-Animation pausiert, siehe SetPose
    private float poseAmount;   // 0 = Ruheposition, 1 = volle Auslenkung (bei poseOverride)

    // Für ReleasePoseSmoothly: blendet nach Freigabe der Pose sanft zum automatischen Zyklus über,
    // statt hart auf dessen aktuellen (beliebigen) Zyklus-Wert zu springen.
    private bool  _releasingPose;
    private float _releaseElapsed;
    private float _releaseDuration;
    private float _releaseFromT;

    // Temporär die Flatter-Geschwindigkeit skalieren (z.B. für ein Tap-Gimmick). Baut sich in Update()
    // sanft auf/ab statt schlagartig zu springen — ein harter Sprung würde die Phase (timer*flapSpeed)
    // in einem Frame springen lassen, was wie ein Ruckler aussieht.
    public void SetSpeedBoost(float multiplier) => speedBoostTarget = multiplier;

    // Hält die Flügel manuell in einer bestimmten Auslenkung (0 = Ruheposition/offen, 1 = voll
    // ausgeklappt/gefaltet) und pausiert dabei die normale Flatter-Animation.
    public void SetPose(float amount)
    {
        poseOverride   = true;
        poseAmount     = Mathf.Clamp01(amount);
        _releasingPose = false; // eine neue manuell gesetzte Pose bricht einen evtl. laufenden Release ab
    }

    // Gibt die Flügel sanft frei: blendet über 'duration' vom zuletzt gehaltenen Pose-Wert zum jeweils
    // aktuellen automatischen Zyklus-Wert über (SmoothStep), statt hart dorthin zu springen.
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
        // Zufälliger Start-Offset, damit mehrere Fairies nicht im exakt gleichen Takt schlagen.
        timer = Random.Range(0f, 100f);
        // Eigener zufälliger Start-Offset für die Phase (unabhängig von timer, siehe phaseAccumulator).
        phaseAccumulator = Random.Range(0f, 100f);
        // Eigener Seed pro Fairy, damit die Tempo-Drift (siehe Update) bei mehreren Fairies nicht
        // synchron "atmet", sondern jede für sich unregelmäßig wirkt.
        noiseSeed = Random.Range(0f, 1000f);
        // Separater Seed für die Ausschlag-Drift, damit sie nicht synchron mit der Tempo-Drift verläuft.
        ampNoiseSeed = Random.Range(2000f, 3000f);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        speedBoostMultiplier = Mathf.Lerp(speedBoostMultiplier, speedBoostTarget,
            1f - Mathf.Exp(-SpeedBoostEaseRate * Time.deltaTime));

        float currentFlapSpeed;
        if (useConstantFlapSpeed)
        {
            // Nur zum Testen/Kalibrieren -- ignoriert Min/Max Flap Speed und die Tempo-Drift komplett.
            currentFlapSpeed = constantFlapSpeed * speedBoostMultiplier;
        }
        else
        {
            // Aktuelle Flatter-Geschwindigkeit driftet sanft zwischen Min/Max (Perlin statt reinem Random,
            // damit sie sich stetig ändert statt zu springen). WICHTIG: Mathf.PerlinNoise bekommt bewusst
            // NICHT den unbegrenzt wachsenden timer direkt, sondern einen auf 0..250 umgewickelten Wert —
            // Unitys Perlin-Implementierung driftet bei größeren Eingabewerten zunehmend Richtung 1, wodurch
            // die Geschwindigkeit über die Zeit sonst immer weiter Richtung maxFlapSpeed wandern würde.
            float noiseInput = Mathf.Repeat(timer, 250f) * speedDriftRate;
            float noiseT     = Mathf.PerlinNoise(noiseInput, noiseSeed);
            currentFlapSpeed = Mathf.Lerp(minFlapSpeed, maxFlapSpeed, noiseT) * speedBoostMultiplier;
        }

        // Phase korrekt als Integral der (evtl. driftenden) Geschwindigkeit akkumulieren -- siehe
        // Kommentar bei phaseAccumulator oben. NICHT timer*currentFlapSpeed weiter unten verwenden.
        phaseAccumulator += currentFlapSpeed * Time.deltaTime;

        // Eigene, unabhängige Noise-Spur für die Ausschlag-Stärke -- bleibt IMMER zwischen
        // minAmplitudeFraction und 100%, nie darunter (kein "mal groß, mal winzig"-Flackern).
        float ampNoiseInput  = Mathf.Repeat(timer, 250f) * amplitudeDriftRate;
        float ampNoiseT      = Mathf.PerlinNoise(ampNoiseInput, ampNoiseSeed);
        float amplitudeFactor = Mathf.Lerp(minAmplitudeFraction, 1f, ampNoiseT);

        // Basis-Zyklus als Dreieckswelle (0..1..0, konstante Geschwindigkeit) statt Sinus -- LiftPhase
        // (Körper-Auftrieb) soll im selben Bewegungsstil laufen wie die Flügel selbst.
        float baseCycle = phaseAccumulator;
        float baseTRaw  = Mathf.PingPong(baseCycle * 2f, 1f); // 0..1, konstante Steigung
        LiftPhase = baseTRaw * 2f - 1f; // -1..1

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

            // Bind-Pose-Z-Winkel der Wurzel EINMALIG sichern, bevor wir sie je anfassen -- die Bones aus
            // dem Sprite-Skinning-Rig haben oft einen Z-Anteil (2D-Zeichenrichtung). Wir bauen die
            // Rotation jeden Frame NEU aus (0, winkel, bindZ) zusammen, statt bindPose*deltaQuaternion
            // zu multiplizieren -- eine Quaternion-Multiplikation ergibt bei vorhandenem Z-Anteil NICHT
            // dasselbe wie "nur Y ändern, Z beibehalten" (Euler-Reihenfolge ist nicht kommutativ). Per
            // Hand im Inspector nur das Y-Feld zu ändern funktionierte korrekt -- das bildet genau diese
            // Fix nach.
            if (!wing.bindCaptured)
            {
                wing.bindEulerZ = wing.transform.localRotation.eulerAngles.z;
                wing.bindCaptured = true;
            }

            float t;
            float rawT = poseAmount; // im poseOverride-Fall unten überschrieben/ignoriert, siehe ApplyChains
            if (poseOverride)
            {
                t = poseAmount;

                // Keine sinnvolle Nachlauf-Größe während einer manuell gehaltenen Pose -- Äste explizit
                // auf denselben Winkel wie die Wurzel setzen (kein Nachlauf), statt den letzten
                // automatischen Wert einfrieren zu lassen.
                ApplyChains(wing, t, amplitudeFactor, useTimeDelay: false, wingCycle: 0f, cycleSpeed: 0f);
            }
            else
            {
                // Dreieckswelle statt Sinus/SmoothStep — konstante Winkelgeschwindigkeit über den GANZEN
                // Zyklus, exakt symmetrisch zwischen Hin- und Rückschlag. phaseOffset verzögert dieses
                // Segment zeitlich zum Haupttakt (Nachlauf-/Peitschen-Effekt zwischen Segmenten).
                float wingCycle = phaseAccumulator * wing.speedMultiplier - wing.phaseOffset;
                rawT = Mathf.PingPong(wingCycle * 2f, 1f);
                t = rawT;

                // Äste bekommen KEIN SmoothDamp/Feder (das erzeugte trotz kleiner Werte immer wieder ein
                // leichtes Nachfedern/Zappeln genau an der Ruhe-Pose) -- stattdessen eine rein zeitliche
                // Verzögerung: derselbe scharfe Zykluswert wie die Wurzel, nur delaySeconds in der
                // Vergangenheit ausgewertet (siehe ApplyChains). Exakt dieselbe Wellenform, nur später --
                // kann dadurch mathematisch nicht überschießen.
                ApplyChains(wing, t, amplitudeFactor, useTimeDelay: true, wingCycle: wingCycle,
                    cycleSpeed: currentFlapSpeed * wing.speedMultiplier);

                if (_releasingPose || releaseBlend < 1f)
                    t = Mathf.Lerp(_releaseFromT, t, releaseBlend);
            }

            // Ausschlag NIE unter minAmplitudeFraction der vollen Range -- der volle Weg (Ruhe- bis
            // Extremwinkel) wird um amplitudeFactor gestaucht, t selbst bleibt aber unangetastet, damit
            // Timing/Geschwindigkeit der Bewegung von der Ausschlag-Stärke unabhängig bleiben. Gilt NUR
            // für den automatischen Zyklus -- eine manuell gesetzte Pose (SetPose) soll immer exakt den
            // gewünschten Winkel erreichen, ohne von der Ausschlag-Drift verkleinert zu werden.
            float extreme = poseOverride
                ? wing.extremeAngle
                : wing.restAngle + (wing.extremeAngle - wing.restAngle) * amplitudeFactor;
            float angle = Mathf.Lerp(wing.restAngle, extreme, t);
            wing.transform.localRotation = Quaternion.Euler(0f, angle, wing.bindEulerZ);
        }
    }

    // Wandelt einen 0..1-Zykluswert in den tatsächlichen Y-Winkel dieses Wings um -- dieselbe Formel wie
    // für die Wurzel unten in Update(), hier als Helper für die Ketten-Bones wiederverwendet.
    private float ComputeAngle(Wing wing, float t, float amplitudeFactor)
    {
        float extreme = wing.restAngle + (wing.extremeAngle - wing.restAngle) * amplitudeFactor;
        return Mathf.Lerp(wing.restAngle, extreme, t);
    }

    // Lässt jeden Ast (lagChains) mit wachsender Verzögerung Richtung Spitze nachlaufen -- KEIN
    // SmoothDamp/Feder mehr (das erzeugte bei jedem Test aufs Neue ein leichtes Nachfedern/Zappeln genau
    // an der Ruhe-Pose, egal wie klein die Smooth-Zeit war). Stattdessen rein zeitliche Verzögerung: jeder
    // Ketten-Bone wertet dieselbe scharfe Dreieckswelle wie die Wurzel aus, nur delaySeconds in der
    // Vergangenheit (exakt wie phaseOffset bei ganzen Wing-Segmenten, nur pro Bone). Das ist die IDENTISCHE
    // Wellenform, nur zeitversetzt -- kann dadurch mathematisch nicht überschießen/nachfedern, egal wie
    // groß die Verzögerung ist.
    private void ApplyChains(Wing wing, float rootT, float amplitudeFactor, bool useTimeDelay, float wingCycle, float cycleSpeed)
    {
        if (wing.lagChains == null) return;

        float rootAngle = ComputeAngle(wing, rootT, amplitudeFactor);

        foreach (var chain in wing.lagChains)
        {
            if (chain.bones == null || chain.bones.Length == 0) continue;

            if (!chain.initialized || chain.bindEulerZ == null || chain.bindEulerZ.Length != chain.bones.Length)
            {
                chain.bindEulerZ = new float[chain.bones.Length];
                for (int i = 0; i < chain.bones.Length; i++)
                {
                    // Bind-Pose-Z-Winkel JEDES Bones sichern, BEVOR wir ihn je anfassen -- die Bones aus
                    // dem Sprite-Skinning-Rig wurden ja absichtlich in unterschiedliche Richtungen
                    // gezeichnet (z.B. der untere Ast zeigt nach unten, mit eigenem Z-Anteil). Die
                    // Rotation wird jeden Frame NEU aus (0, delta, bindZ) gebaut statt bindPose*delta zu
                    // multiplizieren -- siehe ausführlichen Kommentar bei der Wurzel oben.
                    chain.bindEulerZ[i] = chain.bones[i] != null ? chain.bones[i].localRotation.eulerAngles.z : 0f;
                }
                chain.initialized = true;
            }

            float parentAngle = rootAngle;
            for (int i = 0; i < chain.bones.Length; i++)
            {
                if (chain.bones[i] == null) continue;

                float chainT;
                if (useTimeDelay)
                {
                    // Position dieses Bones in der Kette (0 = direkt an der Wurzel, 1 = Spitze) über
                    // delayCurve in einen Anteil der maximalen Verzögerung (chainSmoothTimeStep) umwandeln
                    // -- Standard-Kurve ist linear (0->1), reproduziert also das alte Verhalten "jeder
                    // weitere Bone bekommt gleichmäßig mehr Verzögerung". Über die Kurve lässt sich das
                    // aber frei umformen, z.B. mittlere Bones stärker anheben und die Spitze wieder
                    // abflachen. delaySeconds wird in Zyklus-Einheiten umgerechnet (Ableitung von
                    // wingCycle nach der Zeit ist cycleSpeed), dann exakt dieselbe PingPong-Formel wie die
                    // Wurzel ausgewertet -- nur mit einem in die Vergangenheit verschobenen Zyklus.
                    float normalizedPos = (float)(i + 1) / chain.bones.Length;
                    float delaySeconds = chain.delayCurve.Evaluate(normalizedPos) * chainSmoothTimeStep * chain.bones.Length;
                    float delayedCycle = wingCycle - delaySeconds * cycleSpeed;
                    chainT = Mathf.PingPong(delayedCycle * 2f, 1f);
                }
                else
                {
                    // Pose-Override/Release: kein sinnvoller Nachlauf-Zyklus vorhanden -- Ast folgt direkt
                    // der Wurzel.
                    chainT = rootT;
                }

                float thisAngle = ComputeAngle(wing, chainT, amplitudeFactor);
                // Lokale Rotation relativ zum ELTERN-Bone (Wurzel bei i=0, sonst der vorherige Bone in
                // der Kette) -- da Bones in der Hierarchy verschachtelt sind, addiert Unity das
                // automatisch zur geerbten Rotation dazu.
                float localDelta = thisAngle - parentAngle;
                // Bind-Pose-Z-Winkel (die ursprüngliche Zeichenrichtung des Bones) BEIBEHALTEN, die
                // Rotation komplett neu aus (0, delta, bindZ) aufbauen statt zu multiplizieren.
                chain.bones[i].localRotation = Quaternion.Euler(0f, localDelta, chain.bindEulerZ[i]);

                parentAngle = thisAngle;
            }
        }
    }
}
