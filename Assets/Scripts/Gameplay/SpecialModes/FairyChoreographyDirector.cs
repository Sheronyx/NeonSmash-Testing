using System;
using System.Collections;
using UnityEngine;

// Ersetzt die alte Aktivierungs-Kugel + Slash-Zeremonie: sobald der Farb-/Energie-Trigger einer Fee
// erreicht ist, ruft PhaseManager.Co_TriggerSpecialMode hier PlaySpecialModeIntro(mode) auf.
//
// Ablauf Intro (~introDuration):
//  - Die Lead-Fee (Farbe → Modus via PhaseManager.SpecialModeForColor) spielt ihre Special-Mode-Flug-
//    Animation (Animator-Bool "IsSpecialMode" = true) und fliegt eine choreografierte Bahn:
//      Vortex  → kurz aus dem Bild raus, dann als großer Strudel wieder rein, Radius schrumpft bis
//               Bildschirmmitte (Drehrichtung = VortexModeSystem.clockwise).
//      sonst   → Platzhalter: direkter Anflug zur Mitte (bis Gravity/Fountain eigene Choreos haben).
//  - Die anderen beiden Feen fliegen nach unten aus dem Bild.
//  - In der Mitte angekommen: Lead-Fee zurück auf Idle_Fly ("IsSpecialMode" = false), dann
//    SpecialModeManager.StartMode(mode) — ab hier läuft der eigentliche Special Mode (Elemente spawnen).
//
// Ablauf Outro (bei SpecialModeManager.OnModeEnded, ~outroDuration): alle drei Feen fliegen im Bogen
// zurück an ihre Ausgangsposition/-rotation/-größe, alle wieder Idle_Fly.
//
// PhaseManager wartet nach PlaySpecialModeIntro per WaitUntil(IsModeActive) — StartMode() am Ende des
// Intros ersetzt also 1:1 das frühere FinishCombo() der Aktivierungs-Kugel.
public class FairyChoreographyDirector : MonoBehaviour
{
    public static FairyChoreographyDirector Instance { get; private set; }

    [Serializable]
    public class FairyEntry
    {
        public PointColor color;
        public Transform fairy;
        [Tooltip("Leer lassen = wird in den Kindern gesucht.")]
        public Animator animator;
        [Tooltip("Optional: Partikelspur (z.B. Blasen/Blätter/Steine), die NUR während der Special-Mode-" +
                 "Flugbahn hinter der Fee herausströmt — läuft automatisch synchron zum Flug-Start/-Ende.")]
        public ParticleSystem specialModeTrail;
        [System.NonSerialized] public FairyGlowFlash glow;
    }

    [SerializeField] private FairyEntry[] fairies = new FairyEntry[3];
    [SerializeField] private string specialModeBool = "IsSpecialMode";

    [Header("Intro")]
    [SerializeField] private float introDuration = 5f;
    [Tooltip("Anteil der Intro-Zeit, in dem die Lead-Fee kurz aus dem Bild raus fliegt, bevor die eigentliche Choreo beginnt.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float leadExitPhase = 0.15f;

    [Header("Vortex-Spirale")]
    [Tooltip("Sichtbare Umdrehungen des Strudels (der Off-Screen-Anflug kommt oben drauf).")]
    [SerializeField] private float spiralRevolutions = 3f;
    [Tooltip("Größe des sichtbaren Strudels relativ zur KLEINEREN halben Bildschirmseite (Hochformat = " +
             "halbe Breite). 1 = füllt die Bildbreite; <1 = enger. Alle sichtbaren Umdrehungen bleiben im Bild.")]
    [SerializeField] private float spiralOnScreenRadius = 0.95f;
    [Tooltip("Wie weit außerhalb die Spirale beginnt, relativ zur halben Bildhöhe (2 = doppelte halbe Höhe). " +
             "Die Fee kommt von dort schon spiralend rein — kein Zwischenstopp.")]
    [SerializeField] private float spiralOffScreenRadiusVH = 2f;
    [Tooltip("Ovalität des Strudels: 1 = kreisrund, <1 = horizontal gestaucht (schmaler als hoch, passt zu " +
             "Hochformat). ~0.65–0.8 wirkt natürlich.")]
    [Range(0.4f, 1f)]
    [SerializeField] private float spiralAspect = 0.72f;
    [Tooltip("Muss zur Saugrichtung der Vortex-Elemente passen (VortexModeSystem.clockwise = true → hier true).")]
    [SerializeField] private bool spiralClockwise = true;
    [Tooltip("Zielgröße der Fee in der Strudelmitte, relativ zur Heimat-Größe (0.5 = halb so groß — " +
             "wirkt, als flöge sie in den Hintergrund). Das Outro skaliert wieder auf Heimat-Größe.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float spiralEndScale = 0.55f;
    [Tooltip("Zeitversatz (Sekunden), mit dem der Blätter-Trail der Spiralbahn 'hinterherhinkt' — sorgt " +
             "dafür, dass die Blätter sichtbar aus dem tatsächlich geflogenen Weg kommen statt aus der " +
             "aktuellen (in engen Kurven optisch 'falschen') Fee-Position/-Rotation.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float trailDelay = 0.1f;

    [Header("Gravity — pinke Fee (2 Durchflüge von oben nach unten)")]
    [Tooltip("Sekunden für EINEN sichtbaren Durchflug von oben durchs Bild nach unten. Höher = langsamer.")]
    [SerializeField] private float gravityPassDuration = 1.6f;
    [Tooltip("Wie weit über/unter den Bildrand die Fee hinausschießt (Viewport-Einheiten außerhalb).")]
    [SerializeField] private float gravityPassOvershoot = 0.3f;
    [Tooltip("Seitlicher Versatz der beiden Durchflüge von der Mitte (Viewport-Einheiten): 1. Durchflug " +
             "links, 2. rechts. 0.22 = 22 % Bildbreite je Seite.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float gravityPassSpread = 0.22f;
    [Tooltip("Eigenrotation der pinken Fee um ihre lokale Y-Achse während der Durchflüge (Grad/Sek). " +
             "360 = eine volle Umdrehung pro Sekunde.")]
    [SerializeField] private float gravitySpinSpeed = 420f;

    [Header("Fountain — blaue Fee (seitlich in den Hintergrund, dann 2x Bogenlampe nach vorn-unten raus)")]
    [Tooltip("Sekunden für den ersten Rausflug seitlich in den Hintergrund (schrumpft dabei auf fountainBackScale).")]
    [SerializeField] private float fountainBackExitDuration = 0.9f;
    [Tooltip("Wie klein die Fee im Hintergrund (seitlich, weit hinten) wird, relativ zur Heimat-Größe.")]
    [Range(0.15f, 0.6f)]
    [SerializeField] private float fountainBackScale = 0.4f;
    [Tooltip("Sekunden pro Bogenlampen-Flug (von hinten-seitlich-klein in einer Kurve nach vorn-unten-groß raus).")]
    [SerializeField] private float fountainArcDuration = 1.3f;
    [Tooltip("Scheitel-Höhe des Bogens als Viewport-Y (>1 = über dem oberen Bildrand). Muss höher liegen " +
             "als Start- und Endpunkt, damit ein echtes \"umgekehrtes U\" (erst hoch, dann vorn/unten raus) entsteht.")]
    [SerializeField] private float fountainArcPeakVpY = 1.15f;
    [Tooltip("Eigenrotation der blauen Fee um ihre lokale Y-Achse während der Bögen (Grad/Sek).")]
    [SerializeField] private float fountainSpinSpeed = 340f;

    [Header("Nebenfeen (raus nach unten)")]
    [SerializeField] private float sideFairyExitDuration = 1.2f;
    [Tooltip("Ziel-Viewport-Y der Nebenfeen (< 0 = unter dem Bildrand).")]
    [SerializeField] private float sideFairyExitViewportY = -0.35f;

    [Header("Outro")]
    [SerializeField] private float outroDuration = 1.8f;
    [SerializeField] private float outroCurveStrength = 1.2f;
    [Tooltip("Sekunden am Ende des Heimflugs, über die die Fee per REINER, direkt geglätteter Drehung " +
             "in die Idle-Rotation übergeht (kein exponentielles Nachziehen) — höher = weicher/länger.")]
    [SerializeField] private float rotationSettleTime = 0.9f;

    [Header("Flugrichtungs-Rotation")]
    [Tooltip("Wie schnell die Fee in ihre Flugrichtung dreht (höher = schneller/direkter, aber weniger " +
             "weich). 0 = keine Ausrichtung.")]
    [SerializeField] private float turnSpeed = 6f;
    [Tooltip("Wie stark die Fee in die Tiefe (Hintergrund) schaut statt in der Bildebene zu bleiben. " +
             "0 = rein in Flugrichtung (Profil), 1 = voll in den Hintergrund. ~0.6–0.8 = fliegt sichtbar " +
             "\"nach hinten und seitlich\" weg.")]
    [Range(0f, 1f)]
    [SerializeField] private float backgroundFacing = 0.7f;
    [Tooltip("Zusätzliche \"liegend\"-Neigung um die eigene Seitwärts-Achse (Grad). 0 = kein Kippen, " +
             "~45–65 = flach in der Luft liegend / Sturzflug-Haltung.")]
    [Range(0f, 90f)]
    [SerializeField] private float flightLeanDegrees = 55f;
    [Tooltip("Extra Nase-nach-unten-Kippen beim ABWÄRTS-Fliegen (Grad), skaliert mit der Steilheit des " +
             "Sinkflugs. Beim Aufwärtsfliegen bleibt es unverändert.")]
    [Range(0f, 60f)]
    [SerializeField] private float diveLeanBoostDegrees = 30f;
    [Tooltip("Korrektur, falls die Fee nicht mit dem Gesicht voran fliegt (Modell-Vorderseite ≠ lokales +Z). " +
             "Als Euler-Offset auf die berechnete Blick-Rotation multipliziert.")]
    [SerializeField] private Vector3 faceDirModelOffsetEuler = Vector3.zero;
    [Tooltip("Anteil einer Flugphase, über den die Fee WEICH aus der frontalen Idle-Rotation in die " +
             "Flughaltung dreht (verhindert den ruckartigen Sprung beim Losfliegen von der Standardposition). " +
             "Das weiche ZURÜCK-Drehen am Ende regelt 'Rotation Settle Time' im Outro-Block.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float rotationEaseIn = 0.28f;

    private Vector3[] _homePos;
    private Quaternion[] _homeRot;
    private Vector3[] _homeScale;
    private Coroutine _running;
    private Camera _cam;
    private bool _outroActive;
    private Quaternion _choreoHomeRot = Quaternion.Euler(0f, 180f, 0f); // frontale Idle-Rotation der Feen

    /// <summary>True, solange das Outro läuft (Feen fliegen zurück an ihre Startpositionen).
    /// PhaseManager wartet darauf, bevor die nächste Normal-Phase mit dem Spawnen beginnt.</summary>
    public bool IsOutroActive => _outroActive;

    private void Awake()
    {
        Instance = this;
        _cam = Camera.main;

        _homePos   = new Vector3[fairies.Length];
        _homeRot   = new Quaternion[fairies.Length];
        _homeScale = new Vector3[fairies.Length];

        for (int i = 0; i < fairies.Length; i++)
        {
            var e = fairies[i];
            if (e == null || e.fairy == null) continue;
            if (e.animator == null) e.animator = e.fairy.GetComponentInChildren<Animator>();
            e.glow = e.fairy.GetComponentInChildren<FairyGlowFlash>();
            _homePos[i]   = e.fairy.position;
            _homeRot[i]   = e.fairy.rotation;
            _homeScale[i] = e.fairy.localScale;
            _choreoHomeRot = e.fairy.rotation; // alle Feen identisch (Euler 0,180,0)
        }

        SpecialModeManager.OnModeEnded += HandleModeEnded;
    }

    private void OnDestroy()
    {
        SpecialModeManager.OnModeEnded -= HandleModeEnded;
    }

    // ── Intro ────────────────────────────────────────────────────────────────

    /// <summary>Vom PhaseManager: startet die Feen-Choreo. Ruft am Ende SpecialModeManager.StartMode(mode).</summary>
    public void PlaySpecialModeIntro(SpecialMode mode)
    {
        if (_cam == null) _cam = Camera.main;
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Co_Intro(mode));
    }

    private int LeadIndex(SpecialMode mode)
    {
        for (int i = 0; i < fairies.Length; i++)
            if (fairies[i] != null && fairies[i].fairy != null &&
                PhaseManager.SpecialModeForColor(fairies[i].color) == mode)
                return i;
        return -1;
    }

    private int ColorIndex(PointColor color)
    {
        for (int i = 0; i < fairies.Length; i++)
            if (fairies[i] != null && fairies[i].fairy != null && fairies[i].color == color)
                return i;
        return -1;
    }

    private IEnumerator Co_Intro(SpecialMode mode)
    {
        int lead = LeadIndex(mode);
        Vector3 center = ScreenCenterAtZ(lead >= 0 ? _homePos[lead].z : -5f);

        // Nebenfeen parallel raus nach unten
        for (int i = 0; i < fairies.Length; i++)
        {
            if (i == lead || fairies[i] == null || fairies[i].fairy == null) continue;
            StartCoroutine(Co_SideFairyExit(fairies[i].fairy, _homeRot[i]));
        }

        if (lead < 0)
        {
            // Keine passende Fee — trotzdem StartMode, damit der PhaseManager nicht ewig wartet.
            SpecialModeManager.Instance?.StartMode(mode);
            _running = null;
            yield break;
        }

        var f         = fairies[lead].fairy;
        var anim      = fairies[lead].animator;
        var homeRot   = _homeRot[lead];
        var homeScale = _homeScale[lead];

        // Ziel-Position am Ende der Choreo, je Modus:
        //  Vortex  → echte Bildschirmmitte
        //  Gravity → pinke Fee an ihren eigenen Standardplatz
        //  Fountain→ blaue Fee an den (mittigen) Platz, wo sonst die pinke Fee steht
        int pinkIdx = ColorIndex(PointColor.Pink);
        Vector3 endPos = mode switch
        {
            SpecialMode.Vortex   => center,
            SpecialMode.Fountain => pinkIdx >= 0 ? _homePos[pinkIdx] : _homePos[lead],
            _                    => _homePos[lead],
        };

        SetSpecialBool(anim, true);
        fairies[lead].glow?.SetOutlineGlow(true);   // Leucht-Umrandung an fürs Special-Mode
        // Partikelspur: Play() NICHT hier generell — die einzelnen Choreo-Coroutinen starten sie erst
        // ab dem Moment, an dem die Fee tatsächlich SICHTBAR im Bild fliegt (nicht während sie unsichtbar
        // off-screen unterwegs ist, z.B. beim Reinfliegen in den Hintergrund).
        var trail = fairies[lead].specialModeTrail;

        switch (mode)
        {
            case SpecialMode.Vortex:   yield return Co_VortexSpiral(f, center, homeRot, homeScale, trail); break;
            case SpecialMode.Gravity:  yield return Co_GravityDrops(f, endPos, homeRot, trail);      break;
            case SpecialMode.Fountain: yield return Co_FountainArcs(f, endPos, homeRot, homeScale, trail); break;
            default:                   yield return Co_SimpleApproach(f, endPos, homeRot);          break;
        }

        f.position   = endPos;
        f.rotation   = homeRot;
        SetSpecialBool(anim, false);
        // Stoppt nur das NEUE Emittieren — bereits ausgestoßene Blasen/Blätter/Steine klingen noch
        // natürlich aus, statt abrupt zu verschwinden. In try/catch: manche VFX-Assets (z.B. CFXR)
        // bringen eigene Auto-Destroy-Skripte mit, die das Objekt zerstören können, ohne dass unsere
        // Referenz das rechtzeitig mitbekommt (MissingReferenceException trotz "?."-Check) — das darf
        // NIEMALS den Start des eigentlichen Special Modes (StartMode unten) verhindern.
        try { fairies[lead].specialModeTrail?.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        catch (MissingReferenceException) { /* Trail-Objekt wurde extern zerstört — ignorieren */ }

        if (SpecialModeManager.Instance != null && !SpecialModeManager.Instance.IsModeActive)
            SpecialModeManager.Instance.StartMode(mode);

        _running = null;
    }

    private IEnumerator Co_VortexSpiral(Transform f, Vector3 center, Quaternion homeRot, Vector3 homeScale, ParticleSystem trail)
    {
        if (trail != null)
        {
            try
            {
                trail.Play();
                var em = trail.emission;
                em.enabled = false;   // startet off-screen — erst per SetTrailVisible() an, sobald sichtbar
            }
            catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
        }

        float z       = f.position.z;
        float approachT = introDuration * leadExitPhase;   // kurzer Anflug zum Spiral-Startpunkt
        float spiralT   = introDuration - approachT;       // die eigentliche, durchgehende Spirale

        float onScreenR  = ScreenHalfMinWorld() * spiralOnScreenRadius;
        float offScreenR = HalfHeightWorld() * Mathf.Max(1.2f, spiralOffScreenRadiusVH);

        float dirSign    = spiralClockwise ? -1f : 1f;
        float totalSweep = spiralRevolutions * Mathf.PI * 2f * dirSign;

        Vector3 startPos = f.position;
        Vector3 awayDir  = (startPos - center).sqrMagnitude > 0.0001f
            ? (startPos - center).normalized
            : Vector3.down;
        float startAngle = Mathf.Atan2(awayDir.y, awayDir.x);

        // Ovale Spiral-Position + analytische Tangente (Ellipse: x um spiralAspect gestaucht).
        Vector3 SpiralPos(float angle, float radius) => new Vector3(
            center.x + Mathf.Cos(angle) * radius * spiralAspect,
            center.y + Mathf.Sin(angle) * radius,
            z);
        Vector3 SpiralTangent(float angle) => new Vector3(
            -Mathf.Sin(angle) * spiralAspect,
             Mathf.Cos(angle),
             0f) * Mathf.Sign(dirSign);

        // Radius-Verlauf: erste ~25% schnell von offScreenR (weit draußen) auf onScreenR (Bildgröße),
        // danach linear auf 0 — konstante Radial-Geschwindigkeit, kein Stillstand irgendwo.
        float RadiusAt(float p)
        {
            const float enterP = 0.25f;
            if (p < enterP)
            {
                float lp   = p / enterP;
                float ease = 1f - (1f - lp) * (1f - lp);   // ease-out: sofort schnell rein
                return Mathf.Lerp(offScreenR, onScreenR, ease);
            }
            float q = (p - enterP) / (1f - enterP);
            return Mathf.Lerp(onScreenR, 0f, q);
        }

        Vector3 spiralStart = SpiralPos(startAngle, RadiusAt(0f));

        // Phase A: kurzer Anflug von der Ausgangsposition zum (off-screen) Spiral-Startpunkt.
        // Rotation weich aus der frontalen Idle-Haltung in die Spiral-Anfangs-Tangente.
        Quaternion startFlightRot = FlightRotation(SpiralTangent(startAngle));
        Vector3 prevPos = f.position;
        float t = 0f;
        while (t < approachT)
        {
            t += Time.deltaTime;
            float lp = Mathf.Clamp01(t / approachT);
            float p  = lp * lp;                         // ease-IN: beschleunigt, bremst NICHT ab
            f.position = Vector3.Lerp(startPos, spiralStart, p);
            SlerpTo(f, Quaternion.Slerp(homeRot, startFlightRot, FlightWeightIn(lp)), turnSpeed);
            SetTrailVisible(trail, f.position);
            prevPos = f.position;
            yield return null;
        }

        // Phase B: EINE durchgehende Spirale. Winkel läuft linear (konstante Drehrate). Über die
        // letzten `rotationSettleTime` Sekunden geht die Rotation per reiner, direkt geglätteter
        // Slerp in die Idle-Rotation über (kein Chase, kein Springen).
        float settleT = Mathf.Min(rotationSettleTime, spiralT * 0.85f);
        float travelT = spiralT - settleT;
        Quaternion settleFrom = f.rotation;
        bool captured = false;

        t = 0f;
        while (t < spiralT)
        {
            t += Time.deltaTime;
            float p      = Mathf.Clamp01(t / spiralT);
            float radius = RadiusAt(p);
            float angle  = startAngle + totalSweep * p;

            f.position   = SpiralPos(angle, radius);
            f.localScale = Vector3.Lerp(homeScale, homeScale * spiralEndScale, Mathf.SmoothStep(0f, 1f, p));

            if (t < travelT)
            {
                SlerpTo(f, FlightRotation(SpiralTangent(angle)), turnSpeed);

                // Der Trail hängt als Kind an der Fee und würde sonst ihre (bewusst geglättete,
                // rate-limitierte) Körper-Rotation UND -Position 1:1 übernehmen — das sieht in der engen
                // Spiral-Mitte (Radius schrumpft auf 0, Kurve wird dort extrem eng) unnatürlich aus.
                // Fix: Trail-Position/-Rotation stattdessen von einem leicht ZURÜCKLIEGENDEN Punkt auf
                // derselben analytischen Spiralbahn ableiten (~0.1s "Verzögerung") — die Blätter kommen
                // dadurch sichtbar aus dem tatsächlich geflogenen Weg, nicht aus der aktuellen Fee-Pose.
                if (trail != null)
                {
                    try
                    {
                        float tDelayed     = Mathf.Max(0f, t - trailDelay);
                        float pDelayed     = Mathf.Clamp01(tDelayed / spiralT);
                        float radiusDelayed = RadiusAt(pDelayed);
                        float angleDelayed  = startAngle + totalSweep * pDelayed;
                        trail.transform.position = SpiralPos(angleDelayed, radiusDelayed);
                        trail.transform.rotation = FlightRotation(SpiralTangent(angleDelayed));
                    }
                    catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
                }
            }
            else
            {
                if (!captured) { settleFrom = f.rotation; captured = true; }
                float sp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - travelT) / settleT));
                f.rotation = Quaternion.Slerp(settleFrom, homeRot, sp);
            }

            // Die letzten ~20% sind die engste Kurve kurz vorm Verschwinden im Zentrum (Radius nahe 0) —
            // selbst mit der oben korrigierten Trail-Rotation sieht das Blätter-Auswerfen dort noch
            // unruhig aus. Trail hier einfach schon vorher abschalten, statt bis zum Schluss laufen zu lassen.
            if (p > 0.8f)
            {
                if (trail != null)
                {
                    try { var em = trail.emission; em.enabled = false; }
                    catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
                }
            }
            else
            {
                SetTrailVisible(trail, f.position);
            }
            yield return null;
        }

        f.position   = new Vector3(center.x, center.y, z);
        f.rotation   = homeRot;
        f.localScale = homeScale * spiralEndScale;
    }

    // Gravity / pink: 2 sichtbare Durchflüge von oben nach unten (1× mehr links, 1× mehr rechts).
    // Der Weg nach oben und das Umsetzen zwischen den Durchflügen passieren OFF-SCREEN (unsichtbar).
    private IEnumerator Co_GravityDrops(Transform f, Vector3 endPos, Quaternion homeRot, ParticleSystem trail)
    {
        float z = f.position.z;
        float topVpY = 1f + Mathf.Max(0.1f, gravityPassOvershoot);
        float botVpY = -Mathf.Max(0.1f, gravityPassOvershoot);

        Vector2 topLeftVp  = new Vector2(0.5f - gravityPassSpread, topVpY);
        Vector2 botLeftVp  = new Vector2(0.5f - gravityPassSpread, botVpY);
        Vector2 topRightVp = new Vector2(0.5f + gravityPassSpread, topVpY);
        Vector2 botRightVp = new Vector2(0.5f + gravityPassSpread, botVpY);

        Vector3 topLeft  = ViewportToWorldAtZ(topLeftVp,  z);
        Vector3 botLeft  = ViewportToWorldAtZ(botLeftVp,  z);
        Vector3 topRight = ViewportToWorldAtZ(topRightVp, z);
        Vector3 botRight = ViewportToWorldAtZ(botRightVp, z);

        // Direkt von der Startposition nach oben aus dem Bild raus.
        Vector3 startExit = ViewportToWorldAtZ(new Vector2(
            _cam != null ? _cam.WorldToViewportPoint(f.position).x : 0.5f, topVpY), z);
        yield return Co_FlySegment(f, f.position, startExit, gravityPassDuration * 0.75f, faceVelocity: true);
        f.position = topLeft;                       // off-screen umsetzen (unsichtbar)

        if (trail != null)
        {
            try
            {
                trail.Play();
                var em = trail.emission;
                em.enabled = false;   // startet off-screen — erst per SetTrailVisible() an, sobald sichtbar
            }
            catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
        }

        // Durchflug 1 (links) + 2 (rechts): von oben nach unten, dabei um die eigene Y-Achse rotierend.
        // Das Umsetzen zwischen den beiden Durchflügen ist off-screen und sofort (kein Zwischenstopp) —
        // fühlt sich dadurch wie EIN durchgehender Doppel-Durchflug an, kaum Pause dazwischen.
        float spin = 0f;
        var passes = new[] { (from: topLeft, to: botLeft, next: topRight), (from: topRight, to: botRight, next: (Vector3?)null) };
        foreach (var pass in passes)
        {
            Vector3 passDir = (pass.to - pass.from).normalized;   // konstant senkrecht nach unten
            float t = 0f;
            while (t < gravityPassDuration)
            {
                t += Time.deltaTime;
                // Linear statt SmoothStep: konstante Geschwindigkeit über den GANZEN Durchflug, kein
                // Abbremsen am Ende von Pass 1 / Beschleunigen am Anfang von Pass 2 — sonst wirkt der
                // (unsichtbare) Sprung dazwischen trotzdem wie eine Pause.
                float p = Mathf.Clamp01(t / gravityPassDuration);
                f.position = Vector3.Lerp(pass.from, pass.to, p);
                spin += gravitySpinSpeed * Time.deltaTime;
                // Kopf voran (Euler-Y = 0) + Eigenrotation um die lokale Y-Achse
                f.rotation = HeadFirstRotation(passDir) * Quaternion.AngleAxis(spin, Vector3.up);
                SetTrailVisible(trail, f.position);
                yield return null;
            }
            if (pass.next.HasValue) f.position = pass.next.Value;   // off-screen umsetzen (unsichtbar)
        }

        // Off-screen unter den Standardplatz, dann hoch an den Platz — mit sauberem Rotations-Settle.
        Vector3 belowHome = ViewportToWorldAtZ(new Vector2(
            _cam != null ? _cam.WorldToViewportPoint(endPos).x : 0.5f, botVpY), z);
        f.position = belowHome;
        yield return Co_FlyAndSettle(f, belowHome, endPos, (belowHome + endPos) * 0.5f, homeRot, gravityPassDuration * 0.6f, trail: trail);
    }

    // Fountain / blau: erst seitlich raus in den Hintergrund (schrumpft), dann 2x "Bogenlampe" — von
    // hinten-seitlich-klein in einer Kurve nach vorn-unten-groß aus dem Bild raus (abwechselnde Seite),
    // dabei um die eigene Y-Achse rotierend — dann hoch an den (mittigen) Platz.
    private IEnumerator Co_FountainArcs(Transform f, Vector3 endPos, Quaternion homeRot, Vector3 homeScale, ParticleSystem trail)
    {
        float z = f.position.z;
        Vector3 backScale = homeScale * fountainBackScale;
        float bottomVpY   = -0.35f;

        float side = f.position.x >= 0f ? 1f : -1f;
        Vector3 backSidePos = ViewportToWorldAtZ(new Vector2(side > 0f ? 1.3f : -0.3f, 0.6f), z);

        // Phase 0: seitlich raus in den Hintergrund — schrumpft dabei auf fountainBackScale. Läuft
        // bewusst OHNE Partikelspur: das ist der unsichtbare Teil, in dem sie "verschwindet".
        yield return Co_FlySegmentScaled(f, f.position, backSidePos, homeScale, backScale, fountainBackExitDuration);

        if (trail != null)
        {
            try
            {
                trail.Play();
                var em = trail.emission;
                em.enabled = false;   // Start (arcFrom) ist noch off-screen — erst per SetTrailVisible() an
            }
            catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
        }

        float spin = 0f;
        Vector3 lastPos = backSidePos;

        for (int arc = 0; arc < 2; arc++)
        {
            Vector3 arcFrom = ViewportToWorldAtZ(new Vector2(side > 0f ? 1.3f : -0.3f, 0.6f), z);
            Vector3 arcTo   = ViewportToWorldAtZ(new Vector2(0.5f - side * 0.18f, bottomVpY), z);
            // Kontrollpunkt HÖHER als Start- und Endpunkt → echtes "umgekehrtes U" (erst hoch, dann
            // nach vorn/unten raus), nicht nur eine flache Diagonale.
            Vector3 ctrl    = ViewportToWorldAtZ(new Vector2(0.5f - side * 0.55f, fountainArcPeakVpY), z);
            ctrl.z = z;

            f.position   = arcFrom;   // off-screen an den Bogen-Start (unsichtbar, klein/hinten)
            f.localScale = backScale;

            float t = 0f;
            while (t < fountainArcDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / fountainArcDuration);
                Vector3 q1 = Vector3.Lerp(arcFrom, ctrl, p);
                Vector3 q2 = Vector3.Lerp(ctrl, arcTo, p);
                f.position   = Vector3.Lerp(q1, q2, p);
                // Wächst von "hinten klein" zu "vorn groß" — verstärkt den Tiefen-Eindruck der Kurve.
                f.localScale = Vector3.Lerp(backScale, homeScale, Mathf.SmoothStep(0f, 1f, p));

                spin += fountainSpinSpeed * Time.deltaTime;
                // Analytische Bézier-Tangente (glatt) → Kopf voran, dazu Eigenrotation um die lokale Y-Achse.
                Vector3 tangent = 2f * (1f - p) * (ctrl - arcFrom) + 2f * p * (arcTo - ctrl);
                f.rotation = HeadFirstRotation(tangent) * Quaternion.AngleAxis(spin, Vector3.up);
                SetTrailVisible(trail, f.position);   // Blasen nur, solange sie WIRKLICH im Bild ist
                yield return null;
            }
            f.position   = arcTo;
            f.localScale = homeScale;
            lastPos = arcTo;
            side = -side;   // zweiter Bogen von der anderen Seite hinten rein
        }

        // Letzter Weg: von unten hoch an den Platz, sauberer Settle.
        yield return Co_FlyAndSettle(f, lastPos, endPos, new Vector3((lastPos.x + endPos.x) * 0.5f, endPos.y, z),
            homeRot, fountainArcDuration * 0.8f, trail: trail);
        f.localScale = homeScale;
    }

    // Gerade Strecke a→b in `dur` Sekunden (SmoothStep), Skalierung parallel dazu, weich aus der
    // Idle-Rotation eindrehend in Flugrichtung. Für den seitlichen Rausflug in den Hintergrund.
    private IEnumerator Co_FlySegmentScaled(Transform f, Vector3 a, Vector3 b, Vector3 scaleFrom, Vector3 scaleTo, float dur)
    {
        Quaternion startRot = f.rotation;
        Vector3 prevPos = f.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float rp = Mathf.Clamp01(t / dur);
            float p  = Mathf.SmoothStep(0f, 1f, rp);
            f.position   = Vector3.Lerp(a, b, p);
            f.localScale = Vector3.Lerp(scaleFrom, scaleTo, p);
            Quaternion rot = Quaternion.Slerp(startRot, FlightRotation(f.position - prevPos, f.rotation), FlightWeightIn(rp));
            SlerpTo(f, rot, turnSpeed);
            prevPos = f.position;
            yield return null;
        }
        f.position   = b;
        f.localScale = scaleTo;
    }

    // Gerade Strecke a→b in `dur` Sekunden (SmoothStep), optional in Flugrichtung schauend.
    private IEnumerator Co_FlySegment(Transform f, Vector3 a, Vector3 b, float dur, bool faceVelocity)
    {
        Vector3 prevPos = f.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            f.position = Vector3.Lerp(a, b, p);
            if (faceVelocity) SlerpTo(f, FlightRotation(f.position - prevPos, f.rotation), turnSpeed);
            prevPos = f.position;
            yield return null;
        }
        f.position = b;
    }

    private IEnumerator Co_SimpleApproach(Transform f, Vector3 center, Quaternion homeRot)
    {
        Vector3 startPos = f.position;
        Vector3 target   = new Vector3(center.x, center.y, startPos.z);
        yield return Co_FlyAndSettle(f, startPos, target, (startPos + target) * 0.5f, homeRot, introDuration);
        f.position = target;
        f.rotation = homeRot;
    }

    private IEnumerator Co_SideFairyExit(Transform f, Quaternion homeRot)
    {
        Vector3 startPos = f.position;
        float vpX        = _cam != null ? _cam.WorldToViewportPoint(startPos).x : 0.5f;
        Vector3 exitPos  = ViewportToWorldAtZ(new Vector2(vpX, sideFairyExitViewportY), startPos.z);

        Vector3 prevPos = f.position;
        float t = 0f;
        while (t < sideFairyExitDuration)
        {
            t += Time.deltaTime;
            float rp = Mathf.Clamp01(t / sideFairyExitDuration);
            f.position = Vector3.Lerp(startPos, exitPos, Mathf.SmoothStep(0f, 1f, rp));

            // Nur weich AUS der Idle-Rotation eindrehen — sie bleibt danach off-screen in Flughaltung.
            Quaternion rot = Quaternion.Slerp(homeRot, FlightRotation(f.position - prevPos, f.rotation), FlightWeightIn(rp));
            SlerpTo(f, rot, turnSpeed);
            prevPos = f.position;
            yield return null;
        }
        f.position = exitPos;
    }

    // ── Outro ────────────────────────────────────────────────────────────────

    private void HandleModeEnded(SpecialMode mode) => PlaySpecialModeOutro(mode);

    /// <summary>Alle Feen zurück an ihre Ausgangsposition, alle wieder Idle_Fly.
    /// Die Lead-Fee fliegt dabei erst seitlich aus dem Bild und dann von unten hoch an ihren Platz.</summary>
    public void PlaySpecialModeOutro(SpecialMode mode = SpecialMode.None)
    {
        if (_cam == null) _cam = Camera.main;
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Co_Outro(mode));
    }

    private IEnumerator Co_Outro(SpecialMode mode)
    {
        _outroActive = true;
        int lead = LeadIndex(mode);

        int remaining = 0;
        for (int i = 0; i < fairies.Length; i++)
        {
            if (fairies[i] == null || fairies[i].fairy == null) continue;
            SetSpecialBool(fairies[i].animator, false);
            fairies[i].glow?.SetOutlineGlow(false);   // Leucht-Umrandung wieder aus
            remaining++;
            int ci = i;

            bool alreadyHome = Vector3.Distance(fairies[ci].fairy.position, _homePos[ci]) < 0.15f;
            if (ci == lead && alreadyHome)
                StartCoroutine(Co_SettleInPlace(ci, () => remaining--));   // z.B. pink: steht schon am Platz
            else if (ci == lead && mode == SpecialMode.Fountain)
                StartCoroutine(Co_DirectReturnHome(ci, () => remaining--)); // blau: einfach direkt an den Platz
            else if (ci == lead)
                StartCoroutine(Co_LeadReturnHome(ci, () => remaining--));
            else
                StartCoroutine(Co_FairyReturnHome(ci, () => remaining--));
        }

        while (remaining > 0) yield return null;
        _outroActive = false;
        _running = null;
    }

    // Fee steht schon an ihrem Platz (z.B. pink nach Gravity): nicht mehr rausfliegen, nur die
    // Rotation/Größe weich in die Idle-Haltung zurückbringen.
    private IEnumerator Co_SettleInPlace(int i, Action onDone)
    {
        var f = fairies[i].fairy;
        Quaternion fromRot   = f.rotation;
        Vector3    fromScale = f.localScale;
        Quaternion toRot     = _homeRot[i];
        Vector3    toScale    = _homeScale[i];

        float dur = Mathf.Max(0.3f, rotationSettleTime);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            f.rotation   = Quaternion.Slerp(fromRot, toRot, p);
            f.localScale = Vector3.Lerp(fromScale, toScale, p);
            f.position   = _homePos[i];
            yield return null;
        }
        f.rotation   = toRot;
        f.localScale = toScale;
        f.position   = _homePos[i];
        onDone?.Invoke();
    }

    // Direkter Rückflug ohne seitlich raus/unten rein — einfach von der aktuellen Position (mittiger
    // Platz nach den Fountain-Bögen) auf direktem Weg zum eigenen Heimatplatz.
    private IEnumerator Co_DirectReturnHome(int i, Action onDone)
    {
        var f = fairies[i].fairy;
        Vector3    fromPos   = f.position;
        Vector3    toPos     = _homePos[i];
        Quaternion toRot     = _homeRot[i];
        Vector3    toScale   = _homeScale[i];

        // Etwas schneller als der normale Outro — die Feen müssen nicht synchron ankommen, sie
        // wartet danach einfach im Idle-Flug auf die anderen. Gerade rein mit dem Rücken zum Spieler,
        // dreht sich erst kurz vor der Ankunft zur Ansicht Richtung Spieler.
        yield return Co_FlyAndSettle(f, fromPos, toPos, (fromPos + toPos) * 0.5f, toRot, outroDuration * 0.6f, toRot * Quaternion.Euler(0f, 180f, 0f));

        f.position   = toPos;
        f.rotation   = toRot;
        f.localScale = toScale;
        onDone?.Invoke();
    }

    // Lead-Fee: erst seitlich aus dem Bild, dann (off-screen umgesetzt) von unten hoch an ihren Platz.
    private IEnumerator Co_LeadReturnHome(int i, Action onDone)
    {
        var f = fairies[i].fairy;
        float z            = f.position.z;
        Vector3    homePos = _homePos[i];
        Quaternion homeRot = _homeRot[i];
        Vector3    homeScale = _homeScale[i];

        // ── Phase 1: seitlich raus (Richtung der Heimat-Seite), dabei wieder auf volle Größe ──
        Vector3 fromPos   = f.position;
        Vector3 fromScale = f.localScale;
        float side = homePos.x >= 0f ? 1f : -1f;
        Vector3 sideExit = ViewportToWorldAtZ(new Vector2(side > 0f ? 1.35f : -0.35f, 0.5f), z);

        float p1T = outroDuration * 0.45f;
        Vector3 prevPos = f.position;
        float t = 0f;
        while (t < p1T)
        {
            t += Time.deltaTime;
            float rp = Mathf.Clamp01(t / p1T);
            float p  = Mathf.SmoothStep(0f, 1f, rp);
            f.position   = Vector3.Lerp(fromPos, sideExit, p);
            f.localScale = Vector3.Lerp(fromScale, homeScale, p);
            // Weich aus der Idle-Rotation eindrehen (sie steht davor frontal in der Mitte).
            Quaternion rot = Quaternion.Slerp(homeRot, FlightRotation(f.position - prevPos, f.rotation), FlightWeightIn(rp));
            SlerpTo(f, rot, turnSpeed);
            prevPos = f.position;
            yield return null;
        }

        // ── Off-screen umsetzen: unter den unteren Bildrand, an die X-Position der Heimat ──
        float homeVpX = _cam != null ? _cam.WorldToViewportPoint(homePos).x : 0.5f;
        Vector3 belowPos = ViewportToWorldAtZ(new Vector2(homeVpX, -0.35f), z);
        f.position   = belowPos;
        f.localScale = homeScale;

        // ── Phase 2: von unten hoch an den Heimatplatz — gerade, mit dem Rücken zum Spieler, erst
        // kurz vor der Ankunft (Settle-Teil) zur Ansicht Richtung Spieler gedreht ──
        float p2T = outroDuration - p1T;
        Vector3 belowCtrl = (belowPos + homePos) * 0.5f;
        yield return Co_FlyAndSettle(f, belowPos, homePos, belowCtrl, homeRot, p2T, homeRot * Quaternion.Euler(0f, 180f, 0f));

        f.position   = homePos;
        f.rotation   = homeRot;
        f.localScale = homeScale;
        onDone?.Invoke();
    }

    private IEnumerator Co_FairyReturnHome(int i, Action onDone)
    {
        var f = fairies[i].fairy;

        Vector3    fromPos   = f.position;
        Vector3    fromScale = f.localScale;
        Vector3    toPos     = _homePos[i];
        Quaternion toRot     = _homeRot[i];
        Vector3    toScale    = _homeScale[i];

        // Gerade Strecke (kein seitlicher Kurven-Versatz mehr) — die Fee kommt einfach gerade von
        // unten wieder rein, mit dem Rücken zum Spieler, und dreht sich erst kurz vor der Ankunft
        // (im Settle-Teil von Co_FlyAndSettle) sauber zur Ansicht Richtung Spieler.
        Vector3 control = (fromPos + toPos) * 0.5f;
        Quaternion backFacingRot = toRot * Quaternion.Euler(0f, 180f, 0f);

        // Skalierung parallel zum Flug hochfahren
        StartCoroutine(Co_LerpScale(f, fromScale, toScale, outroDuration));
        yield return Co_FlyAndSettle(f, fromPos, toPos, control, toRot, outroDuration, backFacingRot);

        f.position   = toPos;
        f.rotation   = toRot;
        f.localScale = toScale;
        onDone?.Invoke();
    }

    // Fliegt f über eine quadratische Bézier von->to (control), dreht dabei in Flugrichtung — und geht
    // über die letzten `rotationSettleTime` Sekunden per REINER, direkt SmoothStep-geglätteter Slerp
    // in `homeRot` über (kein SlerpTo-Chase, kein geschwindigkeitsabhängiges Ziel → kein Springen).
    // travelRot: falls gesetzt, wird während des Flug-Teils (t < travelT) auf DIESE feste Rotation
    // eingedreht statt auf die geschwindigkeitsabhängige Flug-Haltung (FlightRotation) — für die
    // Outro-Rückflüge: gerade mit dem Rücken zum Spieler rein, statt seitlich "liegend" gebankt.
    private IEnumerator Co_FlyAndSettle(Transform f, Vector3 fromPos, Vector3 toPos, Vector3 control,
                                        Quaternion homeRot, float duration, Quaternion? travelRot = null,
                                        ParticleSystem trail = null)
    {
        float settleT = Mathf.Min(rotationSettleTime, duration * 0.85f);
        float travelT = duration - settleT;

        Vector3 prevPos = f.position;
        Quaternion settleFrom = f.rotation;
        bool captured = false;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            Vector3 a = Vector3.Lerp(fromPos, control, p);
            Vector3 b = Vector3.Lerp(control, toPos, p);
            f.position = Vector3.Lerp(a, b, p);

            if (t < travelT)
            {
                Quaternion target = travelRot ?? FlightRotation(f.position - prevPos, f.rotation);
                SlerpTo(f, target, turnSpeed);
            }
            else
            {
                if (!captured) { settleFrom = f.rotation; captured = true; }
                float sp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - travelT) / settleT));
                f.rotation = Quaternion.Slerp(settleFrom, homeRot, sp);
            }
            if (trail != null) SetTrailVisible(trail, f.position);
            prevPos = f.position;
            yield return null;
        }
        f.position = toPos;
        f.rotation = homeRot;
    }

    // Blendet die Emission einer Partikelspur je Frame an/aus, je nachdem ob `worldPos` gerade
    // wirklich im sichtbaren Kamerabereich liegt — verhindert Partikel während unsichtbarer
    // Off-Screen-Flugabschnitte (z.B. der Weg in den Hintergrund oder Start/Ende eines Bogens).
    private void SetTrailVisible(ParticleSystem trail, Vector3 worldPos)
    {
        if (trail == null) return;
        try
        {
            var em = trail.emission;
            // Hysterese statt einer einzigen Schwelle: zum EINSCHALTEN muss die Fee wirklich im
            // sichtbaren Bereich sein (enger Rand) — sonst würden Blasen/Steine/Blätter sichtbar
            // außerhalb des Bildschirms emittieren (z.B. beim ersten Hochfliegen aus dem Bild raus).
            // Zum AUSSCHALTEN reicht ein kleiner zusätzlicher Puffer, der nur das Rand-Flackern bei
            // Bewegungen exakt an der Kante verhindert (kein großzügiger Off-Screen-Bereich mehr).
            bool visible = IsOnScreen(worldPos, em.enabled);
            if (em.enabled != visible) em.enabled = visible;
        }
        catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren, Flug läuft weiter */ }
    }

    private bool IsOnScreen(Vector3 worldPos, bool currentlyVisible)
    {
        if (_cam == null) return true;
        Vector3 vp = _cam.WorldToViewportPoint(worldPos);
        float margin = currentlyVisible ? 0.08f : -0.03f; // Aus-Schwelle etwas großzügiger als Ein-Schwelle
        return vp.x > -margin && vp.x < 1f + margin && vp.y > -margin && vp.y < 1f + margin;
    }

    private IEnumerator Co_LerpScale(Transform f, Vector3 from, Vector3 to, float duration)
    {
        float t = 0f;
        while (t < duration && f != null)
        {
            t += Time.deltaTime;
            f.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        if (f != null) f.localScale = to;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Ziel-Rotation für den Flug: Blick primär in die Tiefe (Hintergrund, +Z) plus Anteil der
    // XY-Flugrichtung, dazu um flightLeanDegrees flach "liegend" gekippt. Nur die Zielrotation — das
    // Glätten macht SlerpTo. Overload ohne fallback nur für garantiert gültige Richtungen.
    private static readonly Vector3 BackgroundDir = Vector3.forward; // +Z = weg von der Kamera / in den Hintergrund

    private Quaternion FlightRotation(Vector3 direction) => FlightRotation(direction, Quaternion.identity, 1f);
    private Quaternion FlightRotation(Vector3 direction, Quaternion fallback) => FlightRotation(direction, fallback, 1f);

    // Kopf voran in Flugrichtung, Gesicht grundsätzlich zur Kamera; die Drehung ist reine Rotation in
    // der Bildebene (um die Blickachse), d.h. sie ändert sich STETIG mit der Flugrichtung — nie ein
    // Sprung, egal wie sich die Richtung dreht. Für die pinke/blaue Fee (Durchflüge / Bögen).
    private Quaternion HeadFirstRotation(Vector3 heading)
    {
        if (heading.sqrMagnitude < 1e-7f) return _choreoHomeRot;
        heading.Normalize();
        // Winkel von "Kopf oben" zur Flugrichtung, um die Blickachse (world +Z) gedreht.
        // -X, weil AngleAxis(+, +Z) gegen den Uhrzeigersinn dreht: Flug nach rechts (+X) → Kopf nach rechts.
        float roll = -Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
        return Quaternion.AngleAxis(roll, Vector3.forward) * _choreoHomeRot;
    }

    // dynamicsScale skaliert die schnell wechselnden Neigungs-Anteile (v.a. Sinkflug-Boost) herunter —
    // im engen Strudel-Zentrum auf ~0 gesetzt, damit die Haltung dort nicht mehr pro Umdrehung pumpt.
    private Quaternion FlightRotation(Vector3 direction, Quaternion fallback, float dynamicsScale)
    {
        if (direction.sqrMagnitude < 1e-7f) return fallback;

        Vector3 heading = direction.normalized;

        // Fast senkrechter Flug (pinke Fee, Durchflüge): Kopf-voran-Haltung (siehe HeadFirstRotation).
        if (Mathf.Abs(heading.y) > 0.8f)
        {
            return HeadFirstRotation(heading);
        }

        Vector3 faceDir = Vector3.Slerp(heading, BackgroundDir, Mathf.Clamp01(backgroundFacing)).normalized;

        Vector3 up = Vector3.up;
        if (Vector3.Cross(faceDir, up).sqrMagnitude < 1e-4f) up = -BackgroundDir; // Degenerations-Schutz

        // Beim Sinkflug (heading.y < 0) zusätzlich die Nase nach unten kippen — nur bei großen Bögen (dynamicsScale).
        float diveBoost = Mathf.Clamp01(-heading.y) * diveLeanBoostDegrees * Mathf.Clamp01(dynamicsScale);

        Quaternion look = Quaternion.LookRotation(faceDir, up);
        Quaternion lie  = Quaternion.AngleAxis(flightLeanDegrees + diveBoost, Vector3.right);
        return look * lie * Quaternion.Euler(faceDirModelOffsetEuler);
    }

    // Framerate-unabhängiges, weiches Eindrehen in die Zielrotation.
    private void SlerpTo(Transform f, Quaternion target, float rate)
    {
        if (rate <= 0f) return;
        f.rotation = Quaternion.Slerp(f.rotation, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
    }

    // Blend-Gewicht Idle-Rotation ↔ Flughaltung über eine Flugphase (rp = 0..1):
    //  FlightWeightIn → 0 am Anfang der Phase, 1 danach — weiches Eindrehen aus der Idle-Haltung.
    private float FlightWeightIn(float rp)
        => rotationEaseIn > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rp / rotationEaseIn)) : 1f;

    private void SetSpecialBool(Animator a, bool value)
    {
        if (a == null) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == specialModeBool)
            {
                a.SetBool(specialModeBool, value);
                return;
            }
    }

    private Vector3 ScreenCenterAtZ(float z)
    {
        // Orthographische Kamera bei (0,0,-10): Bildmitte ist immer x=0,y=0.
        if (_cam != null && !_cam.orthographic)
        {
            float d = Mathf.Abs(_cam.transform.position.z - z);
            var c = _cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, d));
            c.z = z;
            return c;
        }
        return new Vector3(0f, 0f, z);
    }

    private float HalfHeightWorld()
    {
        return _cam != null && _cam.orthographic ? _cam.orthographicSize : 5f;
    }

    // Kleinere halbe Bildschirmseite in Weltkoordinaten (bei Hochformat = halbe Breite). Damit passt
    // eine kreisförmige Spirale mit diesem Radius komplett ins Bild.
    private float ScreenHalfMinWorld()
    {
        if (_cam == null) return 2.3f;
        float halfH = _cam.orthographic ? _cam.orthographicSize : 5f;
        float halfW = halfH * _cam.aspect;
        return Mathf.Min(halfH, halfW);
    }

    private Vector3 ViewportToWorldAtZ(Vector2 vp, float z)
    {
        if (_cam == null) return new Vector3(0f, 0f, z);
        float d = Mathf.Abs(_cam.transform.position.z - z);
        var w = _cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, d));
        w.z = z;
        return w;
    }
}
