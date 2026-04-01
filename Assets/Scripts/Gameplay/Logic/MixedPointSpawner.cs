using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using System.Globalization;
using System.Collections.Generic;

public class MixedPointSpawner : MonoBehaviour
{

    private int CurrentScore =>
    ScoreManager.Instance ? ScoreManager.Instance.CurrentScore : 0;

    [Header("Combo System")]
    [SerializeField] private GameObject comboPointPrefab;
    [SerializeField] private int comboSpawnScoreThreshold = 5;
    [SerializeField] private float comboSpawnChance = 0.25f;
    [SerializeField] private float comboCooldown = 5f;
    private bool comboOnCooldown = false;
    private GameObject currentComboPoint;
    private bool isConvertingPoints = false;

    [SerializeField] private float comboDuration = 10f;

    [SerializeField] private GameObject normalPointGoldPrefab;
    [SerializeField] private GameObject swipePointGoldPrefab;

    private bool isGoldModeActive = false;
    [SerializeField] private ArcanePortalFlash portalFlash;

    [SerializeField] private PortalSpawnBeam portalBeam;

    private GameMode CurrentMode =>
        GlobalGameManager.Instance ? GlobalGameManager.Instance.SelectedMode : GameMode.Time;

    private bool IsInfinityMode => CurrentMode == GameMode.Infinity;

    [Header("Safe Area / Gesten")]
    [SerializeField] private bool useSafeAreaForSpawns = true;
    [SerializeField] private float extraBottomGesturePixels = 160f;

    [Header("Spawn-Area (Prozent)")]
    [Range(0f, 0.45f)] public float leftPercent = 0.08f;
    [Range(0f, 0.45f)] public float rightPercent = 0.08f;
    [Range(0f, 0.45f)] public float topPercent = 0.10f;
    [Range(0f, 0.45f)] public float bottomPercent = 0.10f;

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

    // Laufzeit
    public SwipePoint CurrentSwipePoint { get; private set; }
    private GameObject currentPoint;
    private GameObject lastPoint;

    private int normalsInRow = 0;
    private int swipesInRow = 0;

    private bool running = false;
    private bool gameOver = false;
    private Coroutine timeoutRoutine;
    private bool spawnPausedForBanner = false;

    // === GAME OVER / FINISHED UI ===
    [Header("Game Over UI")]
    [SerializeField] private Canvas topBarCanvas;
    [SerializeField] private CanvasGroup gameOverBanner;
    [SerializeField] private TextMeshProUGUI gameOverTextTMP;
    [SerializeField] private AudioClip sfxGameOver;
    [SerializeField] private float resultPanelDelay = 2.0f;
    [SerializeField] private float gameOverBannerFade = 0.25f;
    [SerializeField] private float gameOverBannerHold = 0.6f;
    [SerializeField] private float resultPanelFade = 0.25f;

    [Header("Ergebnisfenster (per Inspector anpassbar)")]
    [SerializeField] private CanvasGroup resultPanel;
    [SerializeField] private TextMeshProUGUI resultHeadlineTMP;
    [SerializeField] private TextMeshProUGUI resultScoreTMP;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Ergebnis-Callbacks")]
    public UnityEvent onRestartRequested;
    public UnityEvent onBackToMenuRequested;

    // -------------------------------------------------
    // Hilfsfunktionen: Raycast für „Deko“-Overlays killen
    // -------------------------------------------------
    private static void MakeCanvasNonBlocking(Canvas canvasRoot)
    {
        if (!canvasRoot) return;
        var gr = canvasRoot.GetComponent<GraphicRaycaster>();
        if (gr) gr.enabled = false;
        var graphics = canvasRoot.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics) g.raycastTarget = false;
    }
    private static void MakeTransformNonBlocking(Transform t)
    {
        if (!t) return;
        var canvas = t.GetComponentInParent<Canvas>();
        if (canvas) MakeCanvasNonBlocking(canvas);
        var graphics = t.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics) g.raycastTarget = false;
    }

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

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(() =>
            {
                HideGameOverUIImmediate();
                onRestartRequested?.Invoke();
            });
        }
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(() =>
            {
                HideGameOverUIImmediate();
                onBackToMenuRequested?.Invoke();
            });
        }

        if (gameOverBanner != null) gameOverBanner.alpha = 0f;
        if (resultPanel != null) resultPanel.alpha = 0f;
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

        // 👉 GOLD MODE entscheidet Prefab
        if (isGoldModeActive)
        {
            prefabToSpawn = spawnSwipe ? swipePointGoldPrefab : normalPointGoldPrefab;
        }
        else
        {
            prefabToSpawn = spawnSwipe ? swipePointPrefab : normalPointPrefab;
        }

        if (prefabToSpawn == swipePointPrefab) { swipesInRow++; normalsInRow = 0; }
        else { normalsInRow++; swipesInRow = 0; }

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);
        Vector2 viewportPos = GetRandomViewportPosition(allowedViewport);

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);
        if (portalBeam != null)
        {
            portalBeam.SpawnWithBeam(prefabToSpawn, worldPos);
        }
        else
        {
            CreatePoint(prefabToSpawn, worldPos);
        }

        TrySpawnComboPoint();

        if (portalFlash != null)
        {
            portalFlash.FlashParticles();
        }
    }

    private void TrySpawnComboPoint()
    {
        if (currentComboPoint != null || comboOnCooldown) return;

        if (ScoreManager.Instance == null) return;

        int score = CurrentScore;

        if (score < comboSpawnScoreThreshold) return;

        if (Random.value > comboSpawnChance) return;

        Rect allowedScreen = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);
        Vector2 viewportPos = GetRandomViewportPosition(allowedViewport);

        Vector3 worldPos = ViewportToWorldOnZ0(viewportPos);

        var combo = Instantiate(comboPointPrefab, worldPos, Quaternion.identity);

        var comboScript = combo.GetComponent<ComboPoint>();
        if (comboScript != null)
        {
            comboScript.spawner = this;
        }

        currentComboPoint = combo;

        // Cooldown starten
        comboOnCooldown = true;
        StartCoroutine(ComboCooldownRoutine());
    }


    public void CreatePoint(GameObject prefab, Vector3 worldPos)
    {
        StopPointTimer();

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var click = newPoint.GetComponent<ClickablePoint>();
        if (click) click.spawner = this;

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

    // === Aufruf im Infinity-Mode (Missed/Timeout) ===
    private async void GameOver()
    {
        if (gameOver) return;
        gameOver = true;

        running = false;
        spawnPausedForBanner = false;
        StopPointTimer();

        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; }
        CurrentSwipePoint = null;

        int score = CurrentScore;

        Debug.Log("GAME OVER ERREICHT");
        if (InAppReviewManager.Instance == null)
        {
            Debug.LogError("InAppReviewManager ist NULL!!!");
        }
        else
        {
            InAppReviewManager.Instance.OnGameFinished();
        }

        if (IsInfinityMode)
        {
            try
            {
                bool uploaded = await HighscoreUploader.TrySubmitAsync(score, LeaderboardApi.InfinityId);
                if (uploaded) Debug.Log($"[LB] Infinity-Bestwert {score} zu '{LeaderboardApi.InfinityId}' hochgeladen.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LB] Infinity-Upload fehlgeschlagen: {e.Message}");
            }
        }

        if (IsInfinityMode) MusicManager.Instance?.ResetGameOnGameOver();
        SfxManager.Instance?.PlayInfinityGameOver();

        onGameOver?.Invoke();
        StartCoroutine(Co_ShowGameOverSequence(score));
    }

    // === Aufruf aus dem TIME-Mode ===
    public void ShowFinishedFromTimeMode(int finalScore)
    {
        if (gameOver) return;

        gameOver = true;
        running = false;
        spawnPausedForBanner = false;
        StopPointTimer();

        if (currentPoint != null) { Destroy(currentPoint); currentPoint = null; }
        CurrentSwipePoint = null;

        Debug.Log("TIME MODE FINISHED ERREICHT");
        InAppReviewManager.Instance?.OnGameFinished();

        onGameOver?.Invoke();
        StartCoroutine(Co_ShowGameOverSequence(finalScore));
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


    private void HideGameOverUIImmediate()
    {
        if (gameOverBanner != null) SetCGState(gameOverBanner, false);
        if (resultPanel != null) SetCGState(resultPanel, false);
        if (topBarCanvas != null) topBarCanvas.enabled = true;
    }

    private IEnumerator Co_ShowGameOverSequence(int score)
    {
        if (topBarCanvas != null) topBarCanvas.enabled = false;

        string bannerText = IsInfinityMode ? "GAME OVER" : "FINISHED";

        if (gameOverBanner != null && gameOverTextTMP != null)
        {
            gameOverTextTMP.text = bannerText;
            if (sfxGameOver != null) SfxManager.Instance?.PlayOneShot(sfxGameOver);

            yield return StartCoroutine(Co_FadeCanvasGroup(gameOverBanner, 0f, 1f, gameOverBannerFade, false));
            float tHold = 0f;
            while (tHold < gameOverBannerHold) { tHold += Time.unscaledDeltaTime; yield return null; }
            yield return StartCoroutine(Co_FadeCanvasGroup(gameOverBanner, 1f, 0f, gameOverBannerFade, false));
        }

        float tDelay = 0f;
        while (tDelay < resultPanelDelay) { tDelay += Time.unscaledDeltaTime; yield return null; }

        if (resultPanel != null)
        {
            if (resultHeadlineTMP != null) resultHeadlineTMP.text = bannerText;
            if (resultScoreTMP != null) resultScoreTMP.text = $"{score}";

            var cv = resultPanel.GetComponent<Canvas>();
            if (cv != null) cv.sortingOrder = Mathf.Max(cv.sortingOrder, 50);

            yield return StartCoroutine(Co_FadeCanvasGroup(resultPanel, 0f, 1f, resultPanelFade, true));
        }
        else
        {
            Debug.LogWarning("[Spawner] resultPanel ist nicht zugewiesen.");
        }
    }

    [SerializeField] private float restartMinFadeDelay = 0.05f;

    public void RestartScene()
    {
        MusicManager.ForceRestartGameMusicNextLoad = true;

        string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(current, restartMinFadeDelay);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(current, LoadSceneMode.Single);
    }

    private void SetCGState(CanvasGroup cg, bool visibleInteractable)
    {
        if (!cg) return;
        if (visibleInteractable)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        else
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }

    private IEnumerator Co_FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration, bool makeInteractableWhenVisible)
    {
        if (!cg) yield break;

        cg.gameObject.SetActive(true);
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (duration <= 0f)
        {
            cg.alpha = to;
            if (to > 0.99f && makeInteractableWhenVisible)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            yield break;
        }

        cg.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;

        bool visible = to > 0.99f;
        cg.interactable = visible && makeInteractableWhenVisible;
        cg.blocksRaycasts = visible && makeInteractableWhenVisible;
    }


    public void OnComboDestroyed()
    {
        currentComboPoint = null;
    }

    private IEnumerator ComboCooldownRoutine()
    {
        yield return new WaitForSeconds(comboCooldown);
        comboOnCooldown = false;
    }

    public bool IsGoldModeActive()
    {
        return isGoldModeActive;
    }

    public void ActivateGoldMode()
    {
        if (isGoldModeActive) return;

        StartCoroutine(GoldModeRoutine());
    }

    private IEnumerator GoldModeRoutine()
    {
        isGoldModeActive = true;

        Debug.Log("GOLD MODE START!");

        // 👉 NEU: alles konvertieren + zerstören
        ConvertAllPointsToGoldAndDestroy();

        portalFlash?.SetGoldMode(true);
        portalBeam?.SetGoldMode(true);
        FindAnyObjectByType<SlashTrail>()?.SetGoldMode(true);

        yield return new WaitForSeconds(comboDuration);

        portalFlash?.SetGoldMode(false);
        portalBeam?.SetGoldMode(false);
        FindAnyObjectByType<SlashTrail>()?.SetGoldMode(false);

        isGoldModeActive = false;

        Debug.Log("GOLD MODE ENDE!");
    }
    public void ConvertAllPointsToGoldAndDestroy()
    {
        StartCoroutine(CoConvertAndDestroyAllPoints());
    }

    private IEnumerator CoConvertAndDestroyAllPoints()
    {

        GameObject mainGold = null;
        isConvertingPoints = true;

        currentPoint = null;
        CurrentSwipePoint = null;

        ClickablePoint[] normalPoints = FindObjectsByType<ClickablePoint>(FindObjectsSortMode.None);
        SwipePoint[] swipePoints = FindObjectsByType<SwipePoint>(FindObjectsSortMode.None);

        List<GameObject> spawnedGolds = new List<GameObject>();

        // --- NORMAL POINTS ---
        foreach (var point in normalPoints)
        {
            if (point == null) continue;

            // 👉 ComboOrb sofort zerstören
            if (point.GetComponent<ComboPoint>() != null)
            {
                Destroy(point.gameObject);
                continue;
            }

            Vector3 pos = point.transform.position;

            // 👉 SCORE hinzufügen
            ScoreManager.Instance?.AddPoints(2);

            Destroy(point.gameObject);

            if (normalPointGoldPrefab != null)
            {
                var gold = Instantiate(normalPointGoldPrefab, pos, Quaternion.identity);

                // 👉 NUR EINMAL setzen!
                if (mainGold == null)
                {
                    mainGold = gold;

                    currentPoint = gold;

                    var clickGold = gold.GetComponent<ClickablePoint>();
                    if (clickGold)
                    {
                        clickGold.spawner = this;
                    }

                    var swipeGold = gold.GetComponent<SwipePoint>();
                    if (swipeGold)
                    {
                        swipeGold.spawner = this;
                        CurrentSwipePoint = swipeGold;
                    }
                }

                spawnedGolds.Add(gold);
            }
        }

        // --- SWIPE POINTS ---
        foreach (var point in swipePoints)
        {
            if (point == null) continue;

            Vector3 pos = point.transform.position;
            Destroy(point.gameObject);

            if (swipePointGoldPrefab != null)
            {
                var gold = Instantiate(swipePointGoldPrefab, pos, Quaternion.identity);
                spawnedGolds.Add(gold);
            }
        }

        yield return new WaitForSeconds(0.7f);

        if (mainGold != null)
        {
            PointCleared(mainGold);
            yield break;
        }



        isConvertingPoints = false;
        yield break;
    }

    public void PauseSpawning(bool pause)
    {
        spawnPausedForBanner = pause;

        if (pause)
        {
            StopPointTimer();
        }
    }

    public void SetGoldVisualState(bool active)
    {
        isGoldModeActive = active;
    }
}
