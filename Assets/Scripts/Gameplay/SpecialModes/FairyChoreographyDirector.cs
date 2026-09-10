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

    [Header("Nebenfeen (raus nach unten)")]
    [SerializeField] private float sideFairyExitDuration = 1.2f;
    [Tooltip("Ziel-Viewport-Y der Nebenfeen (< 0 = unter dem Bildrand).")]
    [SerializeField] private float sideFairyExitViewportY = -0.35f;

    [Header("Outro")]
    [SerializeField] private float outroDuration = 3f;
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
            _homePos[i]   = e.fairy.position;
            _homeRot[i]   = e.fairy.rotation;
            _homeScale[i] = e.fairy.localScale;
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

        SetSpecialBool(anim, true);

        if (mode == SpecialMode.Vortex)
            yield return Co_VortexSpiral(f, center, homeRot, homeScale);
        else
            yield return Co_SimpleApproach(f, center, homeRot);

        f.position   = center;
        f.rotation   = homeRot;
        SetSpecialBool(anim, false);

        if (SpecialModeManager.Instance != null && !SpecialModeManager.Instance.IsModeActive)
            SpecialModeManager.Instance.StartMode(mode);

        _running = null;
    }

    private IEnumerator Co_VortexSpiral(Transform f, Vector3 center, Quaternion homeRot, Vector3 homeScale)
    {
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
            }
            else
            {
                if (!captured) { settleFrom = f.rotation; captured = true; }
                float sp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - travelT) / settleT));
                f.rotation = Quaternion.Slerp(settleFrom, homeRot, sp);
            }
            yield return null;
        }

        f.position   = new Vector3(center.x, center.y, z);
        f.rotation   = homeRot;
        f.localScale = homeScale * spiralEndScale;
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
        int lead = LeadIndex(mode);

        int remaining = 0;
        for (int i = 0; i < fairies.Length; i++)
        {
            if (fairies[i] == null || fairies[i].fairy == null) continue;
            SetSpecialBool(fairies[i].animator, false);
            remaining++;
            int ci = i;
            if (ci == lead)
                StartCoroutine(Co_LeadReturnHome(ci, () => remaining--));
            else
                StartCoroutine(Co_FairyReturnHome(ci, () => remaining--));
        }

        while (remaining > 0) yield return null;
        _running = null;
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

        // ── Phase 2: von unten hoch an den Heimatplatz (mit sauberem Rotations-Settle am Ende) ──
        float p2T = outroDuration - p1T;
        Vector3 belowCtrl = (belowPos + homePos) * 0.5f;
        yield return Co_FlyAndSettle(f, belowPos, homePos, belowCtrl, homeRot, p2T);

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

        Vector3 toTarget = toPos - fromPos;
        Vector3 perp     = new Vector3(-toTarget.y, toTarget.x, 0f).normalized
                           * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 control  = fromPos + toTarget * 0.5f + perp * outroCurveStrength;

        // Skalierung parallel zum Flug hochfahren
        StartCoroutine(Co_LerpScale(f, fromScale, toScale, outroDuration));
        yield return Co_FlyAndSettle(f, fromPos, toPos, control, toRot, outroDuration);

        f.position   = toPos;
        f.rotation   = toRot;
        f.localScale = toScale;
        onDone?.Invoke();
    }

    // Fliegt f über eine quadratische Bézier von->to (control), dreht dabei in Flugrichtung — und geht
    // über die letzten `rotationSettleTime` Sekunden per REINER, direkt SmoothStep-geglätteter Slerp
    // in `homeRot` über (kein SlerpTo-Chase, kein geschwindigkeitsabhängiges Ziel → kein Springen).
    private IEnumerator Co_FlyAndSettle(Transform f, Vector3 fromPos, Vector3 toPos, Vector3 control,
                                        Quaternion homeRot, float duration)
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
                SlerpTo(f, FlightRotation(f.position - prevPos, f.rotation), turnSpeed);
            }
            else
            {
                if (!captured) { settleFrom = f.rotation; captured = true; }
                float sp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - travelT) / settleT));
                f.rotation = Quaternion.Slerp(settleFrom, homeRot, sp);
            }
            prevPos = f.position;
            yield return null;
        }
        f.position = toPos;
        f.rotation = homeRot;
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

    // dynamicsScale skaliert die schnell wechselnden Neigungs-Anteile (v.a. Sinkflug-Boost) herunter —
    // im engen Strudel-Zentrum auf ~0 gesetzt, damit die Haltung dort nicht mehr pro Umdrehung pumpt.
    private Quaternion FlightRotation(Vector3 direction, Quaternion fallback, float dynamicsScale)
    {
        if (direction.sqrMagnitude < 1e-7f) return fallback;

        Vector3 heading = direction.normalized;                       // XY-Flugrichtung
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
