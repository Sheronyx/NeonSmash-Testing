using UnityEngine;
using System.Collections;

public enum TutorialPointType
{
    NormalPoint,
    SwipePoint,
    GoldPoint,     // referenced by TapPoint.cs
    GravityPoint,  // referenced by GravityModeSystem.cs
    FountainPoint, // referenced by FountainModeSystem.cs
    GoldOrb,
    GravityOrb,
    FountainOrb
}

public enum TutorialAnimType { Tap, Swipe }

public readonly struct TutorialStepData
{
    public readonly string text;
    public readonly TutorialAnimType animType;

    public TutorialStepData(string text, TutorialAnimType animType)
    {
        this.text = text;
        this.animType = animType;
    }
}

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;
    public static bool IsTutorialActive => Instance != null && Instance.tutorialActive;

    private const string PrefKey = "TutorialCompleted_v1";

    [Header("Refs")]
    [SerializeField] private TutorialOverlay overlay;
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject pauseButton;

    [Header("UI Override (TIME-Box → TUTORIAL)")]
    [Tooltip("Objekte in der TIME-Box die im Tutorial ausgeblendet werden (TIME-Label, Zahl, Ring…)")]
    [SerializeField] private GameObject[] timeObjectsToHide;
    [Tooltip("Eigenes TMP-Objekt 'TUTORIAL' (im Editor deaktiviert) – wird im Tutorial aktiviert")]
    [SerializeField] private GameObject tutorialLabelObject;

    [Header("Texte – Tap / Swipe")]
    [SerializeField] private string textNormalPoint = "Tap to destroy!";
    [SerializeField] private string textSwipePoint  = "Swipe to destroy!";

    [Header("Texte – Hints")]
    [SerializeField] private string textScoreHint  = "Each destroyed Point increases your score";
    [SerializeField] private string textSpawnHint  = "With each level the spawn time gets shorter";
    [SerializeField] private string textLivesHint  = "Be fast or you lose lifepoints";
    [SerializeField] private string textSpecialOrb = "Look! A Special Orb!\nTap it!";

    [Header("Timing")]
    [Tooltip("Warte nach Spawn, damit Spawn-Animation fertig spielt (vor Overlay-Anzeige)")]
    [SerializeField] private float preSpawnDelay      = 1.1f;
    [Tooltip("Kurze Pause zwischen Schritten")]
    [SerializeField] private float delayBetweenSteps  = 0.4f;
    [Tooltip("Wartezeit nach Orb-Spawn bevor Overlay erscheint (Orb soll sichtbar sein)")]
    [SerializeField] private float orbRevealDelay     = 1.8f;
    [Tooltip("Anzeigedauer des Score-Hinweises (Sekunden)")]
    [SerializeField] private float scoreHintDuration  = 2.5f;
    [SerializeField] private float spawnHintDuration  = 2.5f;
    [SerializeField] private float livesHintDuration  = 2.5f;

    [Header("Spawn-Positionen (Viewport 0–1)")]
    [SerializeField] private Vector2 pointVP      = new(0.5f,  0.5f);
    [Tooltip("Score-Hinweis – oben mittig")]
    [SerializeField] private Vector2 scoreHintVP  = new(0.5f,  0.88f);
    [Tooltip("Spawn-Hinweis – oben mittig")]
    [SerializeField] private Vector2 spawnHintVP  = new(0.5f,  0.88f);
    [Tooltip("Leben-Hinweis – oben rechts, nah an den Herzen")]
    [SerializeField] private Vector2 livesHintVP  = new(0.75f, 0.88f);

    [Tooltip("Positionen für stille Tap-Punkte nach dem ersten erklärten Tap")]
    [SerializeField] private Vector2[] silentTapVPs = new Vector2[]
    {
        new(0.3f, 0.4f),
        new(0.7f, 0.6f),
    };
    [Tooltip("Positionen für stille Swipe-Punkte nach dem ersten erklärten Swipe")]
    [SerializeField] private Vector2[] silentSwipeVPs = new Vector2[]
    {
        new(0.35f, 0.55f),
        new(0.65f, 0.45f),
    };
    [Tooltip("Viewport-Position an der der Tutorial-Orb erscheint")]
    [SerializeField] private Vector2 orbVP                 = new(0.5f,  0.5f);
    [Tooltip("Viewport-Position des normalen Punktes während der Orb-Phase (unten rechts)")]
    [SerializeField] private Vector2 orbPhaseNormalPointVP = new(0.75f, 0.2f);

    [Header("Debug")]
    [Tooltip("Tutorial erzwingen, auch wenn es bereits abgeschlossen wurde (nur im Editor)")]
    [SerializeField] private bool forceTutorialInEditor = false;

    // ── Interner Zustand ──────────────────────────────────────────────────────
    private TutorialPointType? waitingFor;
    private bool actionReceived;
    private bool tutorialActive;
    private bool _overlayIsShown;

    // Orb-Warte-Phase
    private bool             _waitingForOrb;
    private bool             _orbSpawnReceived;
    private TutorialPointType _receivedOrbType;
    private Vector3           _receivedOrbWorldPos;
    private Collider2D        _pendingOrbCollider;

    // Pause-Koordination
    private bool _gamePaused;
    private bool _tutorialFreezesTime;

    public bool TutorialFreezesTime => _tutorialFreezesTime;
    public void SetGamePaused(bool paused) => _gamePaused = paused;

    // Orb-Phase: nur Orb darf getappt werden
    public static bool IsOrbPhaseActive { get; private set; }

    // Spawner liest diese Properties um Positionen zu erzwingen
    public static bool IsWaitingForTutorialOrb      => Instance != null && Instance._waitingForOrb;
    public static Vector2 TutorialOrbViewport        => Instance != null ? Instance.orbVP : new Vector2(0.5f, 0.5f);
    public static Vector2 TutorialNormalPointViewport => Instance != null ? Instance.orbPhaseNormalPointVP : new Vector2(0.75f, 0.2f);

    private void Awake() => Instance = this;

    private void Start()
    {
#if !UNITY_EDITOR
        forceTutorialInEditor = false;
#endif
        if (!forceTutorialInEditor && PlayerPrefs.GetInt(PrefKey, 0) == 1) { enabled = false; return; }

        // Pause-Button im Tutorial deaktivieren
        if (pauseButton != null) pauseButton.SetActive(false);

        // Timer deaktivieren – Tutorial hat kein Zeitlimit
        var tmc = FindFirstObjectByType<TimeModeController>();
        if (tmc != null) tmc.enabled = false;

        // TIME-Box: normale Elemente ausblenden, eigenes TUTORIAL-Label aktivieren
        if (timeObjectsToHide != null)
            foreach (var obj in timeObjectsToHide)
                if (obj != null) obj.SetActive(false);

        if (tutorialLabelObject != null) tutorialLabelObject.SetActive(true);

        tutorialActive = true;
        StartCoroutine(RunTutorialSequence());
    }

    // ── Hauptsequenz ──────────────────────────────────────────────────────────

    private IEnumerator RunTutorialSequence()
    {
        // Spawner sperren: kein automatisches Spawnen
        spawner.SetTutorialMode(true);

        // 2 Frames warten, damit alle Start()-Methoden fertig sind
        yield return null;
        yield return null;

        // Eventuell bereits auto-gespawnten Point entfernen
        spawner.ForceClearCurrentPoint();

        // Warten bis der Countdown (3-2-1-Go) fertig ist und spawner.Begin() aufgerufen wurde
        yield return new WaitUntil(() => spawner.IsRunning);

        // Nach Countdown nochmals sicherstellen dass kein auto-gespawnter Point da ist
        spawner.ForceClearCurrentPoint();

        Vector3 center = GetWorldPos(pointVP);

        // ── 1. Tap MIT Erklärung ─────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.NormalPoint, center,
            new TutorialStepData(textNormalPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ── Score-Hinweis ────────────────────────────────────────────────────
        yield return ShowTimedHint(textScoreHint, scoreHintVP, scoreHintDuration);

        // ── 2–3. Stille Taps – sofort interaktiv ────────────────────────────
        for (int i = 0; i < 2; i++)
        {
            Vector3 tapPos = GetWorldPos(silentTapVPs[i % silentTapVPs.Length]);
            spawner.ForceTutorialSpawn(isTap: true, worldPos: tapPos, lockUntilOverlay: false);
            yield return WaitForSilentAction(TutorialPointType.NormalPoint);
        }

        // ── 4. Swipe MIT Erklärung ───────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textSwipePoint, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ── 5–6. Stille Swipes – sofort interaktiv ───────────────────────────
        for (int i = 0; i < 2; i++)
        {
            Vector3 swipePos = GetWorldPos(silentSwipeVPs[i % silentSwipeVPs.Length]);
            spawner.ForceTutorialSpawn(isTap: false, worldPos: swipePos, lockUntilOverlay: false);
            yield return WaitForSilentAction(TutorialPointType.SwipePoint);
        }

        // ── Hinweis: Spawn-Zeit ───────────────────────────────────────────────
        yield return ShowTimedHint(textSpawnHint, spawnHintVP, spawnHintDuration);

        // ── Hinweis: Leben ────────────────────────────────────────────────────
        yield return ShowTimedHint(textLivesHint, livesHintVP, livesHintDuration);

        // ── Ab hier: normaler Infinity-Spielbetrieb ───────────────────────────
        spawner.SetTutorialMode(false);
        spawner.SpawnNextPoint();
        tutorialActive = false;
        if (pauseButton != null) pauseButton.SetActive(true);

        // ── Warten bis ein Activation-Orb spawnt (echtes Spiel-System) ──────────
        // Der Spawner erzwingt dabei die Mitte als Spawn-Position (TutorialOrbVP).
        _waitingForOrb    = true;
        _orbSpawnReceived = false;
        yield return new WaitUntil(() => _orbSpawnReceived && !_gamePaused);
        _waitingForOrb = false;

        // Orb-Phase starten: normalen Punkt wegräumen, Spawner pausieren
        IsOrbPhaseActive = true;
        spawner.ForceClearCurrentPoint();
        spawner.PauseSpawning(true);

        // Kurz warten damit der Orb in Ruhe sichtbar ist, bevor Overlay erscheint
        yield return WaitUnscaled(orbRevealDelay);

        // ── Special-Orb Erklärung ─────────────────────────────────────────────
        yield return ShowAndWaitForOrb(_receivedOrbType, _receivedOrbWorldPos,
            new TutorialStepData(textSpecialOrb, TutorialAnimType.Tap));

        IsOrbPhaseActive = false;
        spawner.PauseSpawning(false);

        // ── Tutorial abgeschlossen – Spiel läuft weiter ───────────────────────
        PlayerPrefs.SetInt(PrefKey, 1);
        if (PlayerPrefs.GetInt("TimeModeUnlocked", 0) == 0)
        {
            PlayerPrefs.SetInt("TimeModeUnlocked", 1);
            PlayerPrefs.SetInt("ShowTimeModeUnlockNotification", 1);
        }
        PlayerPrefs.Save();
        // Kein EndScreen, kein Redirect. Das Spiel läuft als normaler InfinityMode weiter.
    }

    // ── Overlay + Freeze anzeigen, auf Spieler-Aktion warten ─────────────────

    private IEnumerator ShowAndWait(TutorialPointType type, Vector3 worldPos, TutorialStepData data)
    {
        actionReceived  = false;
        waitingFor      = type;
        _overlayIsShown = true;

        spawner.UnlockCurrentPoint();

        Time.timeScale = 0f;
        overlay.Show(data, worldPos);

        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;
    }

    // ── Orb-Overlay: Overlay zeigen, dann Collider freigeben, auf Tap warten ──

    private IEnumerator ShowAndWaitForOrb(TutorialPointType type, Vector3 worldPos, TutorialStepData data)
    {
        tutorialActive  = true;   // Kurz reaktivieren damit OnActionPerformed funktioniert
        actionReceived  = false;
        waitingFor      = type;
        _overlayIsShown = true;

        Time.timeScale = 0f;
        overlay.Show(data, worldPos);

        // Orb-Collider erst NACH dem Overlay freigeben
        if (_pendingOrbCollider != null)
        {
            _pendingOrbCollider.enabled = true;
            _pendingOrbCollider = null;
        }

        yield return new WaitUntil(() => actionReceived);
        waitingFor     = null;
        tutorialActive = false;   // Tutorial dauerhaft beendet
    }

    // ── Hinweis-Text ohne Finger, Zeit einfrieren, nach Dauer automatisch weiter

    private IEnumerator ShowTimedHint(string text, Vector2 hintVP, float duration)
    {
        Time.timeScale = 0f;
        bool done = false;
        overlay.ShowHint(text, GetWorldPos(hintVP), duration, () => done = true);
        yield return new WaitUntil(() => done);
        Time.timeScale = 1f;
    }

    // ── Stiller Schritt: sofort freischalten, keine Verzögerung, auf Treffer warten

    private IEnumerator WaitForSilentAction(TutorialPointType type)
    {
        spawner.UnlockCurrentPoint();

        actionReceived = false;
        waitingFor     = type;

        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;

        yield return WaitUnscaled(delayBetweenSteps);
    }

    // ── Callback: aufgerufen von TapPoint / SwipePoint / Orbs ────────────────

    public void OnActionPerformed(TutorialPointType type)
    {
        if (!tutorialActive)    return;
        if (waitingFor != type) return;

        actionReceived = true;

        if (_overlayIsShown)
        {
            _overlayIsShown = false;
            overlay.Hide(() => Time.timeScale = 1f);
        }

        waitingFor = null;
    }

    // ── Callback: aufgerufen von Activation-Orbs beim Spawnen ────────────────

    public void OnElementSpawnedShowOverlay(TutorialPointType type, Vector3 worldPos, GameObject orbObject = null)
    {
        if (!_waitingForOrb) return;

        // Collider sofort sperren – Spieler darf nicht tippen bevor Overlay sichtbar ist
        if (orbObject != null)
        {
            _pendingOrbCollider = orbObject.GetComponent<Collider2D>();
            if (_pendingOrbCollider != null) _pendingOrbCollider.enabled = false;
        }

        _receivedOrbType     = type;
        _receivedOrbWorldPos = worldPos;
        _orbSpawnReceived    = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Vector3 GetWorldPos(Vector2 viewport)
    {
        var cam = Camera.main;
        var p = cam.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, Mathf.Abs(cam.transform.position.z)));
        p.z = 0f;
        return p;
    }

    private IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    [ContextMenu("Reset Tutorial (Debug)")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.DeleteKey("TimeModeUnlocked");
        PlayerPrefs.DeleteKey("ShowTimeModeUnlockNotification");
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] Reset. Bitte Szene neu laden.");
    }
}
