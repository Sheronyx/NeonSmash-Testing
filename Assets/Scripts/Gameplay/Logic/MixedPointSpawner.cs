using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MixedPointSpawner : MonoBehaviour
{

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
    [SerializeField] private float goldModeCooldown = 60f;
    private bool goldModeOnCooldown = false;
    private GameObject currentGoldModePoint;
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

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);
        Vector2 viewportPos;
        int attempts = 0;

        do
        {
            viewportPos = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            attempts++;

            bool farFromLast = true;
            bool farFromGold = true;

            // Abstand zu letztem Punkt
            if (lastPoint != null)
            {
                Vector2 lastVP = mainCamera.WorldToViewportPoint(lastPoint.transform.position);
                farFromLast = IsFarEnough(viewportPos, lastVP);
            }

            // Abstand zu GoldOrb
            if (currentGoldModePoint != null)
            {
                Vector2 goldVP = mainCamera.WorldToViewportPoint(currentGoldModePoint.transform.position);
                farFromGold = IsFarEnough(viewportPos, goldVP);
            }

            if (farFromLast && farFromGold)
                break;

        } while (attempts < 30);

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);
        if (portalBeam != null)
        {
            portalBeam.SpawnWithBeam(prefabToSpawn, worldPos);
        }
        else
        {
            CreatePoint(prefabToSpawn, worldPos);
        }

        TrySpawnGoldModePoint();
        TrySpawnGravityModePoint();

        if (portalFlash != null)
        {
            portalFlash.FlashParticles();
        }
    }


    private bool IsFarEnough(Vector2 candidateVP, Vector2 targetVP)
    {
        Vector2 candidatePx = candidateVP * new Vector2(Screen.width, Screen.height);
        Vector2 targetPx = targetVP * new Vector2(Screen.width, Screen.height);

        float minDist = minDistanceAsPercent
            ? Mathf.Min(Screen.width, Screen.height) * minDistancePercent
            : minScreenDistancePixels;

        return Vector2.Distance(candidatePx, targetPx) >= minDist;
    }




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
                float newRT = levelUp.GetReactionTimeForScore(score, reactionTime);

                MusicManager.Instance?.IncreaseGameMusicSpeed();

                StartCoroutine(LevelRoutine(newRT));

                return;
            }
        }

        SpawnNextPoint();
    }


    public void ForceClearCurrentPoint()
    {
        if (currentPoint != null)
        {
            HandlePointHit(currentPoint);
        }
    }


    private IEnumerator LevelRoutine(float newRT)
    {
        spawnPausedForBanner = true;

        yield return levelUp.ShowLevelPanel(levelUp.CurrentLevel, newRT);

        spawnPausedForBanner = false;

        SpawnNextPoint();
    }

    // ---------------- Countdown ----------------

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

        // 🎯 ScreenShake
        ScreenShakeManager.Instance?.Shake(
            isInfinityMode ? 0.3f : 0.2f,
            isInfinityMode ? 0.2f : 0.15f
        );

        InAppReviewManager.Instance?.OnGameFinished();

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

            MusicManager.Instance?.ResetGameOnGameOver();
            SfxManager.Instance?.PlayInfinityGameOver();
        }

        onGameOver?.Invoke();
        uiManager?.ShowGameOver(score, isInfinityMode);
    }

    private void GameOver()
    {
        EndGame(CurrentScore, true);
    }

    public void ShowFinishedFromTimeMode(int finalScore)
    {
        EndGame(finalScore, false);
    }


    // ---------------- Spawn-Area & Debug ----------------

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
        float vx = r.x / Screen.width;
        float vy = r.y / Screen.height;
        float vw = r.width / Screen.width;
        float vh = r.height / Screen.height;
        return new Rect(vx, vy, vw, vh);
    }

    private Vector2 GetRandomViewportPosition(Rect allowedViewport)
    {
        float minDistPixels = minDistanceAsPercent
            ? Mathf.Min(Screen.width, Screen.height) * minDistancePercent
            : minScreenDistancePixels;

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

            Vector3 lastWorld = lastPoint.transform.position;
            Vector3 lastVP3 = mainCamera.WorldToViewportPoint(lastWorld);
            Vector2 lastVP = new Vector2(lastVP3.x, lastVP3.y);

            Vector2 candidatePx = new Vector2(candidateVP.x * Screen.width, candidateVP.y * Screen.height);
            Vector2 lastPx = new Vector2(lastVP.x * Screen.width, lastVP.y * Screen.height);

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
            Vector3 c = b.center;
            Vector3 wRU = c + new Vector3(b.extents.x, b.extents.y, 0f);
            Vector3 spC = mainCamera.WorldToScreenPoint(c);
            Vector3 spRU = mainCamera.WorldToScreenPoint(wRU);
            float diagHalf = Vector2.Distance(new Vector2(spC.x, spC.y), new Vector2(spRU.x, spRU.y));
            half = Mathf.Max(half, diagHalf);
        }

        var rt = go.GetComponentInChildren<RectTransform>();
        if (rt != null)
        {
            Vector2 size = rt.rect.size;
            half = Mathf.Max(half, 0.5f * Mathf.Max(size.x, size.y));
        }

        Destroy(go);
        return half;
    }

    public void OnGoldModePointDestroyed()
    {
        currentGoldModePoint = null;
    }

    private IEnumerator GoldModeCooldownRoutine()
    {
        yield return new WaitForSeconds(goldModeCooldown);
        goldModeOnCooldown = false;
    }


    public void PauseSpawning(bool pause)
    {
        spawnPausedForBanner = pause;

        if (pause)
        {
            StopPointTimer();
        }
    }


    public void HandlePointHit(GameObject point)
    {
        ScoreManager.Instance?.AddPointsFromHit();

        var basePoint = point.GetComponent<BasePoint>();
        if (basePoint != null)
        {
            basePoint.SendMessage("SpawnExplosion");
        }

        PointCleared(point);
    }

    private bool IsFarEnoughFromCurrentPoint(Vector2 candidateVP)
    {
        if (currentPoint == null) return true;

        Vector3 currentWorld = currentPoint.transform.position;
        Vector3 currentVP3 = mainCamera.WorldToViewportPoint(currentWorld);

        Vector2 currentVP = new Vector2(currentVP3.x, currentVP3.y);

        Vector2 candidatePx = new Vector2(candidateVP.x * Screen.width, candidateVP.y * Screen.height);
        Vector2 currentPx = new Vector2(currentVP.x * Screen.width, currentVP.y * Screen.height);

        float minDistPixels = minDistanceAsPercent
            ? Mathf.Min(Screen.width, Screen.height) * minDistancePercent
            : minScreenDistancePixels;

        return Vector2.Distance(candidatePx, currentPx) >= minDistPixels;
    }

    public void ResetCurrentPointTimer()
    {
        if (currentPoint == null) return;

        StopPointTimer();

        if (IsInfinityMode)
        {
            int score = CurrentScore;
            float dynamicTime = levelUp.GetReactionTimeForScore(score, reactionTime);

            timeoutRoutine = StartCoroutine(
                Co_PointTimeout(currentPoint, dynamicTime, useUnscaledTime)
            );
        }
    }

    private void TrySpawnGoldModePoint()
    {
        if (currentActivationPoint != null) return;
        if (currentGoldModePoint != null || goldModeOnCooldown) return;

        if (ScoreManager.Instance == null) return;

        int score = CurrentScore;

        if (score < goldModeSpawnScoreThreshold) return;

        if (Random.value > goldModeSpawnChance) return;

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);
        Vector2 viewportPos;
        int attempts = 0;

        do
        {
            viewportPos = GetRandomViewportPosition(allowedViewport);
            attempts++;

            if (IsFarEnoughFromCurrentPoint(viewportPos))
                break;

        } while (attempts < 20);

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);

        var goldModePoint = Instantiate(goldModeActivationPointPrefab, worldPos, Quaternion.identity);

        var goldModeScript = goldModePoint.GetComponent<GoldModeActivationPoint>();
        if (goldModeScript != null)
        {
            goldModeScript.spawner = this;
        }

        currentGoldModePoint = goldModePoint;
        currentActivationPoint = goldModePoint;

        // Cooldown starten
        goldModeOnCooldown = true;
        StartCoroutine(GoldModeCooldownRoutine());
    }



    private void TrySpawnGravityModePoint()
    {

        if (currentActivationPoint != null) return;

        if (SpecialModeManager.Instance != null &&
            SpecialModeManager.Instance.IsModeActive)
            return;

        if (Random.value > 0.3f) return; // Spawn Chance

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);

        Vector2 vp = GetRandomViewportPosition(allowedViewport);
        Vector3 worldPos = ViewportToWorldOnZ0(vp);

        var orb = Instantiate(gravityModeActivationPointPrefab, worldPos, Quaternion.identity);
        currentActivationPoint = orb;

        var script = orb.GetComponent<GravityModeActivationPoint>();
        if (script != null)
        {
            script.spawner = this;
        }
    }

    public void ClearAllGameplayPoints()
    {
        // 👉 aktueller Punkt sauber entfernen
        ForceClearCurrentPoint();

        // 👉 Swipe Points entfernen
        var swipes = FindObjectsByType<SwipePoint>(FindObjectsSortMode.None);
        foreach (var s in swipes)
            Destroy(s.gameObject);

        // 👉 Tap Points entfernen
        var taps = FindObjectsByType<TapPoint>(FindObjectsSortMode.None);
        foreach (var t in taps)
            Destroy(t.gameObject);

        // 👉 Gold Orb entfernen
        var golds = FindObjectsByType<GoldModeActivationPoint>(FindObjectsSortMode.None);
        foreach (var g in golds)
            Destroy(g.gameObject);

        // 👉 interne Referenzen resetten
        currentPoint = null;
        CurrentSwipePoint = null;
    }

    public void ClearAllActivationOrbs()
    {
        // 🔴 Gravity Orbs
        var gravityOrbs = FindObjectsByType<GravityModeActivationPoint>(FindObjectsSortMode.None);
        foreach (var orb in gravityOrbs)
            Destroy(orb.gameObject);

        // 🟡 Gold Orbs
        var goldOrbs = FindObjectsByType<GoldModeActivationPoint>(FindObjectsSortMode.None);
        foreach (var orb in goldOrbs)
            Destroy(orb.gameObject);
    }

    public void ClearActivationPoint()
{
    currentActivationPoint = null;
}
}
