using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MixedPointSpawner : MonoBehaviour
{
    [SerializeField] private GameObject fountainModeActivationPointPrefab;
    [SerializeField] private GameObject normalPointGoldPrefab;
    [SerializeField] private GameObject swipePointGoldPrefab;

    [SerializeField] private GameObject gravityModeActivationPointPrefab;

    [SerializeField] private GameUIManager uiManager;
    private GameObject currentActivationPoint;


    private int CurrentScore =>
    ScoreManager.Instance ? ScoreManager.Instance.CurrentScore : 0;

    [Header("Gold Mode")]
    [SerializeField] private GameObject goldModeActivationPointPrefab;
    [SerializeField] private int goldModeSpawnScoreThreshold = 5;
    [SerializeField] private float goldModeSpawnChance = 1f;
    private GameObject currentGoldModePoint;

    [Header("Activation Orb Cooldown (geteilt)")]
    [SerializeField] private float activationOrbCooldown = 60f;
    private bool activationOrbOnCooldown = false;
    private SpecialMode lastSpawnedOrbMode = SpecialMode.None;
    private bool isConvertingPoints = false;


    [SerializeField] private ArcanePortalFlash portalFlash;

    [SerializeField] private PortalSpawnBeam portalBeam;

    private GameMode CurrentMode =>
        GlobalGameManager.Instance ? GlobalGameManager.Instance.SelectedMode : GameMode.Time;

    private bool IsInfinityMode => CurrentMode == GameMode.Infinity;

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

    [Header("Auto-Padding (empfohlen)")]
    [SerializeField] private bool autoComputePaddingFromPrefab = true;
    [SerializeField] private GameObject paddingSamplePrefab;
    [SerializeField] private float extraPaddingPixels = 12f;

    [Header("Prefabs & Refs")]
    public GameObject normalPointPrefab;
    public GameObject swipePointPrefab;
    public Camera mainCamera;

    [Header("Start/Timing")]
    public bool autoStart = false;
    public float respawnDelay = 0f;

    [Header("Countdown / Game Over")]
    public float reactionTime = 3f;
    public bool useUnscaledTime = false;
    public UnityEvent onGameOver;

    [SerializeField] private LevelUp levelUp;

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

    public void SpawnNextPoint()
    {
        
        if (levelUp != null && levelUp.IsShowingPanel) return;
        if (!running || spawnPausedForBanner || currentPoint != null || isConvertingPoints) return;

        bool forceSwipe = maxNormalsInRow > 0 && normalsInRow >= maxNormalsInRow;
        bool forceNormal = maxSwipesInRow > 0 && swipesInRow >= maxSwipesInRow;

        GameObject prefabToSpawn;
        bool spawnSwipe;

        if (forceSwipe) spawnSwipe = true;
        else if (forceNormal) spawnSwipe = false;
        else spawnSwipe = Random.value < swipeChance;

        

        if (GoldModeSystem.Instance != null && GoldModeSystem.Instance.IsActive)
        {
            prefabToSpawn = spawnSwipe ? swipePointGoldPrefab : normalPointGoldPrefab;
        }
        else
        {
            prefabToSpawn = spawnSwipe ? swipePointPrefab : normalPointPrefab;
        }

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

        int maxAttempts = (currentActivationPoint != null || currentGoldModePoint != null) ? 80 : 40;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            viewportPos = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            attempts++;

            bool farFromLast = true;
            bool farFromGold = true;
            bool farFromActivation = true;

            // Abstand zu letztem Punkt
            if (lastPoint != null)
            {
                Vector2 lastVP = mainCamera.WorldToViewportPoint(lastPoint.transform.position);
                farFromLast = IsFarEnough(viewportPos, lastVP);
            }

            // Abstand zu Gold Orb — mit Größen beider Objekte
            if (currentGoldModePoint != null)
            {
                Vector2 goldVP = mainCamera.WorldToViewportPoint(currentGoldModePoint.transform.position);
                float goldHalfSizePx = GetHalfSizePixels(currentGoldModePoint);
                farFromGold = IsFarEnoughFromOrb(viewportPos, goldVP, spawnPointHalfSizePx, goldHalfSizePx);
            }

            // Abstand zu Activation Orb (Gravity etc.) — mit Größen beider Objekte
            if (currentActivationPoint != null)
            {
                Vector2 activationVP = mainCamera.WorldToViewportPoint(currentActivationPoint.transform.position);
                float activationHalfSizePx = GetHalfSizePixels(currentActivationPoint);
                farFromActivation = IsFarEnoughFromOrb(viewportPos, activationVP, spawnPointHalfSizePx, activationHalfSizePx);
            }

            if (farFromLast && farFromGold && farFromActivation)
            {
                foundValid = true;
                break;
            }
        }

        if (!foundValid && debugLogs)
            Debug.LogWarning("[Spawner] Kein gültiger Spawn gefunden → fallback Mitte");

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);

        if (portalBeam != null)
        {
            portalBeam.SpawnWithBeam(prefabToSpawn, worldPos);
        }
        else
        {
            CreatePoint(prefabToSpawn, worldPos);
        }
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
    /// Wird von TrySpawnGoldModePoint / TrySpawnGravityModePoint genutzt.
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


    // ─── CreatePoint & PointCleared ───────────────────────────────────────────

    public void CreatePoint(GameObject prefab, Vector3 worldPos)
    {
        StopPointTimer();

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var tap = newPoint.GetComponent<TapPoint>();
        if (tap) tap.spawner = this;

        var swipe = newPoint.GetComponent<SwipePoint>();
        if (swipe) { swipe.spawner = this; CurrentSwipePoint = swipe; }
        else { CurrentSwipePoint = null; }

        lastPoint = newPoint;
        currentPoint = newPoint;

        if (IsInfinityMode)
        {
            int score = CurrentScore;
            float dynamicTime = levelUp.GetReactionTimeForScore(score, reactionTime);

            timeoutRoutine = StartCoroutine(Co_PointTimeout(newPoint, dynamicTime, useUnscaledTime));
            if (debugLogs) Debug.Log($"[Spawner] Timer gestartet: {dynamicTime:F2}s (Score={score}, Mode=Infinity)");
        }
        else
        {
            if (debugLogs) Debug.Log("[Spawner] Kein Timer gestartet (Mode=Time).");
        }

        TrySpawnGoldModePoint();
        TrySpawnGravityModePoint();
        TrySpawnFountainModePoint();

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

        if (IsInfinityMode)
        {
            int score = CurrentScore;
            if (levelUp.TryTriggerLevelUp(score))
            {
                MusicManager.Instance?.IncreaseGameMusicSpeed();
                StartCoroutine(LevelRoutine());
                return;
            }
        }

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

    private IEnumerator LevelRoutine()
    {
        SpawnNextPoint();
        yield break;
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
            GameOver();
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

    private async void EndGame(int score, bool isInfinityMode)
    {
        if (gameOver) return;
        gameOver = true;

        running = false;
        spawnPausedForBanner = false;
        StopPointTimer();

        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; }
        CurrentSwipePoint = null;

        Debug.Log(isInfinityMode ? "GAME OVER ERREICHT" : "TIME MODE FINISHED");

        ScreenShakeManager.Instance?.Shake(
            isInfinityMode ? 0.3f : 0.2f,
            isInfinityMode ? 0.2f : 0.15f
        );

        InAppReviewManager.Instance?.OnGameFinished();

        if (isInfinityMode)
        {
            MusicManager.Instance?.ResetGameOnGameOver();
            SfxManager.Instance?.PlayInfinityGameOver();
        }

        onGameOver?.Invoke();
        uiManager?.ShowGameOver(score, isInfinityMode);

        if (isInfinityMode)
        {
            try
            {
                bool uploaded = await HighscoreUploader.TrySubmitAsync(score, LeaderboardApi.InfinityId);
                if (uploaded)
                    Debug.Log($"[LB] Infinity-Bestwert {score} hochgeladen.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LB] Upload fehlgeschlagen: {e.Message}");
            }
        }
    }

    private void GameOver()
    {
        EndGame(CurrentScore, true);
    }

    public void ShowFinishedFromTimeMode(int finalScore)
    {
        EndGame(finalScore, false);
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


    // ─── Gold Mode ────────────────────────────────────────────────────────────

    public void OnGoldModePointDestroyed()
    {
        currentGoldModePoint = null;
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

    private void TrySpawnGoldModePoint()
    {
        if (levelUp != null && levelUp.IsShowingPanel) return;
        if (currentActivationPoint != null) return;
        if (currentGoldModePoint != null || activationOrbOnCooldown) return;
        if (lastSpawnedOrbMode == SpecialMode.Gold) return;
        if (ScoreManager.Instance == null) return;

        int score = CurrentScore;
        if (score < goldModeSpawnScoreThreshold) return;
        if (Random.value > goldModeSpawnChance) return;

        Rect allowedViewport = ScreenRectToViewportRect(GetAllowedSpawnRect());
        float goldOrbHalfSizePx = GetHalfSizePixels(goldModeActivationPointPrefab);

        Vector2 viewportPos = Vector2.zero;
        int attempts = 0;
        do
        {
            viewportPos = GetRandomViewportPosition(allowedViewport);
            attempts++;
            if (IsFarEnoughFromCurrentPoint(viewportPos, goldOrbHalfSizePx)) break;
        } while (attempts < 20);

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);

        var goldModePoint = Instantiate(goldModeActivationPointPrefab, worldPos, Quaternion.identity);
        var goldModeScript = goldModePoint.GetComponent<GoldModeActivationPoint>();
        if (goldModeScript != null) goldModeScript.spawner = this;

        currentGoldModePoint = goldModePoint;
        currentActivationPoint = goldModePoint;
        lastSpawnedOrbMode = SpecialMode.Gold;

        StartSharedCooldown();
    }


    // ─── Gravity Mode ─────────────────────────────────────────────────────────

    private void TrySpawnGravityModePoint()
    {
        if (levelUp != null && levelUp.IsShowingPanel) return;
        if (currentActivationPoint != null) return;
        if (activationOrbOnCooldown) return;
        if (lastSpawnedOrbMode == SpecialMode.Gravity) return;

        if (SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive)
            return;

        if (Random.value > 0.3f) return;

        Rect allowedViewport = ScreenRectToViewportRect(GetAllowedSpawnRect());
        float gravityOrbHalfSizePx = GetHalfSizePixels(gravityModeActivationPointPrefab);

        Vector2 vp = Vector2.zero;
        int attempts = 0;
        do
        {
            vp = GetRandomViewportPosition(allowedViewport);
            attempts++;
            if (IsFarEnoughFromCurrentPoint(vp, gravityOrbHalfSizePx)) break;
        } while (attempts < 20);

        Vector3 worldPos = ViewportToWorldOnZ0(vp);

        var orb = Instantiate(gravityModeActivationPointPrefab, worldPos, Quaternion.identity);
        var script = orb.GetComponent<GravityModeActivationPoint>();
        if (script != null) script.spawner = this;

        currentActivationPoint = orb;
        lastSpawnedOrbMode = SpecialMode.Gravity;
        StartSharedCooldown();
    }


    // ─── Utility / Public ─────────────────────────────────────────────────────

    public void PauseSpawning(bool pause)
    {
        spawnPausedForBanner = pause;
        if (pause) StopPointTimer();
    }

    public void HandlePointHit(GameObject point)
    {
        ScoreManager.Instance?.AddPointsFromHit();

        if (GoldModeSystem.Instance != null && GoldModeSystem.Instance.IsActive)
            GoldModeSystem.Instance.OnGoldPointHit();

        var basePoint = point.GetComponent<BasePoint>();
        if (basePoint != null) basePoint.SendMessage("SpawnExplosion");
        PointCleared(point);
    }

    public void ResetCurrentPointTimer()
    {
        if (currentPoint == null) return;
        StopPointTimer();

        if (IsInfinityMode)
        {
            int score = CurrentScore;
            float dynamicTime = levelUp.GetReactionTimeForScore(score, reactionTime);
            timeoutRoutine = StartCoroutine(Co_PointTimeout(currentPoint, dynamicTime, useUnscaledTime));
        }
    }

    public void ClearAllGameplayPoints()
    {
        ForceClearCurrentPoint();

        foreach (var s in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
            Destroy(s.gameObject);

        foreach (var t in FindObjectsByType<TapPoint>(FindObjectsSortMode.None))
            Destroy(t.gameObject);

        foreach (var g in FindObjectsByType<GoldModeActivationPoint>(FindObjectsSortMode.None))
            Destroy(g.gameObject);

        currentPoint = null;
        CurrentSwipePoint = null;
    }

    public void ClearAllActivationOrbs()
    {
        foreach (var orb in FindObjectsByType<GravityModeActivationPoint>(FindObjectsSortMode.None))
            Destroy(orb.gameObject);

        foreach (var orb in FindObjectsByType<GoldModeActivationPoint>(FindObjectsSortMode.None))
            Destroy(orb.gameObject);
    }

    public void ClearActivationPoint()
    {
        currentActivationPoint = null;
    }

    public bool IsLevelUpActive()
    {
        return levelUp != null && levelUp.IsShowingPanel;
    }

    private void TrySpawnFountainModePoint()
    {
        if (currentActivationPoint != null) return;
        if (activationOrbOnCooldown) return;
        if (lastSpawnedOrbMode == SpecialMode.Fountain) return;

        if (SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive)
            return;

        if (Random.value > 0.3f) return;

        Rect allowedViewport = ScreenRectToViewportRect(GetAllowedSpawnRect());
        Vector2 vp = GetRandomViewportPosition(allowedViewport);
        Vector3 worldPos = ViewportToWorldOnZ0(vp);

        var orb = Instantiate(fountainModeActivationPointPrefab, worldPos, Quaternion.identity);
        var script = orb.GetComponent<FountainModeActivationPoint>();

        if (script != null) script.spawner = this;

        currentActivationPoint = orb;
        lastSpawnedOrbMode = SpecialMode.Fountain;
        StartSharedCooldown();
    }
}