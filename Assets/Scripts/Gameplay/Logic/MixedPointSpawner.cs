using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;
using System.Globalization;

public class MixedPointSpawner : MonoBehaviour
{

    [Header("Combo System")]
    [SerializeField] private GameObject comboPointPrefab;
    [SerializeField] private int comboSpawnScoreThreshold = 5;
    [SerializeField] private float comboSpawnChance = 0.25f;

    private GameObject currentComboPoint;

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

    // ==== Level Panel Auto-Size ====
    [Header("Level Panel Auto-Size")]
    [SerializeField] private bool autoSizeLevelPanel = true;

    [SerializeField] private bool autoCenterContent = true;

    [SerializeField] private Vector2 contentPadding = new Vector2(40f, 36f); // innen um die Inhalte
    [SerializeField] private Vector2 outerPadding = new Vector2(80f, 60f); // Abstand DarkBG -> äußeres Panel
    [SerializeField] private Vector2 minDarkSize = new Vector2(620f, 420f);
    [SerializeField] private Vector2 minOuterSize = new Vector2(800f, 520f);

    [Header("Level Panel Timing")]
    [SerializeField] private float bannerFadeDuration = 0.25f;
    [SerializeField] private float bannerPostDelay = 0.5f;

    // ===== Level-Up Panel =====
    [Header("Level-Up Panel")]
    [SerializeField] private CanvasGroup levelPanel;           // CanvasGroup am Panel
    [SerializeField] private TextMeshProUGUI levelPanelTMP;    // "LEVEL X"

    // --- Getting faster! ---
    [Header("Getting Faster Text Style")]
    [SerializeField] private TextMeshProUGUI gettingFasterTMP;
    [SerializeField] private string gettingFasterText = "Getting faster!";
    [SerializeField] private Color gettingFasterColor = new Color(1f, 0.85f, 0.95f, 1f);

    [Header("Getting Faster Animation")]
    [SerializeField] private bool gfPulseOnPanelIn = true;
    [SerializeField] private float gfPulseDuration = 0.35f;
    [SerializeField] private float gfPulseScale = 1.08f;
    [SerializeField] private float gfGlowBoost = 0.15f; // zusätzlicher Glow während des Pulses
    [SerializeField] private float gfGlowExtra = 0.10f; // additive Intensität
                                                        // Getting Faster Animation
    [SerializeField, Range(0.25f, 5f)] private float gfPulseSpeedMultiplier = 1f; // 2= doppelt so schnell
    [SerializeField, Range(1, 6)] private int gfPulseRepeats = 1;          // 2–3 = doppelt/dreifach pulsieren
    [SerializeField, Range(0f, 0.5f)] private float gfPulseRepeatGap = 0.05f;    // Pause zwischen Pulsen




    // --- Time Row (unter den Dots) ---
    [Header("Time Row (unter den Dots)")]
    [SerializeField] private TextMeshProUGUI timeLabelTMP;     // links: "Max Smash Time:"
    [SerializeField] private TextMeshProUGUI timeValueTMP;     // rechts: "1.5s"
                                                               // Time Row Style
    [SerializeField, Range(0.25f, 5f)] private float timeValuePulseSpeedMultiplier = 1f;
    [SerializeField, Range(1, 6)] private int timeValuePulseRepeats = 1;
    [SerializeField, Range(0f, 0.5f)] private float timeValuePulseRepeatGap = 0.05f;

    // --- Dots / Lade-Animation ---
    [Header("Loading Dots")]
    [SerializeField] private Image[] dotImages;                // 3 Kacheln (links->rechts)
    [SerializeField] private float dotInterval = 0.35f;
    [SerializeField] private float dotOnScale = 1.08f;
    [SerializeField] private float dotOnDuration = 0.15f;
    [SerializeField] private float afterAllDotsDelay = 0.25f;
    [SerializeField] private Color dotOffColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color dotOnColor = Color.white;
    [Space(3)]
    [SerializeField] private AudioClip sfxDot;
    [SerializeField] private AudioClip sfxPanelIn;
    [SerializeField] private AudioClip sfxPanelOut;

    // --- Dot Style ---
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private Vector2 dotSize = new Vector2(110, 110);
    [SerializeField] private bool dotPreserveAspect = true;
    [SerializeField] private Material dotUIMaterial;

    // ==== Typografie: Level Title ====
    [Header("Typography (Inspector) — Level Title")]
    [SerializeField] private TMP_FontAsset levelTitleFont;
    [SerializeField] private FontStyles levelTitleFontStyle = FontStyles.Bold;
    [SerializeField] private TextAlignmentOptions levelTitleAlign = TextAlignmentOptions.Center;
    [SerializeField] private float levelTitleFontSize = 86f;
    [SerializeField] private float levelTitleLetterSpacing = 0f;
    [SerializeField] private bool levelTitleEnableOutline = false;
    [SerializeField] private float levelTitleOutlineWidth = 0f;
    [SerializeField] private Color levelTitleOutlineColor = Color.white;
    [SerializeField] private bool levelTitleEnableGlow = false;
    [SerializeField] private Color levelTitleGlowColor = Color.black;
    [SerializeField] private float levelTitleGlowPower = 0f;
    [SerializeField] private bool levelTitleEnableUnderlay = false;
    [SerializeField] private Color levelTitleUnderlayColor = Color.black;
    [SerializeField] private Vector2 levelTitleUnderlayOffset = Vector2.zero;
    [SerializeField] private float levelTitleUnderlaySoftness = 0f;

    // ==== Typografie: Getting faster! ====
    [Header("Typography (Inspector) — Getting Faster")]
    [SerializeField] private TMP_FontAsset gfFont;
    [SerializeField] private FontStyles gfFontStyle = FontStyles.Bold;
    [SerializeField] private TextAlignmentOptions gfAlign = TextAlignmentOptions.Center;
    [SerializeField] private float gfFontSize = 80f;
    [SerializeField] private float gfLetterSpacing = 0f;
    [SerializeField] private bool gfEnableOutline = false;
    [SerializeField] private float gfOutlineWidth = 0f;
    [SerializeField] private Color gfOutlineColor = Color.white;
    [SerializeField] private bool gfEnableGlow = false;
    [SerializeField] private Color gfGlowColor = Color.black;
    [SerializeField] private float gfGlowPower = 0f;
    [SerializeField] private bool gfEnableUnderlay = false;
    [SerializeField] private Color gfUnderlayColor = Color.black;
    [SerializeField] private Vector2 gfUnderlayOffset = Vector2.zero;
    [SerializeField] private float gfUnderlaySoftness = 0f;

    // ==== Time Row Style (Farbe/Animation & Größen bleiben hier) ====
    [Header("Time Row Style")]
    [SerializeField] private float timeLabelFontSize = 48f; // wird in ApplyTimeLabelTypography verwendet
    [SerializeField] private float timeValueFontSize = 64f; // wird in ApplyTimeValueTypography verwendet
    [SerializeField] private Color timeValueBaseColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color timeValueFlashColor = new Color(1f, 0.85f, 0.30f, 1f);
    [SerializeField] private float timeValueFlashDuration = 0.35f;
    [SerializeField] private float timeValuePunchScale = 1.12f;

    // ==== Typografie: Time Label ("Max Smash Time:") ====
    [Header("Typography (Inspector) — Time Label")]
    [SerializeField] private TMP_FontAsset timeLabelFont;
    [SerializeField] private FontStyles timeLabelFontStyle = FontStyles.Normal;
    [SerializeField] private TextAlignmentOptions timeLabelAlign = TextAlignmentOptions.Left;
    [SerializeField] private float timeLabelLetterSpacing = 0f;
    [SerializeField] private bool timeLabelEnableOutline = false;
    [SerializeField] private float timeLabelOutlineWidth = 0f;
    [SerializeField] private Color timeLabelOutlineColor = Color.white;
    [SerializeField] private bool timeLabelEnableGlow = false;
    [SerializeField] private Color timeLabelGlowColor = Color.black;
    [SerializeField] private float timeLabelGlowPower = 0f;
    [SerializeField] private bool timeLabelEnableUnderlay = false;
    [SerializeField] private Color timeLabelUnderlayColor = Color.black;
    [SerializeField] private Vector2 timeLabelUnderlayOffset = Vector2.zero;
    [SerializeField] private float timeLabelUnderlaySoftness = 0f;

    // ==== Typografie: Time Value (z. B. "1.5s") ====
    [Header("Typography (Inspector) — Time Value")]
    [SerializeField] private TMP_FontAsset timeValueFont;
    [SerializeField] private FontStyles timeValueFontStyle = FontStyles.Bold;
    [SerializeField] private TextAlignmentOptions timeValueAlign = TextAlignmentOptions.Right;
    [SerializeField] private float timeValueLetterSpacing = 0f;
    [SerializeField] private bool timeValueEnableOutline = false;
    [SerializeField] private float timeValueOutlineWidth = 0f;
    [SerializeField] private Color timeValueOutlineColor = Color.white;
    [SerializeField] private bool timeValueEnableGlow = false;
    [SerializeField] private Color timeValueGlowColor = Color.black;
    [SerializeField] private float timeValueGlowPower = 0f;
    [SerializeField] private bool timeValueEnableUnderlay = false;
    [SerializeField] private Color timeValueUnderlayColor = Color.black;
    [SerializeField] private Vector2 timeValueUnderlayOffset = Vector2.zero;
    [SerializeField] private float timeValueUnderlaySoftness = 0f;

    // ==== Layout (Y-Anker innerhalb des Panels) ====
    [Header("Layout (Y-Anker im Panel)")]
    [SerializeField] private float levelTitleY = -60f;
    [SerializeField] private float dotsY = -200f;
    [SerializeField] private float gettingFasterY = -320f; // mehr Abstand über/unter GF
    [SerializeField] private float timeRowY = -420f;

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
    private int currentLevel = 1;
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

        if (levelPanel) MakeTransformNonBlocking(levelPanel.transform);
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

        currentLevel = 1;

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
        if (!running || spawnPausedForBanner || currentPoint != null) return;

        bool forceSwipe = maxNormalsInRow > 0 && normalsInRow >= maxNormalsInRow;
        bool forceNormal = maxSwipesInRow > 0 && swipesInRow >= maxSwipesInRow;

        GameObject prefabToSpawn;
        if (forceSwipe) prefabToSpawn = swipePointPrefab;
        else if (forceNormal) prefabToSpawn = normalPointPrefab;
        else prefabToSpawn = (Random.value < swipeChance) ? swipePointPrefab : normalPointPrefab;

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

        var portal = FindAnyObjectByType<ArcanePortalFlash>();
        if (portal != null)
        {
            portal.FlashParticles();
        }
    }

    private void TrySpawnComboPoint()
    {
        if (currentComboPoint != null) return;

        if (ScoreManager.Instance == null) return;

        int score = ScoreManager.Instance.CurrentScore;

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

        // automatisch wieder freigeben wenn zerstört
        StartCoroutine(TrackComboLifetime(combo));
    }

    private IEnumerator TrackComboLifetime(GameObject combo)
    {
        while (combo != null)
        {
            yield return null;
        }

        currentComboPoint = null;
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
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            float dynamicTime = GetReactionTimeForScore(score);

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
        if (point == currentPoint) { StopPointTimer(); currentPoint = null; }
        if (CurrentSwipePoint != null && point == CurrentSwipePoint.gameObject) CurrentSwipePoint = null;

        Destroy(point);

        if (!running) return;

        if (IsInfinityMode)
        {
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            if (levelUp.TryTriggerLevelUp(score))
            {
                float newRT = levelUp.GetReactionTimeForScore(score, reactionTime);

                MusicManager.Instance?.IncreaseGameMusicSpeed();

                StartCoroutine(LevelRoutine(score, newRT));

                return;
            }
        }

        SpawnNextPoint();
    }

    private IEnumerator LevelRoutine(int score, float newRT)
    {
        spawnPausedForBanner = true;

        yield return levelUp.ShowLevelPanel(levelUp.GetLevelForScore(score), newRT);

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

        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;

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

    // ---------------- Level / Reaction ----------------

    private int GetLevelForScore(int score)
    {
        if (score >= 400) return 12;
        if (score >= 350) return 11;
        if (score >= 300) return 10;
        if (score >= 250) return 9;
        if (score >= 200) return 8;
        if (score >= 150) return 7;
        if (score >= 100) return 6;
        if (score >= 80) return 5;
        if (score >= 60) return 4;
        if (score >= 40) return 3;
        if (score >= 20) return 2;
        return 1;
    }

    private float GetReactionTimeForScore(int score)
    {
        if (score >= 400) return 0.3f;
        if (score >= 350) return 0.4f;
        if (score >= 300) return 0.5f;
        if (score >= 250) return 0.6f;
        if (score >= 200) return 0.7f;
        if (score >= 150) return 0.8f;
        if (score >= 100) return 0.9f;
        if (score >= 80) return 1.0f;
        if (score >= 60) return 1.5f;
        if (score >= 40) return 2.0f;
        if (score >= 20) return 2.5f;
        return reactionTime;
    }

    private IEnumerator Co_ShowLevelPanelAndResume(int levelNumber, float levelTimeSeconds)
    {
        spawnPausedForBanner = true;

        if (currentPoint != null)
        {
            StopPointTimer();
            Destroy(currentPoint);
            currentPoint = null;
            CurrentSwipePoint = null;
        }

        EnsureLevelPanelExistsIfMissing();

        // Title
        if (levelPanelTMP != null)
        {
            levelPanelTMP.text = $"LEVEL {levelNumber}";
            ApplyTitleTypography(levelPanelTMP, levelTitleFontSize);
        }

        // Getting faster!
        if (gettingFasterTMP != null)
        {
            gettingFasterTMP.text = gettingFasterText;
            gettingFasterTMP.color = gettingFasterColor;
            ApplyGFTypography(gettingFasterTMP);
            gettingFasterTMP.rectTransform.localScale = Vector3.one;
        }

        // Time row (Label + Wert!)  <<< das hat gefehlt
        if (timeLabelTMP != null)
        {
            timeLabelTMP.text = "Smash Time:";
            ApplyTimeLabelTypography(timeLabelTMP);
        }
        if (timeValueTMP != null)
        {
            string secs = (levelTimeSeconds < 1f)
                ? levelTimeSeconds.ToString("0.##", CultureInfo.InvariantCulture)
                : levelTimeSeconds.ToString("0.0#", CultureInfo.InvariantCulture);

            timeValueTMP.text = $"{secs}s";
            timeValueTMP.color = timeValueBaseColor;
            timeValueTMP.rectTransform.localScale = Vector3.one;
            ApplyTimeValueTypography(timeValueTMP);
        }

        // Größe nach aktualisierten Texten neu berechnen
        if (autoSizeLevelPanel) AutoSizeLevelPanel();

        // Puls-Animationen EINMAL starten (neue Repeated-Wrapper)
        if (gfPulseOnPanelIn && gettingFasterTMP != null)
            StartCoroutine(Co_PulseGettingFasterRepeated(gettingFasterTMP));
        if (timeValueTMP != null)
            StartCoroutine(Co_FlashTimeValueRepeated(timeValueTMP));


        // Dots zurücksetzen
        if (dotImages != null)
        {
            foreach (var img in dotImages)
            {
                if (!img) continue;
                img.color = dotOffColor;
                img.transform.localScale = Vector3.one;
            }
        }

        // Panel einblenden
        if (sfxPanelIn) SfxManager.Instance?.PlayOneShot(sfxPanelIn);
        yield return StartCoroutine(FadeCanvasGroup(levelPanel, 0f, 1f, bannerFadeDuration));

        // 1 Sekunde warten, dann Pulse + Dots parallel
        float wait = 0f;
        while (wait < 1f)
        {
            if (!PauseMenuController.IsPaused)
                wait += Time.unscaledDeltaTime;

            yield return null;
        }

        Coroutine dotsRoutine = null;
        if (dotImages != null && dotImages.Length > 0)
            dotsRoutine = StartCoroutine(Co_PlayDots());
        if (dotsRoutine != null) yield return dotsRoutine;

        float delay = 0f;
        while (delay < afterAllDotsDelay)
        {
            if (!PauseMenuController.IsPaused)
                delay += Time.unscaledDeltaTime;

            yield return null;
        }

        if (sfxPanelOut) SfxManager.Instance?.PlayOneShot(sfxPanelOut);
        yield return StartCoroutine(FadeCanvasGroup(levelPanel, 1f, 0f, bannerFadeDuration));

        float tPost = 0f;
        while (tPost < bannerPostDelay) { tPost += Time.unscaledDeltaTime; yield return null; }

        spawnPausedForBanner = false;
        SpawnNextPoint();
    }

    private IEnumerator Co_PlayDots()
    {
        for (int i = 0; i < dotImages.Length; i++)
        {
            var img = dotImages[i];
            if (img)
            {
                img.color = dotOnColor;
                float t = 0f;
                Vector3 start = Vector3.one;
                Vector3 target = Vector3.one * dotOnScale;
                while (t < dotOnDuration)
                {
                    if (!PauseMenuController.IsPaused)
                        t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / dotOnDuration);
                    p = 1f - (1f - p) * (1f - p);
                    img.transform.localScale = Vector3.Lerp(start, target, p);
                    yield return null;
                }
                img.transform.localScale = Vector3.one;
                if (sfxDot) SfxManager.Instance?.PlayOneShot(sfxDot);
            }
            float wait = 0f;
            while (wait < dotInterval)
            {
                if (!PauseMenuController.IsPaused)
                    wait += Time.unscaledDeltaTime;

                yield return null;
            }
        }
    }

    private void EnsureLevelPanelExistsIfMissing()
    {
        if (levelPanel != null && levelPanelTMP != null && dotImages != null && dotImages.Length > 0)
        {
            var existingCanvas = levelPanel.GetComponentInParent<Canvas>();
            if (existingCanvas) MakeCanvasNonBlocking(existingCanvas);
            MakeTransformNonBlocking(levelPanel.transform);

            // Typografie erneut anwenden (falls über Inspector geändert)
            if (levelPanelTMP) ApplyTitleTypography(levelPanelTMP, levelTitleFontSize);
            if (gettingFasterTMP)
            {
                ApplyGFTypography(gettingFasterTMP);
                gettingFasterTMP.color = gettingFasterColor;
            }
            if (timeLabelTMP) ApplyTimeLabelTypography(timeLabelTMP);
            if (timeValueTMP)
            {
                ApplyTimeValueTypography(timeValueTMP);
                timeValueTMP.color = timeValueBaseColor;
            }

            // Y-Positionen aktualisieren
            var root = levelPanel.GetComponent<RectTransform>().transform.Find("DarkBG");
            if (root)
            {
                TrySetAnchoredY(root, "LevelText", levelTitleY);
                TrySetAnchoredY(root, "Dots", dotsY);
                TrySetAnchoredY(root, "GettingFaster", gettingFasterY);
                TrySetAnchoredY(root, "TimeRow", timeRowY);
            }
            return;
        }

        // ===== Neues Panel aufbauen (blockt nie) =====
        var canvasGO = new GameObject("LevelPanelCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGO.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = mainCamera;
        canvas.sortingOrder = 5;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        var panelGO = new GameObject("LevelPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(800f, 520f);
        panelRT.anchoredPosition = Vector2.zero;

        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.5f);
        panelImg.raycastTarget = false;

        levelPanel = panelGO.GetComponent<CanvasGroup>();
        levelPanel.alpha = 0f;

        // innerer Kasten
        var darkBG = new GameObject("DarkBG", typeof(RectTransform), typeof(Image));
        darkBG.transform.SetParent(panelGO.transform, false);
        var darkRT = darkBG.GetComponent<RectTransform>();
        darkRT.anchorMin = new Vector2(0.5f, 0.5f);
        darkRT.anchorMax = new Vector2(0.5f, 0.5f);
        darkRT.sizeDelta = new Vector2(620f, 420f);
        darkRT.anchoredPosition = Vector2.zero;

        var darkImg = darkBG.GetComponent<Image>();
        darkImg.color = new Color(0f, 0f, 0f, 0.55f);
        darkImg.raycastTarget = false;

        // Level-Text
        var txtGO = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(darkBG.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = new Vector2(0.5f, 1f);
        txtRT.anchorMax = new Vector2(0.5f, 1f);
        txtRT.anchoredPosition = new Vector2(0f, levelTitleY);
        txtRT.sizeDelta = new Vector2(560f, 110f);

        var levelText = txtGO.GetComponent<TextMeshProUGUI>();
        levelText.alignment = TextAlignmentOptions.Center;
        levelText.textWrappingMode = TextWrappingModes.NoWrap;
        levelText.text = "LEVEL 1";
        levelText.raycastTarget = false;
        levelPanelTMP = levelText;
        ApplyTitleTypography(levelPanelTMP, levelTitleFontSize);

        // Dots
        var dotsGO = new GameObject("Dots", typeof(RectTransform));
        dotsGO.transform.SetParent(darkBG.transform, false);
        var dotsRT = dotsGO.GetComponent<RectTransform>();
        dotsRT.anchorMin = new Vector2(0.5f, 1f);
        dotsRT.anchorMax = new Vector2(0.5f, 1f);
        dotsRT.anchoredPosition = new Vector2(0f, dotsY);
        dotsRT.sizeDelta = new Vector2(560f, 140f);

        dotImages = new Image[3];
        float spacing = 40f;
        float sizeX = dotSize.x;
        float totalW = sizeX * 3 + spacing * 2;
        float startX = -totalW * 0.5f + sizeX * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            var d = new GameObject($"Dot{i + 1}", typeof(RectTransform), typeof(Image));
            d.transform.SetParent(dotsGO.transform, false);

            var rt = d.GetComponent<RectTransform>();
            rt.sizeDelta = dotSize;
            rt.anchoredPosition = new Vector2(startX + i * (sizeX + spacing), 0f);

            var img = d.GetComponent<Image>();
            img.color = dotOffColor;
            img.sprite = dotSprite;
            img.preserveAspect = dotPreserveAspect;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            if (dotUIMaterial != null) img.material = dotUIMaterial;

            dotImages[i] = img;
        }

        // Getting faster!
        var fasterGO = new GameObject("GettingFaster", typeof(RectTransform), typeof(TextMeshProUGUI));
        fasterGO.transform.SetParent(darkBG.transform, false);
        var fasterRT = fasterGO.GetComponent<RectTransform>();
        fasterRT.anchorMin = new Vector2(0.5f, 1f);
        fasterRT.anchorMax = new Vector2(0.5f, 1f);
        fasterRT.anchoredPosition = new Vector2(0f, gettingFasterY);
        fasterRT.sizeDelta = new Vector2(560f, 90f);

        gettingFasterTMP = fasterGO.GetComponent<TextMeshProUGUI>();
        gettingFasterTMP.text = gettingFasterText;
        gettingFasterTMP.raycastTarget = false;
        gettingFasterTMP.color = gettingFasterColor;
        ApplyGFTypography(gettingFasterTMP);

        // Time Row
        var timeRowGO = new GameObject("TimeRow", typeof(RectTransform));
        timeRowGO.transform.SetParent(darkBG.transform, false);
        var timeRowRTLocal = timeRowGO.GetComponent<RectTransform>();
        timeRowRTLocal.anchorMin = new Vector2(0.5f, 1f);
        timeRowRTLocal.anchorMax = new Vector2(0.5f, 1f);
        timeRowRTLocal.anchoredPosition = new Vector2(0f, timeRowY);
        timeRowRTLocal.sizeDelta = new Vector2(560f, 80f);

        var labelGO = new GameObject("TimeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(timeRowGO.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 0.5f);
        labelRT.anchorMax = new Vector2(0f, 0.5f);
        labelRT.pivot = new Vector2(0f, 0.5f);
        labelRT.anchoredPosition = new Vector2(0f, 0f);
        labelRT.sizeDelta = new Vector2(360f, 80f);

        var labelTMP = labelGO.GetComponent<TextMeshProUGUI>();
        labelTMP.text = "Smash Time:";
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelTMP.color = new Color(1f, 1f, 1f, 0.92f);
        labelTMP.raycastTarget = false;
        timeLabelTMP = labelTMP;
        ApplyTimeLabelTypography(timeLabelTMP);

        var valueGO = new GameObject("TimeValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueGO.transform.SetParent(timeRowGO.transform, false);
        var valueRT = valueGO.GetComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(1f, 0.5f);
        valueRT.anchorMax = new Vector2(1f, 0.5f);
        valueRT.pivot = new Vector2(1f, 0.5f);
        valueRT.anchoredPosition = new Vector2(0f, 0f);
        valueRT.sizeDelta = new Vector2(180f, 80f);

        var valueTMP = valueGO.GetComponent<TextMeshProUGUI>();
        valueTMP.text = "0.0s";
        valueTMP.alignment = TextAlignmentOptions.Right;
        valueTMP.color = timeValueBaseColor;
        valueTMP.raycastTarget = false;
        timeValueTMP = valueTMP;
        ApplyTimeValueTypography(timeValueTMP);

        if (autoSizeLevelPanel) AutoSizeLevelPanel();

        MakeCanvasNonBlocking(canvas);
    }

    private void TrySetAnchoredY(Transform root, string child, float y)
    {
        var t = root.Find(child) as RectTransform;
        if (!t) return;
        var p = t.anchoredPosition;
        p.y = y;
        t.anchoredPosition = p;
    }



    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        if (duration <= 0f) { cg.alpha = to; yield break; }

        cg.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            if (!PauseMenuController.IsPaused)
                t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
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

    // ====== Animationen ======

    // Zeitwert-Flash
    private IEnumerator Co_FlashTimeValue(TextMeshProUGUI tmp, Color flashColor, float duration, float punchScale)
    {
        if (tmp == null) yield break;

        var rt = tmp.rectTransform;
        var fromColor = timeValueBaseColor;
        var toColor = flashColor;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float ease = Mathf.Sin(p * Mathf.PI);     // 0..1..0
            tmp.color = Color.Lerp(fromColor, toColor, ease);
            float s = Mathf.Lerp(1f, punchScale, ease);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        tmp.color = timeValueBaseColor;
        rt.localScale = Vector3.one;
    }

    // Getting faster! Pulse + optionaler Glow-Boost
    private IEnumerator Co_FlashTMP_WithOptionalGlow(TextMeshProUGUI tmp, float duration, float scale, float glowBoost, float glowExtra)
    {
        if (!tmp) yield break;

        var rt = tmp.rectTransform;
        var mat = tmp.fontSharedMaterial;
        float baseGlowPower = 0f;
        if (mat != null && mat.HasProperty(ShaderUtilities.ID_GlowPower))
            baseGlowPower = mat.GetFloat(ShaderUtilities.ID_GlowPower);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float ease = Mathf.Sin(p * Mathf.PI); // 0..1..0
            float s = Mathf.Lerp(1f, scale, ease);
            rt.localScale = new Vector3(s, s, 1f);

            if (mat != null && mat.HasProperty(ShaderUtilities.ID_GlowPower))
                mat.SetFloat(ShaderUtilities.ID_GlowPower, baseGlowPower + glowExtra * ease);

            yield return null;
        }

        rt.localScale = Vector3.one;
        if (mat != null && mat.HasProperty(ShaderUtilities.ID_GlowPower))
            mat.SetFloat(ShaderUtilities.ID_GlowPower, baseGlowPower + glowBoost); // kleiner Rest-Boost
    }

    // ====== Typografie-Anwender ======

    private void ApplyTitleTypography(TextMeshProUGUI t, float size)
    {
        if (!t) return;
        if (levelTitleFont) t.font = levelTitleFont;
        t.fontStyle = levelTitleFontStyle;
        t.alignment = levelTitleAlign;
        t.fontSize = size;
        t.characterSpacing = levelTitleLetterSpacing;

        // Outline
        t.outlineWidth = levelTitleEnableOutline ? levelTitleOutlineWidth : 0f;
        t.outlineColor = levelTitleOutlineColor;

        // Material Effekte
        var mat = t.fontSharedMaterial;
        if (mat != null)
        {
            if (levelTitleEnableGlow)
            {
                mat.EnableKeyword("GLOW_ON");
                if (mat.HasProperty(ShaderUtilities.ID_GlowColor)) mat.SetColor(ShaderUtilities.ID_GlowColor, levelTitleGlowColor);
                if (mat.HasProperty(ShaderUtilities.ID_GlowPower)) mat.SetFloat(ShaderUtilities.ID_GlowPower, levelTitleGlowPower);
            }
            else mat.DisableKeyword("GLOW_ON");

            if (levelTitleEnableUnderlay)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor)) mat.SetColor(ShaderUtilities.ID_UnderlayColor, levelTitleUnderlayColor);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness)) mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, levelTitleUnderlaySoftness);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, levelTitleUnderlayOffset.x);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, levelTitleUnderlayOffset.y);
            }
            else mat.DisableKeyword("UNDERLAY_ON");
        }
    }

    private void ApplyGFTypography(TextMeshProUGUI t)
    {
        if (!t) return;

        if (gfFont) t.font = gfFont;
        t.fontStyle = gfFontStyle;
        t.alignment = gfAlign;
        t.fontSize = gfFontSize;
        t.characterSpacing = gfLetterSpacing;

        // Outline
        t.outlineWidth = gfEnableOutline ? gfOutlineWidth : 0f;
        t.outlineColor = gfOutlineColor;

        var mat = t.fontSharedMaterial;
        if (mat != null)
        {
            if (gfEnableGlow)
            {
                mat.EnableKeyword("GLOW_ON");
                if (mat.HasProperty(ShaderUtilities.ID_GlowColor)) mat.SetColor(ShaderUtilities.ID_GlowColor, gfGlowColor);
                if (mat.HasProperty(ShaderUtilities.ID_GlowPower)) mat.SetFloat(ShaderUtilities.ID_GlowPower, gfGlowPower);
            }
            else mat.DisableKeyword("GLOW_ON");

            if (gfEnableUnderlay)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor)) mat.SetColor(ShaderUtilities.ID_UnderlayColor, gfUnderlayColor);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness)) mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, gfUnderlaySoftness);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, gfUnderlayOffset.x);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, gfUnderlayOffset.y);
            }
            else mat.DisableKeyword("UNDERLAY_ON");
        }
    }

    private void ApplyTimeLabelTypography(TextMeshProUGUI t)
    {
        if (!t) return;

        if (timeLabelFont) t.font = timeLabelFont;
        t.fontStyle = timeLabelFontStyle;
        t.alignment = timeLabelAlign;
        t.fontSize = timeLabelFontSize;               // Größe aus „Time Row Style“
        t.characterSpacing = timeLabelLetterSpacing;

        // Outline
        t.outlineWidth = timeLabelEnableOutline ? timeLabelOutlineWidth : 0f;
        t.outlineColor = timeLabelOutlineColor;

        var mat = t.fontSharedMaterial;
        if (mat != null)
        {
            if (timeLabelEnableGlow)
            {
                mat.EnableKeyword("GLOW_ON");
                if (mat.HasProperty(ShaderUtilities.ID_GlowColor)) mat.SetColor(ShaderUtilities.ID_GlowColor, timeLabelGlowColor);
                if (mat.HasProperty(ShaderUtilities.ID_GlowPower)) mat.SetFloat(ShaderUtilities.ID_GlowPower, timeLabelGlowPower);
            }
            else mat.DisableKeyword("GLOW_ON");

            if (timeLabelEnableUnderlay)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor)) mat.SetColor(ShaderUtilities.ID_UnderlayColor, timeLabelUnderlayColor);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness)) mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, timeLabelUnderlaySoftness);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, timeLabelUnderlayOffset.x);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, timeLabelUnderlayOffset.y);
            }
            else mat.DisableKeyword("UNDERLAY_ON");
        }
    }

    private void ApplyTimeValueTypography(TextMeshProUGUI t)
    {
        if (!t) return;

        if (timeValueFont) t.font = timeValueFont;
        t.fontStyle = timeValueFontStyle;
        t.alignment = timeValueAlign;
        t.fontSize = timeValueFontSize;               // Größe aus „Time Row Style“
        t.characterSpacing = timeValueLetterSpacing;

        // Outline
        t.outlineWidth = timeValueEnableOutline ? timeValueOutlineWidth : 0f;
        t.outlineColor = timeValueOutlineColor;

        var mat = t.fontSharedMaterial;
        if (mat != null)
        {
            if (timeValueEnableGlow)
            {
                mat.EnableKeyword("GLOW_ON");
                if (mat.HasProperty(ShaderUtilities.ID_GlowColor)) mat.SetColor(ShaderUtilities.ID_GlowColor, timeValueGlowColor);
                if (mat.HasProperty(ShaderUtilities.ID_GlowPower)) mat.SetFloat(ShaderUtilities.ID_GlowPower, timeValueGlowPower);
            }
            else mat.DisableKeyword("GLOW_ON");

            if (timeValueEnableUnderlay)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayColor)) mat.SetColor(ShaderUtilities.ID_UnderlayColor, timeValueUnderlayColor);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlaySoftness)) mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, timeValueUnderlaySoftness);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetX)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, timeValueUnderlayOffset.x);
                if (mat.HasProperty(ShaderUtilities.ID_UnderlayOffsetY)) mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, timeValueUnderlayOffset.y);
            }
            else mat.DisableKeyword("UNDERLAY_ON");
        }
    }

    private void AutoSizeLevelPanel()
    {
        if (!levelPanel) return;

        var panelRT = levelPanel.GetComponent<RectTransform>();
        var darkRT = panelRT.transform.Find("DarkBG") as RectTransform;
        if (!darkRT) return;

        // --- 1) Bounds aller Kinder relativ zu DarkBG berechnen ---
        bool has = false;
        Bounds contentBounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (RectTransform child in darkRT)
        {
            // ignorier leere/disabled Objekte nicht, sonst stimmt's bei Fade-In nicht
            var cb = RectTransformUtility.CalculateRelativeRectTransformBounds(darkRT, child);
            if (!has) { contentBounds = cb; has = true; } else contentBounds.Encapsulate(cb);
        }
        if (!has) return;

        // --- 2) DarkBG auf Content + Padding setzen ---
        var neededDark = new Vector2(
            contentBounds.size.x + contentPadding.x * 2f,
            contentBounds.size.y + contentPadding.y * 2f
        );
        neededDark = new Vector2(Mathf.Max(minDarkSize.x, neededDark.x),
                                 Mathf.Max(minDarkSize.y, neededDark.y));
        darkRT.sizeDelta = neededDark;

        // --- 3) Äußeres Panel mit zusätzlichem Rand setzen ---
        var neededOuter = new Vector2(
            neededDark.x + outerPadding.x,
            neededDark.y + outerPadding.y
        );
        neededOuter = new Vector2(Mathf.Max(minOuterSize.x, neededOuter.x),
                                  Mathf.Max(minOuterSize.y, neededOuter.y));
        panelRT.sizeDelta = neededOuter;

        // --- 4) Inhalte exakt mittig in DarkBG positionieren ---
        // contentBounds.center ist der aktuelle Schwerpunkt der Kinder relativ zu DarkBG.
        if (autoCenterContent)
        {
            Vector2 shift = new Vector2(-contentBounds.center.x, -contentBounds.center.y);
            foreach (RectTransform child in darkRT)
            {
                // anchoredPosition verschieben (kein Drift: nach dem ersten Mal ist center ~ 0)
                child.anchoredPosition += shift;
            }
        }

    }

    private IEnumerator Co_PulseGettingFasterRepeated(TextMeshProUGUI tmp)
    {
        if (!tmp) yield break;
        float dur = Mathf.Max(0.01f, gfPulseDuration / Mathf.Max(0.01f, gfPulseSpeedMultiplier));
        for (int i = 0; i < gfPulseRepeats; i++)
        {
            yield return StartCoroutine(Co_FlashTMP_WithOptionalGlow(tmp, dur, gfPulseScale, gfGlowBoost, gfGlowExtra));
            if (i < gfPulseRepeats - 1 && gfPulseRepeatGap > 0f)
                yield return new WaitForSecondsRealtime(gfPulseRepeatGap);
        }
    }

    private IEnumerator Co_FlashTimeValueRepeated(TextMeshProUGUI tmp)
    {
        if (!tmp) yield break;
        float dur = Mathf.Max(0.01f, timeValueFlashDuration / Mathf.Max(0.01f, timeValuePulseSpeedMultiplier));
        for (int i = 0; i < timeValuePulseRepeats; i++)
        {
            yield return StartCoroutine(Co_FlashTimeValue(tmp, timeValueFlashColor, dur, timeValuePunchScale));
            if (i < timeValuePulseRepeats - 1 && timeValuePulseRepeatGap > 0f)
                yield return new WaitForSecondsRealtime(timeValuePulseRepeatGap);
        }
    }

    private IEnumerator WaitWhileGamePaused()
    {
        while (PauseMenuController.IsPaused)
            yield return null;
    }

}
