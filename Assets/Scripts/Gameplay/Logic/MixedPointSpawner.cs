using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MixedPointSpawner : MonoBehaviour
{
    public static MixedPointSpawner Instance { get; private set; }

    [SerializeField] private GameObject fountainModeActivationPointPrefab;
    [SerializeField] private GameObject gravityModeActivationPointPrefab;

    [SerializeField] private GameUIManager uiManager;
    private GameObject currentActivationPoint;

    private int CurrentScore =>
        ScoreManager.Instance ? ScoreManager.Instance.CurrentScore : 0;

    [Header("Activation Orb Cooldown (geteilt)")]
    [SerializeField] private float activationOrbCooldown = 60f;
    [SerializeField] private float initialOrbDelayMin = 30f;
    [SerializeField] private float initialOrbDelayMax = 40f;
    private bool activationOrbOnCooldown = false;
    private SpecialMode lastSpawnedOrbMode = SpecialMode.None;
    private bool isConvertingPoints = false;


    [SerializeField] private ArcanePortalFlash portalFlash;

    [SerializeField] private PortalSpawnBeam portalBeam;

    private GameMode CurrentMode =>
        GlobalGameManager.Instance ? GlobalGameManager.Instance.SelectedMode : GameMode.Infinity;

    private bool IsInfinityMode => CurrentMode == GameMode.Infinity || CurrentMode == GameMode.Multiplayer;

    [Header("Safe Area / Gesten")]
    [SerializeField] private bool useSafeAreaForSpawns = true;
    [SerializeField] private float extraBottomGesturePixels = 160f;

    [Header("Spawn-Area (Prozent)")]
    [Range(0f, 0.45f)] public float leftPercent = 0.1f;
    [Range(0f, 0.45f)] public float rightPercent = 0.1f;
    [Range(0f, 0.45f)] public float topPercent = 0.20f;
    [Range(0f, 0.45f)] public float bottomPercent = 0.20f;

    [Header("Abstand & Padding")]
    [SerializeField] private bool minDistanceAsPercent = true;
    [Range(0f, 0.5f)] public float minDistancePercent = 0.12f;
    public float minScreenDistancePixels = 100f;
    public float spawnPaddingPixels = 24f;

    [Header("Abstand Neon ↔ Activation Orb")]
    [Tooltip("Zusätzliche sichtbare Lücke zwischen NeonPoint- und Orb-Kante.")]
    [SerializeField] private float activationOrbVisualGapPixels = 40f;

    [Header("Activation Orb Spawn-Zone")]
    [Tooltip("Orbs spawnen nur im oberen Bereich (0 = gesamte Spawn-Area, 0.5 = nur obere Hälfte)")]
    [Range(0f, 0.9f)] [SerializeField] private float orbSpawnMinViewportY = 0.5f;

    [Header("Auto-Padding (empfohlen)")]
    [SerializeField] private bool autoComputePaddingFromPrefab = true;
    [SerializeField] private GameObject paddingSamplePrefab;
    [SerializeField] private float extraPaddingPixels = 12f;

    [Header("Prefabs & Refs")]
    public GameObject normalPointPrefab;
    public GameObject swipePointPrefab;

    [Header("Fake Point (Ablenkung)")]
    [Tooltip("Fake-Element-Prefab (FakePoint). Spawnt mit Chance parallel zu Tap-Points.")]
    public GameObject fakePointPrefab;
    [Range(0f, 1f)]
    [Tooltip("Wahrscheinlichkeit, dass bei einem Tap-Point zusätzlich ein Fake spawnt.")]
    public float fakeSpawnChance = 0.1f;
    private GameObject currentFake;

    [Header("Thunder Point (Falle)")]
    [Tooltip("Donnerschock-Prefab (ThunderPoint). Ersetzt mit Chance ein normales Element.")]
    public GameObject thunderPointPrefab;
    [Range(0f, 1f)]
    [Tooltip("Wahrscheinlichkeit, dass ein Donnerschock anstelle des normalen Elements spawnt.")]
    public float thunderSpawnChance = 0.1f;

    [Header("Peek-a-boo (Wolken-Sequenz)")]
    [SerializeField] private PeekABooSystem peekABooSystem;
    [Range(0f, 1f)]
    [Tooltip("Wahrscheinlichkeit, dass anstelle eines normalen Elements die Peek-a-boo-Sequenz startet.")]
    public float peekABooChance = 0.05f;

    GameObject ActiveNormalPrefab =>
        SkinManager.Instance?.ActiveTheme?.tapPointPrefab ?? normalPointPrefab;
    GameObject ActiveSwipePrefab =>
        SkinManager.Instance?.ActiveTheme?.swipePointPrefab ?? swipePointPrefab;
    GameObject ActiveFakePrefab =>
        SkinManager.Instance?.ActiveTheme?.fakePointPrefab ?? fakePointPrefab;
    GameObject ActiveThunderPrefab =>
        SkinManager.Instance?.ActiveTheme?.thunderPointPrefab ?? thunderPointPrefab;

    // Für PeekABooSystem: geskinnte/aktuelle Element-Prefabs
    public GameObject PeekTapPrefab     => ActiveNormalPrefab;
    public GameObject PeekSwipePrefab   => ActiveSwipePrefab;
    public GameObject PeekThunderPrefab => ActiveThunderPrefab;
    public GameObject PeekFakePrefab    => ActiveFakePrefab;
    public Camera mainCamera;

    [Header("Start/Timing")]
    public bool autoStart = false;
    public float respawnDelay = 0f;

    [Header("Countdown / Game Over")]
    public float reactionTime = 3f;
    public bool useUnscaledTime = false;
    public UnityEvent onGameOver;

    [SerializeField] private LevelUp levelUp;

    // Aktuelle Reaktionszeit (dynamisch pro Level) — z.B. für Peek-a-boo.
    public float CurrentReactionTime =>
        PhaseManager.Instance != null ? PhaseManager.Instance.CurrentReactionTime
        : levelUp != null ? levelUp.GetCurrentReactionTime(reactionTime) : reactionTime;

    // Vom PhaseManager gesteuert: in Play-Phasen kein zufälliges Activation-Orb-Spawning.
    [HideInInspector] public bool allowRandomActivationOrbs = true;

    [Header("Spawn-Verteilung (zufällig mit Grenzen)")]
    [Range(0f, 1f)] public float swipeChance = 0.33f;
    public int maxNormalsInRow = 4;
    public int maxSwipesInRow = 2;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool showSpawnAreaDebug = true;
    public Color spawnAreaFill = new Color(0f, 1f, 1f, 0.08f);
    public Color spawnAreaBorder = new Color(0f, 1f, 1f, 0.9f);
    public float spawnAreaBorderThickness = 2f;

    public SwipePoint CurrentSwipePoint { get; private set; }
    public Vector3? CurrentPointPosition => currentPoint != null ? currentPoint.transform.position : null;
    public bool IsRunning => running;
    public bool IsTutorialMode { get; private set; }

    public void SetTutorialMode(bool active) => IsTutorialMode = active;

    // Zwischengespeicherter SwipePoint während er gesperrt ist
    private SwipePoint _lockedSwipePoint;

    private GameObject currentPoint;
    private GameObject lastPoint;

    private int normalsInRow = 0;
    private int swipesInRow = 0;

    private bool running = false;
    private bool gameOver = false;
    private Coroutine timeoutRoutine;
    private bool spawnPausedForBanner = false;


    void Awake()
    {
        Instance = this;
        if (!mainCamera) mainCamera = Camera.main;

        if (autoComputePaddingFromPrefab && paddingSamplePrefab != null && mainCamera != null)
        {
            float halfSizePx = ComputeHalfSizePixels(paddingSamplePrefab);
            float suggested = halfSizePx + extraPaddingPixels;
            if (suggested > spawnPaddingPixels) spawnPaddingPixels = suggested;
            if (debugLogs) Debug.Log($"[Spawner] Auto-Padding gesetzt: {spawnPaddingPixels:F1}px (half={halfSizePx:F1}px + extra={extraPaddingPixels})");
        }
    }

    void Start()
    {
        if (autoStart) Begin();
    }

    public void Begin()
    {
        if (running) return;
        running = true;
        gameOver = false;
        spawnPausedForBanner = false;
        // Reaktionszeit/Phasen kommen jetzt vom PhaseManager (LevelUp wird nicht mehr getriggert).

        // Ersten Orb erst nach initialOrbDelay erlauben
        activationOrbOnCooldown = true;
        StartCoroutine(InitialOrbDelayRoutine());

        if (IsInfinityMode)
        {
            MusicManager.Instance?.ResetGameMusicSpeed();
        }

        if (currentPoint == null) SpawnNextPoint();
    }

    public void StopSpawning()
    {
        running = false;
        StopPointTimer();
    }

    /// <summary>Vom PhaseManager: Spawning für den Phasen-Banner kurz anhalten / fortsetzen.</summary>
    public void SetBannerPause(bool paused)
    {
        spawnPausedForBanner = paused;
        // Beim Fortsetzen das Spawning wieder anstoßen, falls gerade kein Punkt aktiv ist.
        if (!paused && running && currentPoint == null)
            SpawnNextPoint();
    }

    public void SpawnNextPoint()
    {
        if (IsTutorialMode) return;

        if (!running || spawnPausedForBanner || currentPoint != null || isConvertingPoints) return;

        // Activation Orb kommt allein (kein normaler Tap-/SwipePoint daneben).
        // Im Phasen-System steuert der PhaseManager die Orbs → kein zufälliges Spawnen.
        if (IsInfinityMode && allowRandomActivationOrbs && TrySpawnActivationOrb()) return;

        bool forceSwipe = maxNormalsInRow > 0 && normalsInRow >= maxNormalsInRow;
        bool forceNormal = maxSwipesInRow > 0 && swipesInRow >= maxSwipesInRow;

        GameObject prefabToSpawn;
        bool spawnSwipe;

        if (forceSwipe) spawnSwipe = true;
        else if (forceNormal) spawnSwipe = false;
        else spawnSwipe = Random.value < swipeChance;

        

        prefabToSpawn = spawnSwipe ? ActiveSwipePrefab : ActiveNormalPrefab;

        if (spawnSwipe)
        {
            swipesInRow++;
            normalsInRow = 0;
        }
        else
        {
            normalsInRow++;
            swipesInRow = 0;
        }

        // Größe des zu spawnenden Points berechnen
        float spawnPointHalfSizePx = GetHalfSizePixels(prefabToSpawn);

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);

        Vector2 viewportPos = new Vector2(0.5f, 0.5f);
        bool foundValid = false;

        int maxAttempts = currentActivationPoint != null ? 80 : 40;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            viewportPos = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            attempts++;

            bool farFromLast       = true;
            bool farFromActivation = true;

            // Abstand zu letztem Punkt
            if (lastPoint != null)
            {
                Vector2 lastVP = mainCamera.WorldToViewportPoint(lastPoint.transform.position);
                farFromLast = IsFarEnough(viewportPos, lastVP);
            }

            // Abstand zu Activation Orb — mit Größen beider Objekte
            if (currentActivationPoint != null)
            {
                Vector2 activationVP      = mainCamera.WorldToViewportPoint(currentActivationPoint.transform.position);
                float   activationHalfSizePx = GetHalfSizePixels(currentActivationPoint);
                farFromActivation = IsFarEnoughFromOrb(viewportPos, activationVP, spawnPointHalfSizePx, activationHalfSizePx);
            }

            if (farFromLast && farFromActivation)
            {
                foundValid = true;
                break;
            }
        }

        if (!foundValid && debugLogs)
            Debug.LogWarning("[Spawner] Kein gültiger Spawn gefunden → fallback Mitte");

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);

        // Peek-a-boo: übernimmt komplett (kein normales Element, kein Thunder/Fake)
        if (peekABooSystem != null && !PeekABooSystem.IsActive && Random.value < peekABooChance)
        {
            peekABooSystem.StartPeekABoo();
            return;
        }

        // Donnerschock: ersetzt das normale Element (kein Fake, kein PortalBeam)
        if (ActiveThunderPrefab != null && Random.value < thunderSpawnChance)
        {
            var thunder = Instantiate(ActiveThunderPrefab, worldPos, Quaternion.identity);
            float dynamicTime = CurrentReactionTime;
            var tp = thunder.GetComponent<ThunderPoint>();
            if (tp != null) tp.Activate(dynamicTime);

            // Spawner-Timer damit der nächste Point nach Timeout kommt
            timeoutRoutine = StartCoroutine(Co_ThunderTimeout(thunder, dynamicTime));
            return;
        }

        if (portalBeam != null)
        {
            portalBeam.SpawnWithBeam(prefabToSpawn, worldPos);
        }
        else
        {
            CreatePoint(prefabToSpawn, worldPos);
        }
    }

    // Wartet bis der Donnerschock abgelaufen ist, dann nächsten Point spawnen.
    private IEnumerator Co_ThunderTimeout(GameObject thunder, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (thunder == null)
            {
                // Angetippt → LoseLife-Animation abwarten, dann nächsten Point
                float wait = LivesManager.Instance != null
                    ? LivesManager.Instance.TotalLoseDuration : 0.5f;
                yield return new WaitForSeconds(wait);
                if (running && !gameOver) SpawnNextPoint();
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        // Zeit abgelaufen → nächsten Point spawnen (ThunderPoint.OnTimeout + Shrink laufen separat)
        if (running && !gameOver)
            SpawnNextPoint();
    }

    // ─── Abstand-Helpers ───────────────────────────────────────────────────────

    private float GetBaseMinDistancePixels()
    {
        return minDistanceAsPercent
            ? Mathf.Min(Screen.width, Screen.height) * minDistancePercent
            : minScreenDistancePixels;
    }

    /// <summary>Einfacher Mindestabstand (Point ↔ letzter Point).</summary>
    private bool IsFarEnough(Vector2 candidateVP, Vector2 targetVP)
    {
        Vector2 candidatePx = candidateVP * new Vector2(Screen.width, Screen.height);
        Vector2 targetPx = targetVP * new Vector2(Screen.width, Screen.height);
        return Vector2.Distance(candidatePx, targetPx) >= GetBaseMinDistancePixels();
    }

    /// <summary>
    /// Größen-bewusster Abstand: Mindestdistanz = Radius Point + Radius Orb + visueller Gap.
    /// Verhindert, dass sich Objekte optisch berühren oder überlappen.
    /// </summary>
    private bool IsFarEnoughFromOrb(
        Vector2 candidateVP,
        Vector2 orbVP,
        float pointHalfSizePx,
        float orbHalfSizePx)
    {
        Vector2 candidatePx = candidateVP * new Vector2(Screen.width, Screen.height);
        Vector2 orbPx = orbVP * new Vector2(Screen.width, Screen.height);

        float sizeBasedDist = pointHalfSizePx + orbHalfSizePx + Mathf.Max(0f, activationOrbVisualGapPixels);
        float totalMinDist = Mathf.Max(GetBaseMinDistancePixels(), sizeBasedDist);

        return Vector2.Distance(candidatePx, orbPx) >= totalMinDist;
    }

    /// <summary>
    /// Prüft ob eine Kandidatenposition weit genug vom aktuellen Point entfernt ist.
    /// Wird von TrySpawnGravityModePoint genutzt.
    /// </summary>
    private bool IsFarEnoughFromCurrentPoint(Vector2 candidateVP, float orbHalfSizePx)
    {
        if (currentPoint == null) return true;

        Vector2 currentVP = mainCamera.WorldToViewportPoint(currentPoint.transform.position);
        float currentPointHalfSizePx = GetHalfSizePixels(currentPoint);

        return IsFarEnoughFromOrb(candidateVP, currentVP, orbHalfSizePx, currentPointHalfSizePx);
    }


    // ─── Größen-Berechnung ─────────────────────────────────────────────────────

    /// <summary>
    /// Berechnet den Radius eines GameObjects in Pixeln.
    /// Nutzt Collider2D (zuverlässiger) mit Fallback auf SpriteRenderer.
    /// Funktioniert für Prefabs UND instanziierte Objekte.
    /// </summary>
    private float GetHalfSizePixels(GameObject go)
    {
        if (go == null || mainCamera == null) return 40f;

        // Prefab? → kurz instanziieren, messen, zerstören
        bool isPrefab = !go.scene.IsValid();
        GameObject target = isPrefab
            ? Instantiate(go, new Vector3(10000f, 10000f, 0f), Quaternion.identity)
            : go;

        float half = 40f;

        var col = target.GetComponentInChildren<Collider2D>();
        if (col != null)
        {
            Bounds b = col.bounds;
            Vector3 spC = mainCamera.WorldToScreenPoint(b.center);
            Vector3 spE = mainCamera.WorldToScreenPoint(b.center + new Vector3(b.extents.x, b.extents.y, 0f));
            half = Mathf.Max(half, Vector2.Distance(spC, spE));
        }

        var sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds;
            Vector3 spC = mainCamera.WorldToScreenPoint(b.center);
            Vector3 spE = mainCamera.WorldToScreenPoint(b.center + new Vector3(b.extents.x, b.extents.y, 0f));
            half = Mathf.Max(half, Vector2.Distance(spC, spE));
        }

        if (isPrefab) Destroy(target);

        return half;
    }


    // ─── Fake Point ────────────────────────────────────────────────────────────

    // Spawnt mit fakeSpawnChance ein Fake-Element parallel zum echten Tap-Point.
    // Nur wenn noch kein Fake existiert und eine Position ohne Overlap gefunden wird.
    private void TrySpawnFake(GameObject realPoint, float lifetime)
    {
        if (ActiveFakePrefab == null) return;
        if (currentFake != null) return;                 // schon ein Fake da
        if (Random.value > fakeSpawnChance) return;      // Chance verfehlt

        if (!TryFindFakePosition(realPoint, out Vector3 worldPos)) return;

        currentFake = Instantiate(ActiveFakePrefab, worldPos, Quaternion.identity);

        var fp = currentFake.GetComponent<FakePoint>();
        if (fp != null) fp.Activate(lifetime);
    }

    // Räumt das aktuelle Fake (wenn das Original getappt/geräumt wurde).
    private void DismissCurrentFake()
    {
        if (currentFake == null) return;
        var fp = currentFake.GetComponent<FakePoint>();
        if (fp != null) fp.Dismiss();
        else Destroy(currentFake);
        currentFake = null;
    }

    // Sucht eine Position im erlaubten Spawn-Bereich, die weit genug vom echten
    // Point entfernt ist (kein optisches Überlappen).
    private bool TryFindFakePosition(GameObject realPoint, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        Rect allowedScreen   = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);

        float   fakeHalfPx = GetHalfSizePixels(ActiveFakePrefab);
        Vector2 realVP     = mainCamera.WorldToViewportPoint(realPoint.transform.position);
        float   realHalfPx = GetHalfSizePixels(realPoint);

        for (int i = 0; i < 40; i++)
        {
            Vector2 vp = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            if (IsFarEnoughFromOrb(vp, realVP, fakeHalfPx, realHalfPx))
            {
                worldPos = ViewportToWorldOnZ0(vp);
                return true;
            }
        }

        return false; // keine überlappungsfreie Position → diesmal kein Fake
    }

    // ─── CreatePoint & PointCleared ───────────────────────────────────────────

    public void CreatePoint(GameObject prefab, Vector3 worldPos)
    {
        StopPointTimer();
        DismissCurrentFake();  // Altes Fake räumen (deckt auch Original-Timeout ab)

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var tap = newPoint.GetComponent<TapPoint>();
        if (tap) tap.spawner = this;

        var swipe = newPoint.GetComponent<SwipePoint>();
        if (swipe) { swipe.spawner = this; CurrentSwipePoint = swipe; }
        else { CurrentSwipePoint = null; }

        lastPoint = newPoint;
        currentPoint = newPoint;


        if (IsInfinityMode && !IsTutorialMode)
        {
            float dynamicTime = CurrentReactionTime;
            timeoutRoutine = StartCoroutine(Co_PointTimeout(newPoint, dynamicTime, useUnscaledTime));
            if (debugLogs) Debug.Log($"[Spawner] Timer gestartet: {dynamicTime:F2}s (Intensität={(levelUp != null ? levelUp.CurrentLevel : 0)})");

            var fuse = newPoint.GetComponentInChildren<FuseCountdown>();
            if (fuse) fuse.StartBurn(dynamicTime);

            var lineFuse = newPoint.GetComponentInChildren<LineFuse>();
            if (lineFuse) lineFuse.StartBurn(dynamicTime);

            var sparks = newPoint.GetComponentInChildren<BurnSparks>();
            if (sparks)
            {
                bool isTapPoint = newPoint.GetComponent<TapPoint>() != null;
                sparks.SetQuadMode(isTapPoint);
                sparks.StartBurn(dynamicTime);
            }

            var countdownSquare = newPoint.GetComponentInChildren<CountdownSquare>();
            if (countdownSquare) countdownSquare.StartCountdown(dynamicTime);

            var pulse = newPoint.GetComponent<PointPulse>();
            if (pulse) pulse.StartPulsing();

            // Fake-Element parallel zum echten Tap-Point (10% Chance, max 1, kein Overlap)
            if (tap != null) TrySpawnFake(newPoint, dynamicTime);
        }
        else if (!IsTutorialMode)
        {
            if (debugLogs) Debug.Log("[Spawner] Kein Timer gestartet.");
        }

        if (portalFlash != null)
        {
            portalFlash.FlashParticles();
        }
    }

    public void PointCleared(GameObject point)
    {
        Debug.Log($"[PointCleared] START | converting={isConvertingPoints} | point={point.name}");
        if (isConvertingPoints && point != currentPoint)
        {
            Debug.Log("[PointCleared] ABORTED wegen isConvertingPoints");
            return;
        }

        if (point == currentPoint)
        {
            StopPointTimer(); currentPoint = null;
        }
        if (CurrentSwipePoint != null && point == CurrentSwipePoint.gameObject) CurrentSwipePoint = null;

        Destroy(point);

        SpawnNextPoint();
    }

    public bool ForceClearCurrentPoint()
    {
        if (currentPoint != null)
        {
            HandlePointHit(currentPoint);
            return true;
        }
        return false;
    }

    // Gibt den aktuellen Point zurück und entfernt ihn aus dem Spawner-Tracking.
    // Collider wird deaktiviert damit er nicht mehr tappbar ist.
    // Der Aufrufer ist verantwortlich für die Destroy.
    public GameObject StealCurrentPoint()
    {
        if (currentPoint == null) return null;

        StopPointTimer();

        var col = currentPoint.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var stolen = currentPoint;
        currentPoint = null;
        CurrentSwipePoint = null;
        return stolen;
    }

    // ─── Countdown ────────────────────────────────────────────────────────────

    private IEnumerator Co_PointTimeout(GameObject point, float seconds, bool unscaled)
    {
        float t = 0f;
        while (t < seconds && running && !gameOver)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (point == null || point != currentPoint) yield break;
            yield return null;
        }

        if (running && !gameOver && point != null && point == currentPoint)
        {
            DismissCurrentFake();  // Original abgelaufen → Fake gleichzeitig verpuffen

            if (LivesManager.Instance != null)
            {
                Vector3 pointPos = point.transform.position;
                // Point sofort zerstören (kein Score)
                // StopPointTimer() NICHT aufrufen — würde diese Coroutine sofort abbrechen
                timeoutRoutine = null;
                Destroy(currentPoint);
                currentPoint = null;
                CurrentSwipePoint = null;

                ComboManager.Instance?.RegisterMiss();

                bool stillAlive = LivesManager.Instance.LoseLife(pointPos);
                if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
                if (stillAlive)
                {
                    // Warten bis VFX + Herz-Animation fertig, dann nächsten Point spawnen
                    yield return new WaitForSeconds(LivesManager.Instance.TotalLoseDuration);
                    SpawnNextPoint();
                    yield break;
                }
                float delay = LivesManager.Instance?.TotalGameOverAnimDuration ?? 0f;
                yield return new WaitForSecondsRealtime(delay);
            }
            GameOver();
        }
    }

    private void StopPointTimer()
    {
        if (timeoutRoutine != null)
        {
            StopCoroutine(timeoutRoutine);
            timeoutRoutine = null;
        }
    }


    // ─── Game Over ────────────────────────────────────────────────────────────

    private async void EndGame(int score)
    {
        if (gameOver) return;
        gameOver = true;

        running = false;
        spawnPausedForBanner = false;
        StopPointTimer();

        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; }
        CurrentSwipePoint = null;

        if (GravityModeSystem.Instance != null) GravityModeSystem.Instance.ForceStop();
        if (FountainModeSystem.Instance != null) FountainModeSystem.Instance.ForceStop();
        PhaseManager.Instance?.StopRun();
        ComboManager.Instance?.ResetCombo();

        if (MultiplayerManager.IsMultiplayerGame)
        {
            MultiplayerGameSession.Instance?.DeclareLocalPlayerLost();
            return;
        }

        Debug.Log("GAME OVER ERREICHT");

        if (ScreenShakeManager.Instance != null)
            ScreenShakeManager.Instance.Shake(0.3f, 0.2f);

        InAppReviewManager.Instance?.OnGameFinished();

        MusicManager.Instance?.ResetGameOnGameOver();
        SfxManager.Instance?.PlayInfinityGameOver();

        NeonAnalytics.LogGameOver(CurrentMode, score, _gameOverCause);
        _gameOverCause = "timeout";

        AchievementManager.OnGameFinished(score, CurrentMode);
        MissionManager.OnGameFinished(score);

        onGameOver?.Invoke();
        uiManager?.ShowGameOver(score);

        try
        {
            bool uploaded = await HighscoreUploader.TrySubmitAsync(score, LeaderboardApi.InfinityId);
            if (uploaded)
            {
                NeonAnalytics.LogHighscoreBeat(CurrentMode, score);
                Debug.Log($"[LB] Infinity-Bestwert {score} hochgeladen.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LB] Upload fehlgeschlagen: {e.Message}");
        }
    }

    private string _gameOverCause = "timeout";

    private void GameOver()
    {
        EndGame(CurrentScore);
    }

    public void TriggerGameOverFromGravity()
    {
        _gameOverCause = "gravity";
        StartCoroutine(Co_DelayedGameOver());
    }

    private IEnumerator Co_DelayedGameOver()
    {
        float delay = LivesManager.Instance?.TotalGameOverAnimDuration ?? 0f;
        yield return new WaitForSecondsRealtime(delay);
        GameOver();
    }

    public void StopImmediate()
    {
        if (gameOver) return;
        gameOver = true;
        running = false;
        StopPointTimer();
        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; }
        CurrentSwipePoint = null;
        if (GravityModeSystem.Instance != null) GravityModeSystem.Instance.ForceStop();
        if (FountainModeSystem.Instance != null) FountainModeSystem.Instance.ForceStop();
    }

    // ─── Spawn-Area ───────────────────────────────────────────────────────────

    private Rect GetAllowedSpawnRect()
    {
        Rect sa = useSafeAreaForSpawns ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);

        float left = Mathf.Lerp(sa.xMin, sa.xMax, leftPercent);
        float right = Mathf.Lerp(sa.xMin, sa.xMax, 1f - rightPercent);
        float bottom = Mathf.Lerp(sa.yMin, sa.yMax, bottomPercent) + extraBottomGesturePixels;
        float top = Mathf.Lerp(sa.yMin, sa.yMax, 1f - topPercent);

        left += spawnPaddingPixels;
        right -= spawnPaddingPixels;
        bottom += spawnPaddingPixels;
        top -= spawnPaddingPixels;

        float minW = 100f, minH = 100f;
        left = Mathf.Clamp(left, 0, Screen.width - minW);
        right = Mathf.Clamp(right, left + minW, Screen.width);
        bottom = Mathf.Clamp(bottom, 0, Screen.height - minH);
        top = Mathf.Clamp(top, bottom + minH, Screen.height);

        return Rect.MinMaxRect(left, bottom, right, top);
    }

    private static Rect ScreenRectToViewportRect(Rect r)
    {
        return new Rect(r.x / Screen.width, r.y / Screen.height,
                        r.width / Screen.width, r.height / Screen.height);
    }

    /// <summary>Spawn-Viewport für Activation Orbs – nur oberer Bereich.</summary>
    private Rect GetOrbSpawnViewport()
    {
        Rect vp = ScreenRectToViewportRect(GetAllowedSpawnRect());
        float newYMin = Mathf.Lerp(vp.yMin, vp.yMax, orbSpawnMinViewportY);
        return Rect.MinMaxRect(vp.xMin, newYMin, vp.xMax, vp.yMax);
    }

    private Vector2 GetRandomViewportPosition(Rect allowedViewport)
    {
        float minDistPixels = GetBaseMinDistancePixels();

        Vector2 candidateVP;
        int attempts = 0;
        do
        {
            candidateVP = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );
            attempts++;

            if (lastPoint == null || attempts >= 20) break;

            Vector2 lastVP = mainCamera.WorldToViewportPoint(lastPoint.transform.position);
            Vector2 candidatePx = candidateVP * new Vector2(Screen.width, Screen.height);
            Vector2 lastPx = lastVP * new Vector2(Screen.width, Screen.height);

            if (Vector2.Distance(candidatePx, lastPx) >= minDistPixels) break;

        } while (true);

        candidateVP.x = Mathf.Clamp(candidateVP.x, allowedViewport.xMin, allowedViewport.xMax);
        candidateVP.y = Mathf.Clamp(candidateVP.y, allowedViewport.yMin, allowedViewport.yMax);
        return candidateVP;
    }

    private Vector3 ViewportToWorldOnZ0(Vector2 viewportPos)
    {
        var ray = mainCamera.ViewportPointToRay(new Vector3(viewportPos.x, viewportPos.y, 0f));
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            var p = ray.GetPoint(enter);
            p.z = 0f;
            return p;
        }
        var fb = mainCamera.ViewportToWorldPoint(new Vector3(viewportPos.x, viewportPos.y, -mainCamera.transform.position.z));
        fb.z = 0f;
        return fb;
    }

    private float ComputeHalfSizePixels(GameObject prefab)
    {
        var go = Instantiate(prefab, new Vector3(10000, 10000, 0), Quaternion.identity);
        float half = 20f;

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Bounds b = sr.bounds;
            Vector3 spC = mainCamera.WorldToScreenPoint(b.center);
            Vector3 spE = mainCamera.WorldToScreenPoint(b.center + new Vector3(b.extents.x, b.extents.y, 0f));
            half = Mathf.Max(half, Vector2.Distance(spC, spE));
        }

        var rt = go.GetComponentInChildren<RectTransform>();
        if (rt != null)
            half = Mathf.Max(half, 0.5f * Mathf.Max(rt.rect.size.x, rt.rect.size.y));

        Destroy(go);
        return half;
    }

    private IEnumerator InitialOrbDelayRoutine()
    {
        float delay = Random.Range(initialOrbDelayMin, initialOrbDelayMax);
        yield return new WaitForSeconds(delay);
        activationOrbOnCooldown = false;
    }


    private IEnumerator SharedOrbCooldownRoutine()
    {
        yield return new WaitForSeconds(activationOrbCooldown);
        activationOrbOnCooldown = false;
    }

    private void StartSharedCooldown()
    {
        activationOrbOnCooldown = true;
        StartCoroutine(SharedOrbCooldownRoutine());
    }

    // ─── Activation Orb (gemeinsam) ───────────────────────────────────────────

    private bool TrySpawnActivationOrb()
    {
        if (activationOrbOnCooldown) return false;
        if (currentActivationPoint != null) return false;

        // Tutorial erzwingt immer Gravity-Orb an fester Position
        if (TutorialManager.IsWaitingForTutorialOrb)
            return TrySpawnGravityModePoint();

        if (SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive)
            return false;

        return TrySpawnGravityModePoint() || TrySpawnFountainModePoint();
    }

    // ─── Gravity Mode ─────────────────────────────────────────────────────────

    private bool TrySpawnGravityModePoint()
    {
        if (currentActivationPoint != null) return false;
        if (activationOrbOnCooldown) return false;
        if (lastSpawnedOrbMode == SpecialMode.Gravity) return false;
        if (!TutorialManager.IsWaitingForTutorialOrb && Random.value > 0.3f) return false;

        Vector3 worldPos;
        if (TutorialManager.IsWaitingForTutorialOrb)
        {
            worldPos = ViewportToWorldOnZ0(TutorialManager.TutorialOrbViewport);
        }
        else
        {
            Rect allowedViewport = GetOrbSpawnViewport();
            float orbHalf = GetHalfSizePixels(gravityModeActivationPointPrefab);
            Vector2 vp = Vector2.zero;
            int attempts = 0;
            do
            {
                vp = GetRandomViewportPosition(allowedViewport);
                attempts++;
                if (IsFarEnoughFromCurrentPoint(vp, orbHalf)) break;
            } while (attempts < 20);
            worldPos = ViewportToWorldOnZ0(vp);
        }

        var orb = Instantiate(gravityModeActivationPointPrefab, worldPos, Quaternion.identity);
        var script = orb.GetComponent<GravityModeActivationPoint>();
        if (script != null) script.spawner = this;

        currentActivationPoint = orb;
        lastSpawnedOrbMode = SpecialMode.Gravity;
        StartSharedCooldown();
        return true;
    }


    // ─── Utility / Public ─────────────────────────────────────────────────────

    public void PauseSpawning(bool pause)
    {
        spawnPausedForBanner = pause;
        if (pause) StopPointTimer();
    }

    public void HandlePointHit(GameObject point)
    {
        DismissCurrentFake();  // Original getappt → Fake lautlos entfernen

        ScoreManager.Instance?.AddPointsFromHit();
        ComboManager.Instance?.RegisterHit();

        var basePoint = point.GetComponent<BasePoint>();
        if (basePoint != null) basePoint.SendMessage("SpawnExplosion");
        PointCleared(point);
    }

    // Vom PeekABooSystem: registriert das Swipe-Peek-Element für den Swipe-Input.
    public void SetPeekSwipePoint(SwipePoint sp)
    {
        CurrentSwipePoint = sp;
    }

    public void ResetCurrentPointTimer()
    {
        if (currentPoint == null) return;
        StopPointTimer();

        if (IsInfinityMode)
        {
            float dynamicTime = CurrentReactionTime;
            timeoutRoutine = StartCoroutine(Co_PointTimeout(currentPoint, dynamicTime, useUnscaledTime));
        }
    }

    /// <summary>
    /// Phasenende: alle noch aktiven Elemente POSITIV auflösen — als hätte der Spieler sie korrekt
    /// bedient. Tap/Swipe/Gravity = Erfolg (Punkte + Combo); Shocker = Zeit auslaufen lassen;
    /// Fake = ignorieren. Voraussetzung: Spawning ist pausiert (SetBannerPause(true)) → kein Nachspawn.
    /// </summary>
    public void PositiveClearAll()
    {
        // Fake: positiv = ignorieren (lautlos entfernen, kein Schaden)
        foreach (var f in FindObjectsByType<FakePoint>(FindObjectsSortMode.None))
            f.Dismiss();

        // Shocker: positiv = Zeit auslaufen lassen (sicheres Verpuffen)
        foreach (var s in FindObjectsByType<ThunderPoint>(FindObjectsSortMode.None))
            s.Vanish();

        // Tap & Swipe: als Erfolg auflösen (Punkte + Combo)
        foreach (var sp in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
            HandlePointHit(sp.gameObject);
        foreach (var tp in FindObjectsByType<TapPoint>(FindObjectsSortMode.None))
            HandlePointHit(tp.gameObject);

        // Gravity & Fountain: normale = Erfolg (Punkte+Combo); Shocker/Fake = sicher verpuffen (nicht tappen!)
        foreach (var gp in FindObjectsByType<GravityPoint>(FindObjectsSortMode.None))
        {
            if (gp.IsShocker || gp.IsFake) gp.DissolveNoPenalty(); else gp.TryTap();
        }
        foreach (var fp2 in FindObjectsByType<FountainPoint>(FindObjectsSortMode.None))
        {
            if (fp2.IsShocker || fp2.IsFake) fp2.DissolveNoPenalty(); else fp2.TryTap();
        }

        currentPoint = null;
        CurrentSwipePoint = null;
    }

    /// <summary>True, solange noch irgendein spielbares Element in der Szene ist (currentPoint oder
    /// frei fliegende Tap/Swipe/Gravity/Fountain/Fake/Thunder/Peek-Elemente). Genutzt vom PhaseManager,
    /// um am Phasenende die Restelemente natürlich auslaufen zu lassen, bevor es weitergeht.</summary>
    public bool HasActiveGameplayPoints()
    {
        if (currentPoint != null || CurrentSwipePoint != null) return true;
        if (FindFirstObjectByType<TapPoint>()      != null) return true;
        if (FindFirstObjectByType<SwipePoint>()    != null) return true;
        if (FindFirstObjectByType<GravityPoint>()  != null) return true;
        if (FindFirstObjectByType<FountainPoint>() != null) return true;
        if (FindFirstObjectByType<FakePoint>()     != null) return true;
        if (FindFirstObjectByType<ThunderPoint>()  != null) return true;
        if (FindFirstObjectByType<PeekElement>()   != null) return true;
        return false;
    }

    public void ClearAllGameplayPoints()
    {
        ForceClearCurrentPoint();

        foreach (var s in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
            Destroy(s.gameObject);

        foreach (var t in FindObjectsByType<TapPoint>(FindObjectsSortMode.None))
            Destroy(t.gameObject);

        currentPoint = null;
        CurrentSwipePoint = null;
    }

    public void ClearAllActivationOrbs()
    {
        foreach (var orb in FindObjectsByType<GravityModeActivationPoint>(FindObjectsSortMode.None))
            Destroy(orb.gameObject);
    }

    public void ClearActivationPoint()
    {
        currentActivationPoint = null;
    }

    /// <summary>Vom PhaseManager: den Activation-Orb des gewählten Modus spawnen. Der Orb spielt
    /// seine Animation selbst ab und ruft am Ende StartMode(mode) auf.</summary>
    public void SpawnActivationOrb(SpecialMode mode)
    {
        GameObject prefab = mode == SpecialMode.Fountain
            ? fountainModeActivationPointPrefab
            : gravityModeActivationPointPrefab;
        if (prefab == null) { Debug.LogWarning($"[Spawner] Kein Activation-Orb-Prefab für {mode}."); return; }

        Vector3 pos = ViewportToWorldOnZ0(new Vector2(0.5f, 0.5f));
        var orb = Instantiate(prefab, pos, Quaternion.identity);

        var g = orb.GetComponent<GravityModeActivationPoint>();  if (g != null) g.spawner = this;
        var f = orb.GetComponent<FountainModeActivationPoint>(); if (f != null) f.spawner = this;

        currentActivationPoint = orb;
    }

    public bool IsLevelUpActive()
    {
        return levelUp != null && levelUp.IsShowingPanel;
    }

    // ── Tutorial-Kontrolle ────────────────────────────────────────────────────

    /// <summary>
    /// Gibt eine zufällige, gültige Spawn-Position zurück (wie im echten Spiel).
    /// Wird vom Tutorial für stille Schritte genutzt.
    /// </summary>
    public Vector3 GetRandomSpawnWorldPos()
    {
        Rect allowedViewport = ScreenRectToViewportRect(GetAllowedSpawnRect());
        Vector2 vp = new Vector2(
            Random.Range(allowedViewport.xMin, allowedViewport.xMax),
            Random.Range(allowedViewport.yMin, allowedViewport.yMax)
        );
        return ViewportToWorldOnZ0(vp);
    }

    /// <summary>
    /// Spawnt gezielt einen Tap- oder SwipePoint an gegebener Position (Tutorial).
    /// lockUntilOverlay=true: Collider wird gesperrt bis UnlockCurrentPoint() aufgerufen wird
    ///                        (für Schritte mit Erklärungstext).
    /// lockUntilOverlay=false: Point ist sofort interaktiv (für stille Schritte).
    /// </summary>
    public void ForceTutorialSpawn(bool isTap, Vector3 worldPos,
                                   SwipeDirection? forcedDir = null,
                                   bool lockUntilOverlay = true)
    {
        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; CurrentSwipePoint = null; }
        StopPointTimer();

        GameObject prefab = isTap ? ActiveNormalPrefab : ActiveSwipePrefab;

        if (portalBeam != null)
        {
            if (lockUntilOverlay)
            {
                // Beam feuern → nach Ankunft sperren (Callback)
                SwipeDirection? dir = forcedDir;
                portalBeam.SpawnWithBeam(prefab, worldPos, () => LockTutorialPoint(isTap, dir));
            }
            else
            {
                // Stiller Schritt: Beam feuern, kein Sperren – Point sofort interaktiv
                portalBeam.SpawnWithBeam(prefab, worldPos);
            }
        }
        else
        {
            CreatePoint(prefab, worldPos);
            if (lockUntilOverlay)
                LockTutorialPoint(isTap, forcedDir);
        }
    }

    private void LockTutorialPoint(bool isTap, SwipeDirection? forcedDir)
    {
        if (currentPoint != null)
        {
            var col = currentPoint.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
        if (CurrentSwipePoint != null)
        {
            _lockedSwipePoint = CurrentSwipePoint;
            CurrentSwipePoint = null;
        }
        if (!isTap && forcedDir.HasValue && _lockedSwipePoint != null)
            _lockedSwipePoint.SetDirection(forcedDir.Value);
    }

    /// <summary>
    /// Gibt den aktuellen Tutorial-Point für Interaktion frei
    /// (Collider aktivieren + SwipePoint-Referenz wiederherstellen).
    /// </summary>
    public void UnlockCurrentPoint()
    {
        if (currentPoint != null)
        {
            var col = currentPoint.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }
        if (_lockedSwipePoint != null)
        {
            CurrentSwipePoint = _lockedSwipePoint;
            _lockedSwipePoint = null;
        }
    }

    /// <summary>Registriert einen vom TutorialManager manuell gespawnten Orb im Tracking.</summary>
    public void RegisterTutorialOrb(GameObject orb)
    {
        currentActivationPoint = orb;
    }

    private bool TrySpawnFountainModePoint()
    {
        if (currentActivationPoint != null) return false;
        if (activationOrbOnCooldown) return false;
        if (lastSpawnedOrbMode == SpecialMode.Fountain) return false;
        if (TutorialManager.IsWaitingForTutorialOrb) return false;
        if (Random.value > 0.3f) return false;

        Rect allowedViewport = GetOrbSpawnViewport();
        Vector3 worldPos = ViewportToWorldOnZ0(GetRandomViewportPosition(allowedViewport));

        var orb = Instantiate(fountainModeActivationPointPrefab, worldPos, Quaternion.identity);
        var script = orb.GetComponent<FountainModeActivationPoint>();
        if (script != null) script.spawner = this;

        currentActivationPoint = orb;
        lastSpawnedOrbMode = SpecialMode.Fountain;
        StartSharedCooldown();
        return true;
    }
}