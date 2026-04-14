using UnityEngine;
using System.Collections;

public enum TutorialPointType
{
    NormalPoint,
    SwipePoint,
    GoldOrb,
    GoldPoint,
    GravityOrb,
    GravityPoint,
    FountainOrb,
    FountainPoint
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

    [Header("Orb Prefabs")]
    [SerializeField] private GameObject goldOrbPrefab;
    [SerializeField] private GameObject gravityOrbPrefab;
    [SerializeField] private GameObject fountainOrbPrefab;

    [Header("Texte")]
    [SerializeField] private string textNormalPoint   = "Tap to destroy!";
    [SerializeField] private string textSwipePoint    = "Swipe to destroy!";
    [SerializeField] private string textGoldOrb       = "LOOK! A special orb.\nTap to start Gold Mode!";
    [SerializeField] private string textGoldPoint     = "Tap the golden point!";
    [SerializeField] private string textGoldSwipe     = "Swipe the golden point!";
    [SerializeField] private string textGravityOrb    = "LOOK! A special orb.\nTap to start Gravity Mode!";
    [SerializeField] private string textGravityPoint  = "Tap the falling points!";
    [SerializeField] private string textFountainOrb   = "LOOK! A special orb.\nTap to start Fountain Mode!";
    [SerializeField] private string textFountainPoint = "Tap the flying points!";
    [SerializeField] private string textEnd           = "Now it's your turn!";

    [Header("Timing")]
    [Tooltip("Wartezeit nach dem Spawn, damit die Element-Animation fertig spielen kann")]
    [SerializeField] private float preSpawnDelay     = 1.1f;
    [SerializeField] private float delayBetweenSteps = 0.5f;
    [Tooltip("Sekunden in GravityMode bis der Hinweis erscheint")]
    [SerializeField] private float gravityHintDelay  = 2f;

    [Header("Spawn-Positionen (Viewport 0–1)")]
    [Tooltip("Position für normale Punkte und Gold-Punkte (zentral)")]
    [SerializeField] private Vector2 pointVP = new Vector2(0.5f, 0.50f);
    [Tooltip("Position für Aktivierungs-Orbs")]
    [SerializeField] private Vector2 orbVP   = new Vector2(0.5f, 0.50f);

    // Interner Zustand
    private TutorialPointType? waitingFor;
    private bool actionReceived;
    private bool tutorialActive;
    private bool noFreezeMode;         // Overlay ohne Zeitstop zeigen
    private bool specialOverlayShown;  // Verhindert mehrfaches ShowOverlay pro Schritt

    // Aktuell gesperrter Orb (Collider deaktiviert bis Overlay erscheint)
    private GameObject _lockedOrb;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (PlayerPrefs.GetInt(PrefKey, 0) == 1) { enabled = false; return; }

        tutorialActive = true;
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();

        StartCoroutine(RunTutorialSequence());
    }

    // ── Hauptsequenz ─────────────────────────────────────────────────────────

    private IEnumerator RunTutorialSequence()
    {
        // Spawner sperren: kein automatisches Spawnen
        spawner.SetTutorialMode(true);

        // Warte 2 Frames, damit alle Start()-Methoden fertig sind
        yield return null;
        yield return null;

        // Eventuell bereits auto-gespawnten Point entfernen
        spawner.ForceClearCurrentPoint();

        Vector3 center   = GetWorldPos(pointVP);
        Vector3 orbWorld = GetWorldPos(orbVP);

        // ─────────────────────────────────────────────────────────────────────
        // 1. NeonTap
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.NormalPoint, center,
            new TutorialStepData(textNormalPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 2. NeonSwipe (fixe Richtung UpRight)
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textSwipePoint, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 3. GoldActivationOrb
        // ─────────────────────────────────────────────────────────────────────
        if (goldOrbPrefab != null)
        {
            SpawnTutorialOrb(goldOrbPrefab, orbWorld);
            yield return WaitUnscaled(preSpawnDelay);
            yield return ShowAndWait(TutorialPointType.GoldOrb, orbWorld,
                new TutorialStepData(textGoldOrb, TutorialAnimType.Tap));
            // Zeit wurde in OnActionPerformed für Orbs sofort freigegeben →
            // Fly-Animation läuft jetzt. Warten bis GoldMode aktiv ist.
            yield return new WaitUntil(() =>
                GoldModeSystem.Instance != null && GoldModeSystem.Instance.IsActive);
        }

        yield return WaitUnscaled(0.4f);

        // ─────────────────────────────────────────────────────────────────────
        // 4. GoldNeonTap
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.GoldPoint, center,
            new TutorialStepData(textGoldPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 5. GoldNeonSwipe
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textGoldSwipe, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 6. GoldMode bis zum Ende laufen lassen
        // ─────────────────────────────────────────────────────────────────────
        spawner.SetTutorialMode(false);
        spawner.SpawnNextPoint();
        yield return new WaitUntil(() =>
            GoldModeSystem.Instance == null || !GoldModeSystem.Instance.IsActive);
        spawner.SetTutorialMode(true);
        spawner.ForceClearCurrentPoint();
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 7+8. Normaler Modus wieder: NeonTap
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.NormalPoint, center,
            new TutorialStepData(textNormalPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 9. NeonSwipe
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textSwipePoint, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 10. GravityActivationOrb
        // ─────────────────────────────────────────────────────────────────────
        if (gravityOrbPrefab != null)
        {
            SpawnTutorialOrb(gravityOrbPrefab, orbWorld);
            yield return WaitUnscaled(preSpawnDelay);
            yield return ShowAndWait(TutorialPointType.GravityOrb, orbWorld,
                new TutorialStepData(textGravityOrb, TutorialAnimType.Tap));
        }

        // Warten bis GravityMode wirklich gestartet ist
        yield return new WaitUntil(() =>
            GravityModeSystem.Instance != null && GravityModeSystem.Instance.IsActive);

        // ─────────────────────────────────────────────────────────────────────
        // 11. GravityMode läuft
        // 12. Nach 2s: Hinweis anzeigen (KEIN Freeze – Punkte fallen weiter)
        // ─────────────────────────────────────────────────────────────────────
        yield return WaitUnscaled(gravityHintDelay);

        actionReceived  = false;
        waitingFor      = TutorialPointType.GravityPoint;
        noFreezeMode    = true;
        specialOverlayShown = false;

        // Position: erstes sichtbares GravityPoint oder Bildschirmmitte
        Vector3 hintPos = GetFirstGravityPointPos(center);
        overlay.Show(new TutorialStepData(textGravityPoint, TutorialAnimType.Tap), hintPos);

        // ─────────────────────────────────────────────────────────────────────
        // 13. Warte auf ersten GravityPoint-Treffer, dann GravityMode weiter
        // ─────────────────────────────────────────────────────────────────────
        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;

        // ─────────────────────────────────────────────────────────────────────
        // 14. Nach GravityMode Ende: NeonTap
        // ─────────────────────────────────────────────────────────────────────
        yield return new WaitUntil(() =>
            GravityModeSystem.Instance == null || !GravityModeSystem.Instance.IsActive);
        yield return WaitUnscaled(delayBetweenSteps);

        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.NormalPoint, center,
            new TutorialStepData(textNormalPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 15. NeonSwipe
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: false, worldPos: center, SwipeDirection.UpRight);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.SwipePoint, center,
            new TutorialStepData(textSwipePoint, TutorialAnimType.Swipe));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 16. NeonTap
        // ─────────────────────────────────────────────────────────────────────
        spawner.ForceTutorialSpawn(isTap: true, worldPos: center);
        yield return WaitUnscaled(preSpawnDelay);
        yield return ShowAndWait(TutorialPointType.NormalPoint, center,
            new TutorialStepData(textNormalPoint, TutorialAnimType.Tap));
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 17. FountainActivationOrb
        // ─────────────────────────────────────────────────────────────────────
        if (fountainOrbPrefab != null)
        {
            SpawnTutorialOrb(fountainOrbPrefab, orbWorld);
            yield return WaitUnscaled(preSpawnDelay);
            yield return ShowAndWait(TutorialPointType.FountainOrb, orbWorld,
                new TutorialStepData(textFountainOrb, TutorialAnimType.Tap));
        }

        // FountainPoint-Wartestate SOFORT nach dem Orb setzen, noch vor dem
        // WaitUntil – sonst spawnt der erste FountainPoint bevor waitingFor gesetzt ist
        specialOverlayShown = false;
        actionReceived      = false;
        waitingFor          = TutorialPointType.FountainPoint;

        // Warten bis FountainMode gestartet ist (Fly-Animation des Orbs läuft noch)
        yield return new WaitUntil(() =>
            SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive);

        // ─────────────────────────────────────────────────────────────────────
        // 18. FountainPoint – erster Treffer
        // ─────────────────────────────────────────────────────────────────────
        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;

        // FountainMode bis zum Ende laufen lassen
        yield return new WaitUntil(() =>
            SpecialModeManager.Instance == null || !SpecialModeManager.Instance.IsModeActive);
        yield return WaitUnscaled(delayBetweenSteps);

        // ─────────────────────────────────────────────────────────────────────
        // 19. Ende
        // ─────────────────────────────────────────────────────────────────────
        tutorialActive = false;
        overlay.ShowEndScreen(textEnd, () =>
        {
            overlay.HideEndScreen();
            spawner.SetTutorialMode(false);
            spawner.PauseSpawning(false);
            spawner.SpawnNextPoint();
        });

        Debug.Log("[Tutorial] Abgeschlossen.");
    }

    // ── Overlay mit Freeze anzeigen und auf Spieler-Aktion warten ────────────

    private IEnumerator ShowAndWait(TutorialPointType type, Vector3 worldPos, TutorialStepData data)
    {
        actionReceived = false;
        waitingFor     = type;

        // Erst freischalten, dann einfrieren → Spieler kann während Freeze tippen
        spawner.UnlockCurrentPoint();
        UnlockCurrentOrb();

        Time.timeScale = 0f;
        overlay.Show(data, worldPos);

        yield return new WaitUntil(() => actionReceived);
        waitingFor = null;
    }

    // ── Aufgerufen von SpecialMode-Systemen wenn erstes Element spawnt ────────

    public void OnElementSpawnedShowOverlay(TutorialPointType type, Vector3 worldPos)
    {
        if (!tutorialActive)          return;
        if (waitingFor != type)       return;
        if (specialOverlayShown)      return;

        // GravityPoint wird manuell gehandhabt (kein Freeze, 2s Delay)
        if (type == TutorialPointType.GravityPoint) return;

        if (type == TutorialPointType.FountainPoint)
        {
            specialOverlayShown = true;
            Time.timeScale = 0f;
            overlay.Show(new TutorialStepData(textFountainPoint, TutorialAnimType.Tap), worldPos);
        }
    }

    // ── Aufgerufen wenn Spieler korrekte Aktion ausgeführt hat ───────────────

    public void OnActionPerformed(TutorialPointType type)
    {
        if (!tutorialActive)    return;
        if (waitingFor != type) return;

        actionReceived = true;

        bool isOrb = type is TutorialPointType.GoldOrb
                          or TutorialPointType.GravityOrb
                          or TutorialPointType.FountainOrb;

        if (isOrb)
        {
            // Sofort Zeit freigeben, damit Fly-Animation spielen kann
            Time.timeScale = 1f;
            overlay.Hide(null);
        }
        else if (noFreezeMode)
        {
            // GravityMode-Hinweis: Zeit wurde nicht eingefroren
            overlay.Hide(null);
            noFreezeMode = false;
        }
        else
        {
            overlay.Hide(() => Time.timeScale = 1f);
        }

        waitingFor = null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SpawnTutorialOrb(GameObject prefab, Vector3 worldPos)
    {
        var orb = Instantiate(prefab, worldPos, Quaternion.identity);

        var goldOrb  = orb.GetComponent<GoldModeActivationPoint>();
        if (goldOrb  != null) goldOrb.spawner  = spawner;

        var gravOrb  = orb.GetComponent<GravityModeActivationPoint>();
        if (gravOrb  != null) gravOrb.spawner  = spawner;

        var fountOrb = orb.GetComponent<FountainModeActivationPoint>();
        if (fountOrb != null) fountOrb.spawner = spawner;

        spawner.RegisterTutorialOrb(orb);

        // Collider sperren bis das Overlay erscheint
        var col = orb.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        _lockedOrb = orb;
    }

    private void UnlockCurrentOrb()
    {
        if (_lockedOrb == null) return;
        var col = _lockedOrb.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
        _lockedOrb = null;
    }

    private Vector3 GetWorldPos(Vector2 viewport)
    {
        var cam = Camera.main;
        var p = cam.ViewportToWorldPoint(
            new Vector3(viewport.x, viewport.y, Mathf.Abs(cam.transform.position.z)));
        p.z = 0f;
        return p;
    }

    private Vector3 GetFirstGravityPointPos(Vector3 fallback)
    {
        var gp = FindFirstObjectByType<GravityPoint>();
        return gp != null ? gp.transform.position : fallback;
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
