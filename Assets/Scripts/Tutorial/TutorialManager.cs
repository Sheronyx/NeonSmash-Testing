using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

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

    [Header("Texte")]
    [SerializeField] private string textNormalPoint = "Tap to destroy!";
    [SerializeField] private string textSwipePoint  = "Swipe to destroy!";
    [SerializeField] private string textScoreHint   = "Each destroyed Point increases your score";
    [SerializeField] private string textEnd         = "Tutorial mastered!";

    [Header("Timing")]
    [Tooltip("Warte nach Spawn, damit Spawn-Animation fertig spielt (vor Overlay-Anzeige)")]
    [SerializeField] private float preSpawnDelay     = 1.1f;
    [Tooltip("Kurze Pause zwischen Schritten")]
    [SerializeField] private float delayBetweenSteps = 0.4f;
    [Tooltip("Anzeigedauer des Score-Hinweises (Sekunden)")]
    [SerializeField] private float scoreHintDuration = 2.5f;

    [Header("Spawn-Position (Viewport 0–1)")]
    [SerializeField] private Vector2 pointVP     = new Vector2(0.5f, 0.5f);
    [Tooltip("Position des Score-Hinweises oben links (Viewport). X=0.15 → links, Y=0.88 → ca. 100px unter TopBar")]
    [SerializeField] private Vector2 scoreHintVP = new Vector2(0.5f, 0.88f);

    [Header("Debug")]
    [Tooltip("Tutorial erzwingen, auch wenn es bereits abgeschlossen wurde (nur im Editor)")]
    [SerializeField] private bool forceTutorialInEditor = false;

    // Interner Zustand
    private TutorialPointType? waitingFor;
    private bool actionReceived;
    private bool tutorialActive;
    private bool _overlayIsShown;

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
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();

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

        // ── Score-Hinweis (kein Freeze, kein Finger) ─────────────────────────
        yield return ShowTimedHint(textScoreHint, scoreHintVP, scoreHintDuration);

        // ── 2–5. Stille Taps – zufällige Positionen, sofort interaktiv ──────────
        for (int i = 0; i < 4; i++)
        {
            spawner.ForceTutorialSpawn(isTap: true, worldPos: spawner.GetRandomSpawnWorldPos(), lockUntilOverlay: false);
            yield return WaitForSilentAction(TutorialPointType.NormalPoint);
        }

        // ── 6. Swipe MIT Erklärung ───────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textSwipePoint, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ── 7–8. Stille Swipes – zufällige Positionen, sofort interaktiv ──────
        for (int i = 0; i < 2; i++)
        {
            spawner.ForceTutorialSpawn(isTap: false, worldPos: spawner.GetRandomSpawnWorldPos(), lockUntilOverlay: false);
            yield return WaitForSilentAction(TutorialPointType.SwipePoint);
        }

        // ── Sieg-Screen ───────────────────────────────────────────────────────
        tutorialActive = false;
        Time.timeScale = 1f;
        overlay.ShowEndScreen(textEnd, () =>
        {
            if (SceneFader.Instance != null)
                SceneFader.Instance.LoadScene("MainMenuScene");
            else
                SceneManager.LoadScene("MainMenuScene");
        });
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

    // ── Hinweis-Text ohne Finger, Zeit einfrieren, nach Dauer automatisch weiter ─

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
        // Sofort interaktiv – kein Delay, kein Overlay
        spawner.UnlockCurrentPoint();

        actionReceived = false;
        waitingFor     = type;

        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;

        yield return WaitUnscaled(delayBetweenSteps);
    }

    // ── Callback: aufgerufen von TapPoint / SwipePoint ────────────────────────

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

    // Stub: wird von SpecialMode-Systemen aufgerufen – im Tutorial nicht mehr benötigt
    public void OnElementSpawnedShowOverlay(TutorialPointType type, Vector3 worldPos) { }

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
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] Reset. Bitte Szene neu laden.");
    }
}
