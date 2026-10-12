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
//      Gravity → pinke Fee: Co_GravityDrops an ihren eigenen Platz.
//      Fountain→ blaue Fee: Co_FountainGlide, ein leichter Bogen direkt an den mittigen Platz, ohne
//               Eigenrotation. Unterwegs spielt EINMAL die GetEnergy-Gebärde auf der maskierten
//               Arm-Ebene; in die Beschwörungs-Schleife wechselt sie erst beim Ankommen (IsCasting).
//      sonst   → direkter Anflug zur Mitte.
//  - Die anderen beiden Feen fliegen nach unten aus dem Bild.
//  - In der Mitte angekommen: Lead-Fee zurück auf Idle_Fly ("IsSpecialMode" = false), dann
//    SpecialModeManager.StartMode(mode) — ab hier läuft der eigentliche Special Mode (Elemente spawnen).
//
// Ablauf Outro (bei SpecialModeManager.OnModeEnded, ~outroDuration): alle drei Feen fliegen im Bogen
// zurück an ihre Ausgangsposition/-rotation/-größe, alle wieder Idle_Fly. Die blaue Fee nimmt dabei
// denselben leichten Bogen wie auf dem Hinweg (Co_DirectReturnHome), ohne Umdrehen, mit Gebärde.
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
        [Tooltip("Die komplette Special-Mode-Bewegung steckt bereits in der Blender-Animation " +
                 "(SpecialMode_Fly). Unity fliegt dann KEINE eigene Bahn mehr (keine Spirale, kein " +
                 "Anflug) und laesst die Fee an ihrem Platz stehen -- das Intro wartet nur noch " +
                 "introDuration ab. Pro Fee einzeln, weil nicht jede schon eine eigene Animation hat.")]
        public bool animationOnlyIntro;
        [System.NonSerialized] public FairyGlowFlash glow;
    }

    [SerializeField] private FairyEntry[] fairies = new FairyEntry[3];
    [SerializeField] private string specialModeBool = "IsSpecialMode";
    [Tooltip("Bool im Animator, das laeuft, solange der Special Mode aktiv ist. Darauf haengt die " +
             "Beschwoerungs-Schleife (Arme kreisen) -- sie beginnt erst, wenn die Fee in der Mitte " +
             "angekommen ist, und endet mit dem Modus. Feen ohne diesen Parameter ignorieren es.")]
    [SerializeField] private string castingBool = "IsCasting";

    [Header("Intro")]
    [SerializeField] private float introDuration = 5f;

    [Tooltip("Wartet vor dem Special-Mode-Intro, bis die GetEnergy-Animation der Lead-Fee " +
             "durchgelaufen ist. Ohne das bricht sie mitten im Faustschlag ab, weil der Trigger " +
             "schon beim 20. Element faellt, waehrend die Energiekugel noch zur Fee fliegt.")]
    [SerializeField] private bool waitForGetEnergy = true;
    [Tooltip("Name der maskierten Arm-Ebene im Animator.")]
    [SerializeField] private string armLayerName = "Arme";
    [Tooltip("Name des GetEnergy-Zustands auf dieser Ebene.")]
    [SerializeField] private string getEnergyStateName = "GetEnergy";
    [Tooltip("Wie lange hoechstens auf den START der GetEnergy-Animation gewartet wird " +
             "(die Energiekugel fliegt ~0.45s).")]
    [SerializeField] private float getEnergyStartTimeout = 1.2f;
    [Tooltip("Wie lange hoechstens auf das ENDE der GetEnergy-Animation gewartet wird.")]
    [SerializeField] private float getEnergyEndTimeout = 2.5f;
    [Tooltip("Name des Special-Mode-Zustands auf der Base Layer. Bei animationOnlyIntro wartet das " +
             "Intro, bis dieser Clip EINMAL durchgelaufen ist, statt stur introDuration abzuwarten.")]
    [SerializeField] private string specialModeStateName = "SpecialMode_Fly";
    [Tooltip("Anteil der Intro-Zeit, in dem die Lead-Fee kurz aus dem Bild raus fliegt, bevor die eigentliche Choreo beginnt.")]
    [Range(0.05f, 0.4f)]
    [SerializeField] private float leadExitPhase = 0.15f;

    [Header("Vortex-Anflug")]
    [Tooltip("Sekunden fuer den Flug von der Startposition in die Bildmitte. Die Fee ist dabei " +
             "bereits eingerollt -- das Einrollen selbst passiert vorher am Startplatz.")]
    [SerializeField] private float vortexApproachDuration = 2.5f;
    [Tooltip("Wie stark der Anflug gekruemmt ist, als Anteil der Streckenlaenge. 0 = schnurgerade, " +
             "0.25 = deutlich sichtbarer Bogen.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float vortexApproachCurve = 0.25f;
    [Tooltip("Zielgroesse der Fee in der Bildmitte, relativ zur Heimat-Groesse. Das Kleinerwerden " +
             "laesst sie nach hinten fliegen. Das Outro skaliert wieder auf Heimat-Groesse.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float spiralEndScale = 0.55f;

    [Tooltip("Dreht die eingekugelte Fee um ihre eigene Seitwaerts-Achse (Grad/Sek) -- der Salto " +
             "einer rollenden Kugel. Bewusst hier und nicht in Blender: der Flug dauert so lange " +
             "wie der Anflug eingestellt ist, nicht die feste Clip-Laenge; ein gebackenes Rollen " +
             "wuerde also mitten in der Drehung enden. 0 = kein Rollen.")]
    [SerializeField] private float vortexRollSpeed = 0f;
    [Tooltip("Anteil des Fluges, ueber den das Rollen anlaeuft bzw. am Ende wieder ausrollt. Ohne " +
             "das wuerde sie aus dem Stand losrotieren und am Ziel mitten in der Drehung einfrieren, " +
             "bevor sie sich wieder aufrollt.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float vortexRollRamp = 0.2f;
    [Tooltip("Knochen, um den sich die eingerollte Fee dreht. Der Wurzelpunkt des Fee-Objekts " +
             "liegt an den Fuessen -- wuerde man darum drehen, schleuderte der Koerper auf einem " +
             "Kreis von fast 80 cm herum, statt sich um sich selbst zu drehen.")]
    [SerializeField] private string rollPivotBone = "mixamorig:Hips";
    [Tooltip("Dreht die eingerollte Fee auf dem HINWEG mit dem Ruecken zum Spieler und beim " +
             "Ankommen wieder zurueck. Der Rueckweg fuehrt nach vorn zum Spieler, dort bleibt " +
             "sie durchgehend zugewandt.")]
    [SerializeField] private bool vortexFaceBackground = true;   // nur auf dem Hinweg
    [Tooltip("Sekunden, ueber die sie am Startplatz schon anrollt -- laeuft GLEICHZEITIG mit dem " +
             "Strecken und Einrollen. Sollte etwa so lang sein wie der Einroll-Clip.")]
    [SerializeField] private float vortexTurnBeforeFlight = 0.7f;
    [Tooltip("Sekunden fuer das Rausrollen nach unten aus dem Bild. Danach wird sie unsichtbar " +
             "unter dem Bildrand umgedreht und rollt von dort wieder herein.")]
    [SerializeField] private float vortexExitDuration = 0.7f;
    [Tooltip("Ab welcher Stelle des Einrollens sie schon anrollt. Sie steht dabei noch -- zu frueh " +
             "wirkt es wie eine zusaetzliche Rolle auf der Stelle. 0.85 = nur das letzte Stueck. " +
             "1 = gar nicht.")]
    [Range(0f, 1f)]
    [SerializeField] private float vortexRollStartInCurl = 1f;
    [Tooltip("Anteil des RUECKWEGS, nach dem das Rollen fertig ist. Der Flug bremst zum Schluss " +
             "ab -- rollt sie bis zuletzt weiter, dreht sie sich am Ende fast auf der Stelle.")]
    [Range(0.4f, 1f)]
    [SerializeField] private float vortexRollEndOnReturn = 0.8f;
    [Tooltip("Anteil des HINWEGS, nach dem das Rollen fertig ist. Danach gleitet sie nur noch in " +
             "die Mitte und dreht sich dabei sichtbar vom Hintergrund zum Spieler. Rollt sie bis " +
             "zuletzt, geht die Drehung im Ueberschlagen unter.")]
    [Range(0.4f, 1f)]
    [SerializeField] private float vortexRollEndOnApproach = 0.75f;
    [Tooltip("Sekunden fuer das Herumdrehen zum Spieler am Ende des Hinwegs. Beginnt nie frueher " +
             "als das Rollen aufhoert -- sonst ueberlagern sich beide und man sieht die Drehung nicht.")]
    [SerializeField] private float vortexTurnBackTime = 0.35f;
    [Tooltip("Sekunden VOR der Ankunft in der Mitte, ab denen sie sich schon aufklappt -- das " +
             "Aufrollen laeuft dann parallel zum Herumdrehen statt erst danach. Entspricht sinnvoll " +
             "etwa der Dauer des Herumdrehens. 0 = erst nach der Ankunft.")]
    [SerializeField] private float vortexUncurlLead = 0.35f;
    [Tooltip("Bool im Animator, das die Fee eingerollt haelt, solange sie zurueckfliegt. Ohne das " +
             "klappt der Einroll-Clip nach seinem Ende von selbst wieder in den Idle auf -- mitten " +
             "im Rueckflug. Feen ohne diesen Parameter ignorieren es.")]
    [SerializeField] private string curledBool = "IsCurled";

    [Tooltip("Sekunden, die das Einrollen am Modus-Ende dauert, bevor die Fee losfliegt -- Laenge " +
             "des Einroll-Clips (SpecialMode_Curl). Nur dann wartet das Outro, sonst fliegt sie " +
             "sofort los. 0 = kein Warten.")]
    [SerializeField] private float vortexCurlTime = 0f;
    [Tooltip("Name des GetEnergy-Ausloesers. Wird am Modus-Ende zurueckgesetzt.")]
    [SerializeField] private string getEnergyTrigger = "GetEnergy";
    [Tooltip("Name des Float-Parameters, der das Tempo der GetEnergy-Animation skaliert.")]
    [SerializeField] private string getEnergySpeedParam = "GetEnergySpeed";

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

    [Header("Fountain — blaue Fee (gleitet in einem leichten Bogen an den mittigen Platz)")]
    [Tooltip("Sekunden für das Hinübergleiten an den mittigen Platz.")]
    [SerializeField] private float fountainGlideDuration = 1.6f;
    [Tooltip("Wie hoch der Bogen über der direkten Verbindung liegt, in Welteinheiten. 0 = schnurgerade.")]
    [SerializeField] private float fountainGlideArcHeight = 0.9f;
    [Tooltip("Tempo-Faktor für die GetEnergy-Gebärde während des Hinübergleitens (skaliert das " +
             "Zustandstempo). Kleiner = langsamer, damit die Gebärde die ganze Strecke ausfüllt " +
             "statt nach der halben fertig zu sein.")]
    [Range(0.2f, 2f)]
    [SerializeField] private float fountainGlideGestureSpeed = 0.55f;

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
        SpecialModeManager.OnModeStarted += HandleModeStarted;
    }

    private void OnDestroy()
    {
        SpecialModeManager.OnModeEnded -= HandleModeEnded;
        SpecialModeManager.OnModeStarted -= HandleModeStarted;
    }

    // Der eigentliche Modus beginnt erst, wenn die Lead-Fee in der Mitte angekommen ist --
    // genau dort soll ihre Beschwoerungs-Schleife einsetzen.
    private void HandleModeStarted(SpecialMode mode)
    {
        int lead = LeadIndex(mode);
        if (lead >= 0) SetBool(fairies[lead].animator, castingBool, true);
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

        // Die GetEnergy-Animation zuerst zu Ende bringen. Der Trigger faellt bereits beim 20.
        // zerstoerten Element, die Energiekugel ist zu dem Zeitpunkt aber noch unterwegs zur Fee --
        // ohne dieses Warten wuerde der Faustschlag mittendrin abgeschnitten.
        if (lead >= 0 && waitForGetEnergy)
            yield return Co_WaitForGetEnergy(fairies[lead].animator);

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

        SetBool(anim, curledBool, false);    // Sicherheitsnetz, falls ein Outro abgebrochen wurde
        SetBool(anim, castingBool, false);
        SetSpecialBool(anim, true);
        fairies[lead].glow?.SetOutlineGlow(true);   // Leucht-Umrandung an fürs Special-Mode
        // Partikelspur: Play() NICHT hier generell — die einzelnen Choreo-Coroutinen starten sie erst
        // ab dem Moment, an dem die Fee tatsächlich SICHTBAR im Bild fliegt (nicht während sie unsichtbar
        // off-screen unterwegs ist, z.B. beim Reinfliegen in den Hintergrund).
        var trail = fairies[lead].specialModeTrail;

        if (fairies[lead].animationOnlyIntro)
        {
            // Die Bewegung kommt vollstaendig aus der Blender-Animation. Unity schiebt die Fee
            // deshalb nicht zusaetzlich durchs Bild -- sonst addieren sich beide Bewegungen.
            // Gewartet wird genau einen Durchlauf des Clips, nicht stur introDuration: so passt
            // sich das Intro automatisch an, wenn die Animation spaeter umgetimt wird.
            yield return Co_WaitForSpecialAnimationOnce(anim);
        }
        else
        {
            switch (mode)
            {
                case SpecialMode.Vortex:
                {
                    // Das Umdrehen passiert bewusst HINTER dem Bildrand: eine Drehung mitten im
                    // Bild sah mechanisch aus. Stattdessen rollt sie zum Spieler gerichtet nach
                    // unten raus, wird unsichtbar gewendet und kommt umgedreht wieder herein.
                    Vector3 startPos = f.position;
                    float[] roll = new float[1];

                    // 1) Einrollen am Platz, zum Spieler gerichtet -- gegen Ende rollt sie schon an.
                    Coroutine anrollen = vortexTurnBeforeFlight > 0f
                        ? StartCoroutine(Co_TurnInPlace(f, homeRot, 0f, vortexTurnBeforeFlight, -1f, roll))
                        : null;
                    yield return Co_WaitForSpecialAnimationOnce(anim);
                    if (anrollen != null) yield return anrollen;

                    // 2) Schraeg nach unten aus dem Bild rollen -- gleich auf die Hoehe der
                    // Bildmitte, damit danach kein Umsetzen noetig ist. Rollrichtung wie auf dem
                    // Rueckweg, sie bewegt sich hier ja ebenfalls nach unten.
                    float mitteX = _cam != null ? _cam.WorldToViewportPoint(center).x : 0.5f;
                    Vector3 unten = ViewportToWorldAtZ(new Vector2(mitteX, -0.4f), startPos.z);
                    yield return Co_CurledFlight(f, startPos, unten, homeScale, homeScale, homeRot,
                                                 unten - startPos, false, -1f, 0f,
                                                 roll[0], 1f, vortexExitDuration, roll);

                    // Kurz vor der Ankunft schon aufklappen, damit sich Aufrollen und
                    // Herumdrehen ueberlagern. Nur die ANIMATION laeuft dadurch frueher an --
                    // der eigentliche Modus startet weiterhin erst, wenn sie in der Mitte ist.
                    if (vortexUncurlLead > 0f)
                        StartCoroutine(Co_SetBoolAfter(anim, castingBool, true,
                            Mathf.Max(0f, vortexApproachDuration - vortexUncurlLead)));

                    // 3) Umgedreht wieder reinrollen, dabei in den Hintergrund schrumpfen. Sie
                    // bewegt sich jetzt nach OBEN -- die Rollrichtung muss deshalb andersherum
                    // sein als beim Rausrollen, sonst rollt die Kugel gegen ihre Flugrichtung.
                    yield return Co_CurledFlight(f, unten, center, homeScale,
                                                 homeScale * spiralEndScale, homeRot,
                                                 center - unten, vortexFaceBackground, 1f,
                                                 vortexFaceBackground ? 1f : 0f,
                                                 roll[0], vortexRollEndOnApproach,
                                                 vortexApproachDuration, roll);
                    break;
                }
                case SpecialMode.Gravity:  yield return Co_GravityDrops(f, endPos, homeRot, trail);      break;
                case SpecialMode.Fountain: yield return Co_FountainGlide(f, anim, endPos, homeRot, homeScale, trail); break;
                default:                   yield return Co_SimpleApproach(f, endPos, homeRot);          break;
            }

            f.position   = endPos;
            f.rotation   = homeRot;
        }
        SetSpecialBool(anim, false);
        // Stoppt nur das NEUE Emittieren — bereits ausgestoßene Blasen/Blätter/Steine klingen noch
        // natürlich aus, statt abrupt zu verschwinden. In try/catch: manche VFX-Assets (z.B. CFXR)
        // bringen eigene Auto-Destroy-Skripte mit, die das Objekt zerstören können, ohne dass unsere
        // Referenz das rechtzeitig mitbekommt (MissingReferenceException trotz "?."-Check) — das darf
        // NIEMALS den Start des eigentlichen Special Modes (StartMode unten) verhindern.
        try { fairies[lead].specialModeTrail?.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        catch (NullReferenceException) { }   // neuere Unity werfen das statt MissingReference
        catch (MissingReferenceException) { /* Trail-Objekt wurde extern zerstört — ignorieren */ }

        if (SpecialModeManager.Instance != null && !SpecialModeManager.Instance.IsModeActive)
            SpecialModeManager.Instance.StartMode(mode);

        _running = null;
    }

    // Wartet, bis die GetEnergy-Animation auf der maskierten Arm-Ebene gelaufen UND fertig ist.
    // Zwei Phasen, weil sie beim Aufruf meist noch gar nicht begonnen hat (die Energiekugel fliegt
    // noch). Beide Phasen haben ein Zeitlimit -- ein haengendes Intro waere schlimmer als ein
    // abgeschnittener Faustschlag.
    private IEnumerator Co_WaitForGetEnergy(Animator anim)
    {
        if (anim == null || anim.runtimeAnimatorController == null) yield break;

        int ebene = -1;
        for (int i = 0; i < anim.layerCount; i++)
            if (anim.GetLayerName(i) == armLayerName) { ebene = i; break; }
        if (ebene < 0) yield break;

        bool Laeuft()
        {
            if (anim.IsInTransition(ebene)) return true;
            var st = anim.GetCurrentAnimatorStateInfo(ebene);
            return st.IsName(getEnergyStateName) && st.normalizedTime < 1f;
        }

        float t = 0f;
        while (t < getEnergyStartTimeout && !Laeuft()) { t += Time.deltaTime; yield return null; }

        t = 0f;
        while (t < getEnergyEndTimeout && Laeuft()) { t += Time.deltaTime; yield return null; }
    }

    // Wartet einen vollstaendigen Durchlauf des Special-Mode-Clips auf der Base Layer ab.
    private IEnumerator Co_WaitForSpecialAnimationOnce(Animator anim)
    {
        if (anim == null || anim.runtimeAnimatorController == null)
        { yield return new WaitForSeconds(introDuration); yield break; }

        float t = 0f;
        while (t < introDuration)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (!anim.IsInTransition(0) && st.IsName(specialModeStateName)) break;
            t += Time.deltaTime; yield return null;
        }

        float grenze = introDuration * 2f;
        t = 0f;
        while (t < grenze)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsName(specialModeStateName) && st.normalizedTime >= 1f) break;
            t += Time.deltaTime; yield return null;
        }
    }

    // Vortex / gruen: die bereits eingerollte Fee fliegt in einer leichten Kurve in die Bildmitte
    // und wird dabei kleiner -- sie wirkt dadurch, als flöge sie nach hinten. Die Schauwirkung kommt
    // nicht mehr aus der Flugbahn, sondern aus dem schnellen Rollen um die eigene Achse.
    // Vortex / gruen: die eingerollte Fee fliegt in einer leichten Kurve zwischen Startplatz und
    // Bildmitte, waehrend sie sich um ihre eigene Mitte ueberschlaegt. Wird fuer den Hinweg und den
    // Rueckweg benutzt -- mit derselben Kurvenreferenz fliegt sie exakt dieselbe Bahn zurueck.
    private IEnumerator Co_CurledFlight(Transform f, Vector3 von, Vector3 nach,
                                        Vector3 scaleVon, Vector3 scaleNach,
                                        Quaternion homeRot, Vector3 kurvenReferenz,
                                        bool wegdrehen, float rollRichtung, float drehungStart,
                                        float rollStartWinkel, float rollEndeAnteil,
                                        float dauerVorgabe, float[] rollAus)
    {
        von  = new Vector3(von.x,  von.y,  f.position.z);
        nach = new Vector3(nach.x, nach.y, f.position.z);

        // Kontrollpunkt seitlich der Verbindungslinie -> leichter Bogen statt gerader Strecke.
        // Die Seite haengt an kurvenReferenz und nicht an der Flugrichtung: nur so liegt der
        // Rueckweg auf demselben Bogen wie der Hinweg.
        Vector3 quer = new Vector3(-kurvenReferenz.y, kurvenReferenz.x, 0f);
        quer = quer.sqrMagnitude > 0.0001f ? quer.normalized : Vector3.up;
        Vector3 ctrl = (von + nach) * 0.5f + quer * ((nach - von).magnitude * vortexApproachCurve);

        float dauer   = Mathf.Max(0.3f, dauerVorgabe);

        Vector3 pivot = RollPivotLocal(f);
        Quaternion rueckenRot = homeRot * Quaternion.AngleAxis(180f, Vector3.up);

        // Sie rollt durchgehend bis zur Ankunft -- auch waehrend sie sich wieder zum Spieler dreht.
        // Damit sie am Ende trotzdem exakt aufrecht steht (der Aufroll-Clip setzt das voraus), wird
        // die Gesamtdrehung auf eine GANZE Zahl von Umdrehungen gerundet. Eine volle Umdrehung ist
        // dieselbe Lage wie gar keine, also landet sie punktgenau, ohne zurueckgedreht zu werden.
        float drehRamp = Mathf.Clamp(vortexRollRamp, 0.01f, 0.9f);
        // Laeuft die Drehung schon aus dem Einrollen heraus, braucht sie keinen zweiten Anlauf.
        float ramp = rollStartWinkel > 0f ? 0.02f : drehRamp;
        float flaeche = 1f - ramp * 0.5f;                     // mittlere Geschwindigkeit beim Anlauf

        // Rollstrecke endet ggf. vor der Ankunft -- der Flug bremst zum Schluss ab, und sie soll
        // sich dort nicht noch auf der Stelle weiterdrehen.
        float rollEnde = Mathf.Clamp(rollEndeAnteil, 0.2f, 1f);
        float roh = vortexRollSpeed * dauer * rollEnde * flaeche;
        // Auf eine GANZE Zahl von Umdrehungen runden -- einschliesslich dessen, was beim Einrollen
        // schon gedreht wurde. Eine volle Umdrehung ist dieselbe Lage wie gar keine.
        float ziel   = Mathf.Max(360f, Mathf.Round((rollStartWinkel + roh) / 360f) * 360f);
        float gesamt = ziel - rollStartWinkel;

        // Anteil der Drehung, der bis zum Zeitpunkt u geschafft ist (Anlauf, danach gleichmaessig).
        float Fortschritt(float u)
        {
            if (u <= ramp)
            {
                float w = u / ramp;
                return ramp * (w * w * w - 0.5f * w * w * w * w) / flaeche;
            }
            return (ramp * 0.5f + (u - ramp)) / flaeche;
        }

        // Das Zurueckdrehen zum Spieler faengt fruehestens dort an, wo das Rollen aufhoert --
        // sonst ueberlagern sich Ueberschlag und Drehung und man sieht von der Drehung nichts.
        // Innerhalb dieses Fensters bestimmt vortexTurnBackTime, wie flott sie sich umdreht.
        float rueckAnteil = Mathf.Clamp(
            Mathf.Min(1f - rollEnde, vortexTurnBackTime / dauer), 0.05f, 0.9f);

        float t = 0f;
        while (t < dauer)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dauer);
            float p = Mathf.SmoothStep(0f, 1f, u);
            Vector3 a2 = Vector3.Lerp(von, ctrl, p);
            Vector3 b2 = Vector3.Lerp(ctrl, nach, p);
            Vector3 bahn = Vector3.Lerp(a2, b2, p);
            f.localScale = Vector3.Lerp(scaleVon, scaleNach, p);

            // Ruecken zum Spieler drehen, am Ende wieder zurueck.
            // Setzt dort an, wo die Drehung am Startplatz aufgehoert hat.
            float hin     = Mathf.Lerp(drehungStart, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / drehRamp)));
            float zurueck = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - (1f - rueckAnteil)) / rueckAnteil));
            Quaternion basis = wegdrehen
                ? Quaternion.Slerp(homeRot, rueckenRot, hin * (1f - zurueck))
                : homeRot;

            // Ueberschlag um die WAAGERECHTE BILDACHSE -- vorangestellt, damit die Rollrichtung
            // nicht davon abhaengt, wie herum die Fee gerade steht. rollRichtung dreht sie auf dem
            // Rueckweg um, damit die Kugel mit ihrer Flugrichtung rollt und nicht dagegen.
            float winkel = rollStartWinkel + gesamt * Fortschritt(Mathf.Clamp01(u / rollEnde));
            f.rotation = Quaternion.AngleAxis(winkel * rollRichtung, Vector3.right) * basis;
            SetzeUmDrehpunkt(f, bahn, homeRot, pivot);
            yield return null;
        }

        f.position   = nach;
        f.rotation   = homeRot;
        f.localScale = scaleNach;
        if (rollAus != null) rollAus[0] = rollStartWinkel + gesamt;
    }

    // Dreht die Fee auf der Stelle weg. Position und Groesse bleiben unveraendert -- bewegt wird
    // nur die Ausrichtung, und zwar um die Kugelmitte statt um den Fusspunkt. Laeuft parallel zum
    // Einroll-Clip, damit aus Einrollen und Umdrehen eine einzige Bewegung wird.
    // Dreht die Fee auf der Stelle weg und laesst sie gegen Ende schon anrollen. Position und
    // Groesse bleiben unveraendert, gedreht wird um die Kugelmitte statt um den Fusspunkt. Laeuft
    // parallel zum Einroll-Clip, damit daraus eine einzige Bewegung wird.
    private IEnumerator Co_TurnInPlace(Transform f, Quaternion homeRot, float anteil, float dauer,
                                       float rollRichtung, float[] rollAus)
    {
        rollAus[0] = 0f;
        if (dauer <= 0f) yield break;

        Quaternion rueckenRot = homeRot * Quaternion.AngleAxis(180f, Vector3.up);
        Vector3 pivot = RollPivotLocal(f);
        Vector3 bahn  = f.position;
        float ab = Mathf.Clamp01(vortexRollStartInCurl);
        float roll = 0f;

        float t = 0f;
        while (t < dauer)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dauer);

            // Das Rollen laeuft schon waehrend des Einrollens an, sobald die Kugel fast fertig ist.
            if (ab < 1f)
            {
                float g = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ab, 1f, u));
                roll += vortexRollSpeed * g * Time.deltaTime;
            }

            Quaternion basis = Quaternion.Slerp(homeRot, rueckenRot, Mathf.SmoothStep(0f, 1f, u) * anteil);
            f.rotation = Quaternion.AngleAxis(roll * rollRichtung, Vector3.right) * basis;
            SetzeUmDrehpunkt(f, bahn, homeRot, pivot);
            yield return null;
        }
        rollAus[0] = roll;
    }

    private IEnumerator Co_SetBoolAfter(Animator a, string name, bool wert, float nachSekunden)
    {
        if (nachSekunden > 0f) yield return new WaitForSeconds(nachSekunden);
        SetBool(a, name, wert);
    }

    private IEnumerator Co_ReleaseCurl(Animator a, float nachSekunden)
    {
        yield return new WaitForSeconds(nachSekunden);
        SetBool(a, curledBool, false);
    }

    // Rueckweg: dieselbe Bahn wie der Hinweg, nur andersherum -- eingerollt und rollend.
    private IEnumerator Co_CurledReturnHome(int i, Action onDone)
    {
        var f = fairies[i].fairy;
        Vector3    homePos   = _homePos[i];
        Quaternion homeRot   = _homeRot[i];
        Vector3    homeScale = _homeScale[i];

        // Erst einrollen lassen, dann losfliegen.
        if (vortexCurlTime > 0f) yield return new WaitForSeconds(vortexCurlTime);

        Vector3 von = f.position;

        // Sobald sie aufhoert zu rollen, klappt sie sich auch schon auf -- der Rest des Fluges
        // ist reines Ankommen. Vorher stand sie bis zuletzt als Kugel im Bild.
        StartCoroutine(Co_ReleaseCurl(fairies[i].animator,
            Mathf.Max(0.3f, vortexApproachDuration) * vortexRollEndOnReturn));

        // Auf dem Rueckweg kommt sie nach vorn zum Spieler -- da stimmt die Blickrichtung
        // bereits, sie muss sich weder weg- noch wieder zurueckdrehen.
        yield return Co_CurledFlight(f, von, homePos, f.localScale, homeScale, homeRot,
                                     von - homePos, false, -1f, 0f, 0f, vortexRollEndOnReturn,
                                     vortexApproachDuration, null);

        f.position   = homePos;
        f.rotation   = homeRot;
        f.localScale = homeScale;
        SetBool(fairies[i].animator, curledBool, false);   // Sicherheitsnetz
        onDone?.Invoke();
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
            catch (NullReferenceException) { }   // neuere Unity werfen das statt MissingReference
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
    /// <summary>Blaue Fee: gleitet in einem leichten, hohen Bogen direkt von ihrem Platz an den
    /// mittigen Platz. Keine Eigenrotation, keine Größenänderung, kein Umweg über den Hintergrund —
    /// sie bleibt durchgehend sichtbar und lässt die Blasen unter sich fallen. Ihre
    /// Beschwörungsanimation läuft dabei schon: der Animator steht beim Aufruf bereits auf
    /// SpecialMode_Cast, weil ihr Controller direkt aus dem Idle dorthin wechselt.</summary>
    private IEnumerator Co_FountainGlide(Transform f, Animator anim, Vector3 endPos, Quaternion homeRot,
                                         Vector3 homeScale, ParticleSystem trail)
    {
        Vector3 startPos = f.position;
        Vector3 target   = new Vector3(endPos.x, endPos.y, startPos.z);

        // Die GetEnergy-Gebaerde EINMAL waehrend des Hinuebergleitens. Sie laeuft auf der maskierten
        // Arm-Ebene, und das ist hier entscheidend: der GetEnergy-Clip traegt die ersten 36 Frames
        // der Schwebekurve und endet dadurch rund 36 cm hoeher, als er beginnt. Unmaskiert auf der
        // Base Layer abgespielt wuerde sie beim Uebergang in SpecialMode_Cast genau um diese Strecke
        // absacken. Maskiert kommt die Huefte weiter aus dem Idle, und es passiert nichts dergleichen.
        SetzeFloat(anim, getEnergySpeedParam, fountainGlideGestureSpeed);
        SetzeTrigger(anim, getEnergyTrigger);

        if (trail != null)
        {
            // Sie ist die ganze Strecke im Bild, die Blasen dürfen also sofort laufen.
            // SetTrailVisible in Co_FlyAndSettle regelt das Emittieren am Bildrand.
            try { trail.Play(); }
            catch (NullReferenceException) { }   // neuere Unity werfen das statt MissingReference
            catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
        }

        // Kontrollpunkt über der Mitte der Strecke → leichter, hoher Bogen statt gerader Linie.
        Vector3 ctrl = (startPos + target) * 0.5f;
        ctrl.y = Mathf.Max(startPos.y, target.y) + fountainGlideArcHeight;
        ctrl.z = startPos.z;

        f.localScale = homeScale;
        // travelRot: homeRot haelt sie aufrecht. Ohne das dreht Co_FlyAndSettle sie in Flugrichtung ein.
        yield return Co_FlyAndSettle(f, startPos, target, ctrl, homeRot, fountainGlideDuration,
                                     travelRot: homeRot, trail: trail);

        f.position   = target;
        f.rotation   = homeRot;
        f.localScale = homeScale;
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

    private void HandleModeEnded(SpecialMode mode)
    {
        int lead = LeadIndex(mode);
        if (lead >= 0)
        {
            SetBool(fairies[lead].animator, castingBool, false);
            // Waehrend des Modus laufen GetEnergy-Ausloeser auf, die nirgends verbraucht werden:
            // aus der Beschwoerungs-Schleife fuehrt auf der Base Layer kein Weg in den
            // GetEnergy-Zustand. Ohne dieses Zuruecksetzen feuert direkt nach dem Outro ein
            // nachtraeglicher Faustschlag.
            ResetTrigger(fairies[lead].animator, getEnergyTrigger);
            // Haelt den Einroll-Clip auf seinem letzten Bild stehen, bis sie zu Hause ist.
            if (mode == SpecialMode.Vortex)
                SetBool(fairies[lead].animator, curledBool, true);
        }
        PlaySpecialModeOutro(mode);
    }

    private static void SetzeTrigger(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
            { a.SetTrigger(name); return; }
    }

    private static void SetzeFloat(Animator a, string name, float wert)
    {
        if (a == null || a.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Float && p.name == name)
            { a.SetFloat(name, wert); return; }
    }

    private static void ResetTrigger(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
            { a.ResetTrigger(name); return; }
    }

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
            else if (ci == lead && mode == SpecialMode.Vortex)
                StartCoroutine(Co_CurledReturnHome(ci, () => remaining--));  // gruen: denselben Weg zurueck
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

    /// <summary>Blaue Fee zurück: derselbe leichte Bogen wie auf dem Hinweg, nur andersherum —
    /// ohne Eigenrotation, ohne Umdrehen, und unterwegs noch einmal die GetEnergy-Gebärde.
    /// Spiegelbild von Co_FountainGlide, damit Hin- und Rückweg gleich aussehen.</summary>
    private IEnumerator Co_DirectReturnHome(int i, Action onDone)
    {
        var f     = fairies[i].fairy;
        var anim  = fairies[i].animator;
        var trail = fairies[i].specialModeTrail;
        Vector3    fromPos = f.position;
        Vector3    toPos   = _homePos[i];
        Quaternion toRot   = _homeRot[i];
        Vector3    toScale = _homeScale[i];

        // Gebärde einmal auf der maskierten Arm-Ebene. Die Base Layer blendet parallel aus der
        // Beschwörung zurück ins Idle (IsCasting ist beim Outro schon false) — die Hüfte kommt
        // also wieder von dort, und die Gebärde legt sich sauber darüber.
        SetzeFloat(anim, getEnergySpeedParam, fountainGlideGestureSpeed);
        SetzeTrigger(anim, getEnergyTrigger);

        if (trail != null)
        {
            try { trail.Play(); }
            catch (NullReferenceException) { }   // neuere Unity werfen das statt MissingReference
            catch (MissingReferenceException) { /* extern zerstörtes VFX-Objekt — ignorieren */ }
        }

        Vector3 ctrl = (fromPos + toPos) * 0.5f;
        ctrl.y = Mathf.Max(fromPos.y, toPos.y) + fountainGlideArcHeight;

        // travelRot: toRot hält sie aufrecht und zum Spieler gewandt — kein Umdrehen mehr.
        yield return Co_FlyAndSettle(f, fromPos, toPos, ctrl, toRot, fountainGlideDuration,
                                     travelRot: toRot, trail: trail);

        if (trail != null)
        {
            // Nur das neue Emittieren stoppen, schon ausgestoßene Blasen klingen aus.
            try { trail.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            catch (NullReferenceException) { }
            catch (MissingReferenceException) { }
        }

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

        // Erst einrollen lassen, dann losfliegen -- der Heimweg soll im eingerollten Zustand
        // rollend passieren, genau wie der Hinweg.
        bool rollen = vortexRollSpeed != 0f;
        if (rollen && vortexCurlTime > 0f) yield return new WaitForSeconds(vortexCurlTime);
        Vector3 pivot = rollen ? RollPivotLocal(f) : Vector3.zero;

        float p1T = outroDuration * 0.45f;
        Vector3 prevPos = f.position;
        Quaternion basisRot = f.rotation;
        float rollWinkel = 0f;
        float t = 0f;
        while (t < p1T)
        {
            t += Time.deltaTime;
            float rp = Mathf.Clamp01(t / p1T);
            float p  = Mathf.SmoothStep(0f, 1f, rp);
            Vector3 bahn = Vector3.Lerp(fromPos, sideExit, p);
            f.localScale = Vector3.Lerp(fromScale, homeScale, p);
            // Weich aus der Idle-Rotation eindrehen (sie steht davor frontal in der Mitte).
            Quaternion rot = Quaternion.Slerp(homeRot, FlightRotation(bahn - prevPos, basisRot), FlightWeightIn(rp));
            if (rollen)
            {
                basisRot = Quaternion.Slerp(basisRot, rot, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                rollWinkel += vortexRollSpeed * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rp / vortexRollRamp))
                            * Time.deltaTime;
                f.rotation = Quaternion.AngleAxis(rollWinkel, Vector3.right) * basisRot;
                SetzeUmDrehpunkt(f, bahn, homeRot, pivot);
            }
            else
            {
                f.position = bahn;
                SlerpTo(f, rot, turnSpeed);
                basisRot = f.rotation;
            }
            prevPos = bahn;
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
        yield return Co_FlyAndSettle(f, belowPos, homePos, belowCtrl, homeRot, p2T,
            homeRot * Quaternion.Euler(0f, 180f, 0f), null,
            rollen ? vortexRollSpeed : 0f, rollWinkel, pivot);

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
                                        ParticleSystem trail = null,
                                        float rollSpeed = 0f, float rollStart = 0f,
                                        Vector3 rollPivot = default)
    {
        float settleT = Mathf.Min(rotationSettleTime, duration * 0.85f);
        float travelT = duration - settleT;

        Vector3 prevPos = f.position;
        Quaternion settleFrom = f.rotation;
        bool captured = false;
        // Eigenrotation der eingerollten Fee: wird getrennt von der Flughaltung gefuehrt, sonst
        // daempft die traege Eindreh-Slerp das Rollen weg. Das Zurueckdrehen macht die Settle-Phase.
        Quaternion basisRot = f.rotation;
        float rollWinkel = rollStart;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            Vector3 a = Vector3.Lerp(fromPos, control, p);
            Vector3 b = Vector3.Lerp(control, toPos, p);
            Vector3 bahn = Vector3.Lerp(a, b, p);
            f.position = bahn;

            if (t < travelT)
            {
                Quaternion target = travelRot ?? FlightRotation(f.position - prevPos, basisRot);
                if (rollSpeed != 0f)
                {
                    basisRot = Quaternion.Slerp(basisRot, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                    float aus = travelT > 0f ? Mathf.SmoothStep(0f, 1f,
                                   Mathf.Clamp01((1f - t / travelT) / Mathf.Max(0.01f, vortexRollRamp))) : 0f;
                    rollWinkel += rollSpeed * aus * Time.deltaTime;
                    f.rotation = Quaternion.AngleAxis(rollWinkel, Vector3.right) * basisRot;
                }
                else
                {
                    SlerpTo(f, target, turnSpeed);
                    basisRot = f.rotation;
                }
            }
            else
            {
                if (!captured) { settleFrom = f.rotation; captured = true; }
                float sp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - travelT) / settleT));
                f.rotation = Quaternion.Slerp(settleFrom, homeRot, sp);
            }
            if (rollSpeed != 0f) SetzeUmDrehpunkt(f, bahn, homeRot, rollPivot);
            if (trail != null) SetTrailVisible(trail, f.position);
            prevPos = bahn;
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
        catch (NullReferenceException) { }   // neuere Unity werfen das statt MissingReference
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

    // Lage des Drehknochens im lokalen Raum der Fee. Wird einmal zu Beginn des Fluges bestimmt,
    // also in der bereits eingerollten Haltung -- dort liegt der Koerper um die Huefte herum.
    private Vector3 RollPivotLocal(Transform f)
    {
        if (f == null) return Vector3.zero;

        // Mittelpunkt der Huelle ueber alle Koerperknochen -- ein einzelner Knochen trifft die
        // Mitte der Kugel nicht: die Huefte liegt rund 18 cm darunter, und der Koerper wuerde
        // weiterhin sichtbar um diesen Versatz kreisen.
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        int n = 0;
        foreach (var t in f.GetComponentsInChildren<Transform>(true))
        {
            string nm = t.name;
            if (!nm.StartsWith("mixamorig:") && !nm.StartsWith("Tail_")) continue;
            // Fingerknochen auslassen -- davon gibt es 40 Stueck, sie wuerden den Mittelpunkt
            // zu den Haenden ziehen.
            if (nm.Contains("HandIndex") || nm.Contains("HandMiddle") || nm.Contains("HandPinky")
                || nm.Contains("HandRing")  || nm.Contains("HandThumb")) continue;
            Vector3 lp = f.InverseTransformPoint(t.position);
            min = Vector3.Min(min, lp);
            max = Vector3.Max(max, lp);
            n++;
        }
        if (n > 0) return (min + max) * 0.5f;

        foreach (var t in f.GetComponentsInChildren<Transform>(true))
            if (t.name == rollPivotBone) return f.InverseTransformPoint(t.position);
        return Vector3.zero;
    }

    // Setzt die Fee so, dass nicht ihr Wurzelpunkt, sondern der Drehknochen auf der Flugbahn liegt.
    // Ohne das waere der Drehpunkt die Fussspitze und der Koerper wuerde grosse Kreise fahren.
    private static void SetzeUmDrehpunkt(Transform f, Vector3 bahn, Quaternion ruheRot, Vector3 pivotLokal)
    {
        Vector3 skaliert = Vector3.Scale(f.localScale, pivotLokal);
        f.position = bahn + ruheRot * skaliert - f.rotation * skaliert;
    }

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

    // Setzt ein Bool nur, wenn der Controller es kennt -- Feen ohne eigene Animation bleiben so
    // unberuehrt, ohne dass Unity eine Warnung wirft.
    private static void SetBool(Animator a, string name, bool value)
    {
        if (a == null || a.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
            {
                a.SetBool(name, value);
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

    private Vector3 ViewportToWorldAtZ(Vector2 vp, float z)
    {
        if (_cam == null) return new Vector3(0f, 0f, z);
        float d = Mathf.Abs(_cam.transform.position.z - z);
        var w = _cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, d));
        w.z = z;
        return w;
    }
}
