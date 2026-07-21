using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Großes visuelles Intro beim Antippen von PLAY im Hauptmenü: die drei Feen fliegen selbst (auf
// einem Bogen, nicht reingezogen) ins aktuell angezeigte Skin-Portal und verschwinden, danach zoomt
// die Kamera nah an die Portal-Mitte heran — erst DANACH wechselt die Szene zum eigentlichen Spiel
// (siehe ModeSelectController.OnInfinity).
// Antippen während der Sequenz überspringt den Rest sofort (kein Warten bei jedem Spielstart).
public class PlayIntroSequence : MonoBehaviour
{
    public static PlayIntroSequence Instance { get; private set; }

    [Header("Feen (fliegen selbst ins Portal und verschwinden)")]
    [SerializeField] private Transform[] fairies;
    [SerializeField] private float fairyFlightDuration = 0.5f;
    [Tooltip("Versatz zwischen dem Start der einzelnen Feen-Flüge — 0 = alle gleichzeitig los.")]
    [SerializeField] private float fairyStagger = 0.12f;
    [Tooltip("Wie stark der Flugweg von der geraden Linie zum Portal abweicht (Bogen statt starrer Linie).")]
    [SerializeField] private float fairyCurveStrength = 1.2f;
    [Tooltip("Wie viel schneller die Flügel während des Flugs schlagen (sanft hochgeeast über " +
             "FairyWingFlap.SetSpeedBoost, kein abrupter Sprung).")]
    [SerializeField] private float fairyFlapSpeedBoost = 1.6f;

    [Header("Kamera-Zoom zur Portal-Mitte")]
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

        if (portal != null && fairies != null && fairies.Length > 0)
            yield return Co_FairiesIntoPortal(portal);

        Camera cam = Camera.main;
        if (portal != null && cam != null && cam.orthographic)
            yield return Co_ZoomToPortal(cam, portal);

        DimOverlay.Instance?.Hide();
        _playing = false;
        onComplete?.Invoke();
    }

    private IEnumerator Co_FairiesIntoPortal(Transform portal)
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
    }

    private IEnumerator Co_SingleFairyIntoPortal(Transform fairy, Transform portal, Action onDone)
    {
        // Eigene Bewegungs-Skripte pausieren, während das Intro selbst die Position steuert (gleiches
        // Prinzip wie die Tap-Gimmicks: nie zwei Bewegungsquellen gleichzeitig). Der Flügelschlag
        // (FairyWingFlap) bleibt bewusst aktiv, sieht im Flug lebendiger aus als starre Flügel.
        var fairyFloat = fairy.GetComponent<FairyFloat>();
        if (fairyFloat != null) fairyFloat.enabled = false;
        var col = fairy.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Falls PLAY genau während eines eigenen Tap-Gimmicks (Loop/Bubble/...) gedrückt wurde, dessen
        // Coroutine stoppen — sonst würde die parallel weiter an Position/Pose rumfummeln.
        fairy.GetComponent<FairyTapGimmick>()?.StopAllCoroutines();
        fairy.GetComponent<FairyLoopFlight>()?.StopAllCoroutines();
        fairy.GetComponent<FairyBubbleBurst>()?.StopAllCoroutines();

        // ClearPose löst eine evtl. hängende gehaltene Flügel-Pose (z.B. während der Vibrationsphase
        // von Blau) — sonst würden die Flügel während des ganzen Flugs regungslos in dieser Pose
        // stecken bleiben, statt zu flattern. SetSpeedBoost fährt den Flügelschlag sanft hoch (gleiche
        // exponentielle Ease-Logik wie bei den Tap-Gimmicks), kein ruckartiger Sprung.
        var wingFlap = fairy.GetComponent<FairyWingFlap>();
        if (wingFlap != null)
        {
            wingFlap.ClearPose();
            wingFlap.SetSpeedBoost(fairyFlapSpeedBoost);
        }

        Vector3 startPos   = fairy.position;
        Vector3 startScale = fairy.localScale;

        // Bogen statt gerader Linie (gleiches Prinzip wie Co_MoveCurved bei den Tap-Gimmicks) —
        // zufälliger seitlicher Kontrollpunkt, damit die Fee aktiv reinfliegt statt reingezogen zu wirken.
        Vector3 toPortal   = portal.position - startPos;
        Vector3 perp       = new Vector3(-toPortal.y, toPortal.x, 0f).normalized;
        perp              *= (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 control    = startPos + toPortal * 0.5f + perp * fairyCurveStrength;

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
            yield return null;
        }

        fairy.gameObject.SetActive(false);
        onDone?.Invoke();
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
