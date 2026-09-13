using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Großes visuelles Intro beim Antippen von PLAY im Hauptmenü: die drei (jetzt 3D-) Feen fliegen
// selbst (auf einem Bogen, nicht reingezogen) ins aktuell angezeigte Skin-Portal — sie drehen sich
// dabei um (Rücken zum Spieler) und wechseln parallel von Idle_Fly in ihre Flugmodus-Animation
// (Special-Mode-Fly-Clip, per Animator-Bool "IsSpecialMode"), dann fliegen sie kleiner werdend ins
// Portal und verschwinden. Danach zoomt die Kamera nah an die Portal-Mitte heran — erst DANACH
// wechselt die Szene zum eigentlichen Spiel (siehe ModeSelectController.OnInfinity).
// Antippen während der Sequenz überspringt den Rest sofort (kein Warten bei jedem Spielstart).
public class PlayIntroSequence : MonoBehaviour
{
    public static PlayIntroSequence Instance { get; private set; }

    [Header("Feen (3D, stehen standardmäßig im Idle-Flug, fliegen selbst ins Portal und verschwinden)")]
    [SerializeField] private Transform[] fairies;
    [SerializeField] private string specialModeBool = "IsSpecialMode";
    [SerializeField] private float fairyFlightDuration = 1.15f;
    [Tooltip("Anteil des Flugs (Sekunden), über den sich die Fee vom Spieler weg dreht (Rücken zur " +
             "Kamera) — läuft PARALLEL zum Umschalten auf die Flugmodus-Animation, danach hält sie " +
             "die Rückenrotation bis sie im Portal verschwindet.")]
    [SerializeField] private float fairyTurnDuration = 0.22f;
    [Tooltip("Versatz zwischen dem Start der einzelnen Feen-Flüge — 0 = alle gleichzeitig los.")]
    [SerializeField] private float fairyStagger = 0.12f;
    [Tooltip("Wie stark der Flugweg von der geraden Linie zum Portal abweicht (Bogen statt starrer Linie).")]
    [SerializeField] private float fairyCurveStrength = 1.2f;
    [Tooltip("Zusätzliches Nach-vorn-Kippen (Liege-/Superman-Haltung) beim Flug Richtung Portal, in Grad. " +
             "0 = bleibt aufrecht, ~50-70 = flach liegend fliegend.")]
    [Range(0f, 90f)]
    [SerializeField] private float fairyFlightLeanDegrees = 60f;

    [Header("Kamera-Zoom zur Portal-Mitte")]
    [Tooltip("Wartezeit, bevor der Zoom beginnt — läuft danach PARALLEL zu den noch fliegenden Feen " +
             "(nicht erst danach), spart also Gesamtzeit gegenüber einem rein sequentiellen Ablauf.")]
    [SerializeField] private float cameraZoomStartDelay = 0.35f;
    [SerializeField] private float cameraZoomDuration = 0.8f;
    [Tooltip("Ziel-Orthographic-Size relativ zur aktuellen (kleiner = näher reingezoomt).")]
    [Range(0.02f, 1f)] [SerializeField] private float cameraZoomTargetFactor = 0.12f;

    [Header("Menü-UI ausblenden")]
    [SerializeField] private CanvasGroup menuUiCanvasGroup;
    [SerializeField] private float menuUiFadeDuration = 0.35f;

    [Header("Überspringen")]
    [Tooltip("Antippen während der Sequenz beendet sie sofort (Feen ausblenden, Kamera-Zoom überspringen).")]
    [SerializeField] private bool allowSkip = true;

    private bool _playing;
    private bool _skipRequested;

    private void Awake() => Instance = this;

    private void Update()
    {
        if (!_playing || !allowSkip || _skipRequested) return;

        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
            _skipRequested = true;
    }

    /// <summary>Startet die Intro-Sequenz; ruft onComplete auf, sobald sie fertig ist (oder
    /// übersprungen wurde). Fehlt Portal/Kamera-Referenz, wird onComplete sicherheitshalber sofort
    /// aufgerufen — der Play-Flow darf durch dieses rein kosmetische Feature nie blockiert werden.</summary>
    public void Play(Action onComplete)
    {
        if (_playing) { onComplete?.Invoke(); return; }
        StartCoroutine(Co_Play(onComplete));
    }

    private IEnumerator Co_Play(Action onComplete)
    {
        _playing = true;
        _skipRequested = false;
        DimOverlay.Instance?.Show();

        // Läuft parallel zum Rest (nicht ausgewartet) — das Menü-UI soll einfach schnell wegfaden,
        // ohne die Gesamtdauer der Sequenz zu verlängern.
        if (menuUiCanvasGroup != null)
            StartCoroutine(Co_FadeOutMenuUi());

        Transform portal = MenuPortalSwitcher.Instance != null ? MenuPortalSwitcher.Instance.ActivePortalTransform : null;
        Camera cam = Camera.main;

        bool fairiesDone = !(portal != null && fairies != null && fairies.Length > 0);
        bool zoomDone     = !(portal != null && cam != null && cam.orthographic);

        if (!fairiesDone)
            StartCoroutine(Co_FairiesIntoPortal(portal, () => fairiesDone = true));

        // Der Kamera-Zoom startet jetzt schon WÄHREND die Feen noch reinfliegen (nach kurzer
        // Verzögerung, damit man den Flug noch sieht) statt erst danach — spart Gesamtzeit, ohne dass
        // sich der Flug selbst schneller anfühlen muss.
        if (!zoomDone)
            StartCoroutine(Co_DelayedZoom(cam, portal, () => zoomDone = true));

        while (!fairiesDone || !zoomDone) yield return null;

        DimOverlay.Instance?.Hide();
        _playing = false;
        onComplete?.Invoke();
    }

    private IEnumerator Co_FairiesIntoPortal(Transform portal, Action onAllDone)
    {
        int remaining = 0;
        for (int i = 0; i < fairies.Length; i++)
        {
            var fairy = fairies[i];
            if (fairy == null) continue;
            remaining++;
            StartCoroutine(Co_SingleFairyIntoPortal(fairy, portal, () => remaining--));

            // Nach der letzten Fee nicht mehr unnötig warten — das würde die Gesamtdauer verlängern,
            // ohne dass danach noch etwas gestaffelt werden müsste.
            bool isLast = i == fairies.Length - 1;
            if (isLast) continue;

            float staggerT = 0f;
            while (staggerT < fairyStagger && !_skipRequested)
            {
                staggerT += Time.deltaTime;
                yield return null;
            }
        }

        while (remaining > 0 && !_skipRequested)
            yield return null;

        // Bei Skip: alle Feen sofort ausblenden statt mitten in der Flugbahn stehen zu lassen.
        foreach (var fairy in fairies)
            if (fairy != null) fairy.gameObject.SetActive(false);

        onAllDone?.Invoke();
    }

    private IEnumerator Co_SingleFairyIntoPortal(Transform fairy, Transform portal, Action onDone)
    {
        // Parallel zum Umdrehen (siehe unten) auf die Flugmodus-Animation wechseln — derselbe Bool
        // und dieselbe Special-Mode-Fly-Animation wie im eigentlichen Spiel (FairyChoreographyDirector).
        var animator = fairy.GetComponentInChildren<Animator>();
        SetSpecialBool(animator, true);

        Vector3    startPos      = fairy.position;
        Vector3    startScale    = fairy.localScale;
        Quaternion startRot      = fairy.rotation;
        // Umdrehen = zusätzliche 180°-Drehung um die eigene Y-Achse (Rücken statt Gesicht zur Kamera),
        // plus nach vorn kippen (Liege-/Superman-Haltung) für den Flug Richtung Portal.
        Quaternion backFacingRot = startRot * Quaternion.Euler(0f, 180f, 0f) * Quaternion.AngleAxis(fairyFlightLeanDegrees, Vector3.right);

        // Bogen statt gerader Linie — zufälliger seitlicher Kontrollpunkt, damit die Fee aktiv
        // reinfliegt statt reingezogen zu wirken.
        Vector3 toPortal = portal.position - startPos;
        Vector3 perp     = new Vector3(-toPortal.y, toPortal.x, 0f).normalized;
        perp            *= (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 control  = startPos + toPortal * 0.5f + perp * fairyCurveStrength;

        float turnT = Mathf.Clamp(fairyTurnDuration, 0.05f, fairyFlightDuration);

        float t = 0f;
        while (t < fairyFlightDuration && !_skipRequested)
        {
            t += Time.deltaTime;
            // SmoothStep: startet aus der Ruhe heraus, beschleunigt in den Flug und wird erst kurz
            // vorm Verschwinden im Portal wieder langsamer — wirkt wie ein aktiver Anflug, nicht wie
            // ein passives Reingezogen-werden.
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fairyFlightDuration));
            Vector3 a = Vector3.Lerp(startPos, control, p);
            Vector3 b = Vector3.Lerp(control, portal.position, p);
            fairy.position   = Vector3.Lerp(a, b, p);
            fairy.localScale = Vector3.Lerp(startScale, Vector3.zero, p);

            // Dreht sich zu Beginn des Flugs (parallel zum Animations-Wechsel oben) einmal um und
            // hält danach die Rückenrotation, bis sie im Portal verschwindet.
            float turnP = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / turnT));
            fairy.rotation = Quaternion.Slerp(startRot, backFacingRot, turnP);

            yield return null;
        }

        fairy.gameObject.SetActive(false);
        onDone?.Invoke();
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

    private IEnumerator Co_FadeOutMenuUi()
    {
        float startAlpha = menuUiCanvasGroup.alpha;
        menuUiCanvasGroup.blocksRaycasts = false; // sofort, damit man während des Fades nichts mehr antippen kann

        float t = 0f;
        while (t < menuUiFadeDuration)
        {
            t += Time.deltaTime;
            menuUiCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(t / menuUiFadeDuration));
            yield return null;
        }
        menuUiCanvasGroup.alpha = 0f;
    }

    // Startet den Zoom erst nach `cameraZoomStartDelay` (damit man den Feen-Abflug noch kurz sieht),
    // läuft aber PARALLEL zu den noch fliegenden Feen statt erst danach — spart Gesamtzeit.
    private IEnumerator Co_DelayedZoom(Camera cam, Transform portal, Action onDone)
    {
        float t = 0f;
        while (t < cameraZoomStartDelay && !_skipRequested)
        {
            t += Time.deltaTime;
            yield return null;
        }
        yield return Co_ZoomToPortal(cam, portal);
        onDone?.Invoke();
    }

    private IEnumerator Co_ZoomToPortal(Camera cam, Transform portal)
    {
        Vector3 startPos   = cam.transform.position;
        Vector3 targetPos  = new Vector3(portal.position.x, portal.position.y, startPos.z);
        float   startSize  = cam.orthographicSize;
        float   targetSize = startSize * cameraZoomTargetFactor;

        float t = 0f;
        while (t < cameraZoomDuration && !_skipRequested)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / cameraZoomDuration));
            cam.transform.position = Vector3.Lerp(startPos, targetPos, p);
            cam.orthographicSize   = Mathf.Lerp(startSize, targetSize, p);
            yield return null;
        }
    }
}
