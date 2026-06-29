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

    [Header("Farbige Tap-Prefabs (Rot / Grün / Lila)")]
    [SerializeField] private GameObject tapPrefab_Red;
    [SerializeField] private GameObject tapPrefab_Green;
    [SerializeField] private GameObject tapPrefab_Purple;

    [Header("Farbige Swipe-Prefabs (Rot / Grün / Lila)")]
    [SerializeField] private GameObject swipePrefab_Red;
    [SerializeField] private GameObject swipePrefab_Green;
    [SerializeField] private GameObject swipePrefab_Purple;

    [Header("Floating Score")]
    [SerializeField] private GameObject floatingScorePrefab;
    [SerializeField] private Color floatingColorDefault = Color.white;
    [SerializeField] private Color floatingColorRed     = new Color(1f,  0.35f, 0.35f);
    [SerializeField] private Color floatingColorGreen   = new Color(0.35f, 1f,  0.45f);
    [SerializeField] private Color floatingColorPurple  = new Color(0.75f, 0.3f, 1f);

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
    public bool IsRunning => running;
    public bool IsTutorialMode { get; private set; }

    public void SetTutorialMode(bool active) => IsTutorialMode = active;

    // ── 3-Slot-System ─────────────────────────────────────────────────────────
    private class SlotState
    {
        public GameObject point;
        public PointColor color;
        public Coroutine  timeout;
        public bool       isThunder;
    }

    private readonly SlotState[] _slots =
    {
        new SlotState(), new SlotState(), new SlotState()
    };

    // Reservierte Positionen während Beam-Flug (Slot hat noch kein point, aber Pos ist vergeben).
    private readonly Vector3?[] _pendingSlotPositions = { null, null, null };

    // Für Rückwärtskompatibilität (Tutorial, legacy-Checks)
    private GameObject currentPoint
    {
        get { foreach (var s in _slots) if (s.point != null) return s.point; return null; }
    }

    // Alle aktiven SwipePoints — für PlayerInputHandler
    public SwipePoint[] GetActiveSwipePoints()
    {
        var list = new System.Collections.Generic.List<SwipePoint>();
        foreach (var s in _slots)
            if (s.point != null) { var sp = s.point.GetComponent<SwipePoint>(); if (sp) list.Add(sp); }
        return list.ToArray();
    }

    private int FindSlotIndex(GameObject point)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].point == point) return i;
        return -1;
    }

    private void ClearAllSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].timeout != null) { StopCoroutine(_slots[i].timeout); _slots[i].timeout = null; }
            if (_slots[i].point  != null) { Destroy(_slots[i].point); _slots[i].point = null; }
        }
        CurrentSwipePoint = null;
        DismissExtraThunder();
    }

    // Räumt alle Slots außer exceptIndex lautlos auf (kein Score, kein Leben-Verlust).
    private void ClearOtherSlots(int exceptIndex)
    {
        for (int j = 0; j < _slots.Length; j++)
        {
            if (j == exceptIndex) continue;
            if (_slots[j].timeout != null) { StopCoroutine(_slots[j].timeout); _slots[j].timeout = null; }
            if (_slots[j].point   != null)
            {
                if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == _slots[j].point)
                    CurrentSwipePoint = null;
                Destroy(_slots[j].point);
                _slots[j].point = null;
            }
        }
    }

    // Hilfsmethoden für farbige Prefabs (Fallback auf Default)
    private GameObject GetTapPrefab(PointColor c) => c switch
    {
        PointColor.Red    => tapPrefab_Red    != null ? tapPrefab_Red    : ActiveNormalPrefab,
        PointColor.Green  => tapPrefab_Green  != null ? tapPrefab_Green  : ActiveNormalPrefab,
        PointColor.Purple => tapPrefab_Purple != null ? tapPrefab_Purple : ActiveNormalPrefab,
        _                 => ActiveNormalPrefab
    };

    private GameObject GetSwipePrefab(PointColor c) => c switch
    {
        PointColor.Red    => swipePrefab_Red    != null ? swipePrefab_Red    : ActiveSwipePrefab,
        PointColor.Green  => swipePrefab_Green  != null ? swipePrefab_Green  : ActiveSwipePrefab,
        PointColor.Purple => swipePrefab_Purple != null ? swipePrefab_Purple : ActiveSwipePrefab,
        _                 => ActiveSwipePrefab
    };

    // Zwischengespeicherter SwipePoint während er gesperrt ist
    private SwipePoint _lockedSwipePoint;

    private GameObject lastPoint;

    private int normalsInRow = 0;
    private int swipesInRow = 0;

    private bool running = false;
    private bool gameOver = false;
    private bool spawnPausedForBanner = false;

    // Rundentyp: alle 3 Slots spawnen immer denselben Typ (Tap oder Swipe).
    // Wird neu gewürfelt, sobald alle 3 Slots gleichzeitig leer sind.
    private bool _roundIsSwipe         = false;
    private bool _roundIsAllThunder    = false; // alle 3 Slots sind Shocker
    private bool _roundHasExtraThunder = false; // 3 normale + 1 extra Shocker

    // Extra-Shocker (bei _roundHasExtraThunder): außerhalb des Slot-Systems
    private GameObject _extraThunder;
    private Coroutine  _extraThunderRoutine;


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

        SpawnNextPoint();
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
        if (!paused && running)
            SpawnNextPoint();
    }

    // Füllt alle leeren Slots mit neuen Elementen.
    public void SpawnNextPoint()
    {
        if (IsTutorialMode) return;
        if (!running || spawnPausedForBanner || isConvertingPoints) return;
        if (IsInfinityMode && allowRandomActivationOrbs && TrySpawnActivationOrb()) return;

        // Alle Slots leer → neuen Rundentyp würfeln.
        bool allEmpty = true;
        foreach (var s in _slots) { if (s.point != null) { allEmpty = false; break; } }
        if (allEmpty)
        {
            _roundIsAllThunder    = false;
            _roundHasExtraThunder = false;

            if (ActiveThunderPrefab != null && thunderSpawnChance > 0f && Random.value < thunderSpawnChance)
            {
                // 50/50: alle drei Shocker ODER normale Elemente + ein extra Shocker
                if (Random.value < 0.5f) _roundIsAllThunder    = true;
                else                     _roundHasExtraThunder = true;
            }

            if (!_roundIsAllThunder)
            {
                bool forceSwipe  = maxNormalsInRow > 0 && normalsInRow >= maxNormalsInRow;
                bool forceNormal = maxSwipesInRow  > 0 && swipesInRow  >= maxSwipesInRow;
                _roundIsSwipe = forceSwipe ? true : (forceNormal ? false : Random.value < swipeChance);
                if (_roundIsSwipe) { swipesInRow++; normalsInRow = 0; }
                else               { normalsInRow++; swipesInRow = 0; }
            }
        }

        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].point == null)
                SpawnSlot(i);

        // Extra-Shocker erst spawnen, wenn alle 3 Slots belegt sind
        if (_roundHasExtraThunder && _extraThunder == null)
        {
            bool allFilled = true;
            foreach (var s in _slots) { if (s.point == null) { allFilled = false; break; } }
            if (allFilled) SpawnExtraThunder();
        }
    }

    // Gibt die Farbe zurück, die in keinem anderen belegten Slot vorhanden ist.
    private PointColor GetUnusedColor(int slotIndex)
    {
        bool redUsed = false, greenUsed = false, purpleUsed = false;
        for (int j = 0; j < _slots.Length; j++)
        {
            // Slot zählt als belegt wenn er ein point hat ODER sein Beam noch im Flug ist
            if (j == slotIndex || (_slots[j].point == null && !_pendingSlotPositions[j].HasValue)) continue;
            switch (_slots[j].color)
            {
                case PointColor.Red:    redUsed    = true; break;
                case PointColor.Green:  greenUsed  = true; break;
                case PointColor.Purple: purpleUsed = true; break;
            }
        }
        var available = new System.Collections.Generic.List<PointColor>(3);
        if (!redUsed)    available.Add(PointColor.Red);
        if (!greenUsed)  available.Add(PointColor.Green);
        if (!purpleUsed) available.Add(PointColor.Purple);
        return available.Count > 0
            ? available[Random.Range(0, available.Count)]
            : (PointColor)Random.Range(0, 3);
    }

    // Spawnt ein neues Element in Slot i (zufällige Farbe, Tap oder Swipe).
    private void SpawnSlot(int i)
    {
        if (!running || spawnPausedForBanner || _slots[i].point != null || isConvertingPoints) return;

        // Farbe: die noch nicht in einem anderen Slot belegten Farbe wählen
        PointColor color = GetUnusedColor(i);
        _slots[i].color = color;

        // Peek-a-boo nur für Slot 0, und nur wenn kein anderer Slot aktiv ist
        if (i == 0 && peekABooSystem != null && !PeekABooSystem.IsActive
            && _slots[1].point == null && _slots[2].point == null
            && Random.value < peekABooChance)
        {
            peekABooSystem.StartPeekABoo();
            return;
        }

        bool spawnSwipe = _roundIsSwipe;
        bool isThunder  = _roundIsAllThunder;

        GameObject samplePrefab = isThunder ? ActiveThunderPrefab
                                 : (spawnSwipe ? GetSwipePrefab(color) : GetTapPrefab(color));
        Vector2 viewportPos = FindSlotPosition(i, samplePrefab);
        Vector3 worldPos    = ViewportToWorldOnZ0(viewportPos);

        // Position sofort reservieren, damit Geschwister-Slots sie in FindSlotPosition vermeiden
        _pendingSlotPositions[i] = worldPos;

        if (portalBeam != null && !IsTutorialMode)
        {
            // Variablen für Closure festhalten
            int        ci    = i;
            PointColor cc    = color;
            bool       cs    = spawnSwipe;
            bool       ct    = isThunder;
            Vector3    cpos  = worldPos;
            GameObject cpref = samplePrefab;
            portalBeam.FireBeamTo(worldPos, color, () =>
            {
                _pendingSlotPositions[ci] = null;
                if (!running || gameOver || _slots[ci].point != null) return;
                FinishSlotSpawn(ci, cpref, cpos, cc, cs, ct);
            });
        }
        else
        {
            _pendingSlotPositions[i] = null;
            FinishSlotSpawn(i, samplePrefab, worldPos, color, spawnSwipe, isThunder);
        }
    }

    // Instanziiert das Element und startet Timer/Fuse — nach Beam-Ankunft oder direkt.
    private void FinishSlotSpawn(int i, GameObject prefab, Vector3 worldPos,
                                  PointColor color, bool spawnSwipe, bool isThunder)
    {
        if (isThunder)
        {
            _slots[i].isThunder = true;
            var thunder = Instantiate(ActiveThunderPrefab, worldPos, Quaternion.identity);
            var tp = thunder.GetComponent<ThunderPoint>();
            float dynamicTime = CurrentReactionTime;
            if (tp != null) tp.Activate(dynamicTime);

            _slots[i].point   = thunder;
            _slots[i].timeout = StartCoroutine(Co_SlotTimeout(i, thunder, dynamicTime));
            lastPoint = thunder;
            if (portalFlash != null) portalFlash.FlashParticles();
            return;
        }

        _slots[i].isThunder = false;

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var tap   = newPoint.GetComponent<TapPoint>();
        var swipe = newPoint.GetComponent<SwipePoint>();
        if (tap   != null) tap.spawner   = this;
        if (swipe != null) { swipe.spawner = this; CurrentSwipePoint = swipe; }

        var bp = newPoint.GetComponent<BasePoint>();
        if (bp != null) bp.Color = color;

        _slots[i].point = newPoint;
        lastPoint = newPoint;

        if (IsInfinityMode && !IsTutorialMode)
        {
            float dynamicTime = CurrentReactionTime;
            _slots[i].timeout = StartCoroutine(Co_SlotTimeout(i, newPoint, dynamicTime));

            var fuse        = newPoint.GetComponentInChildren<FuseCountdown>();
            var lineFuse    = newPoint.GetComponentInChildren<LineFuse>();
            var sparks      = newPoint.GetComponentInChildren<BurnSparks>();
            var countdownSq = newPoint.GetComponentInChildren<CountdownSquare>();
            var pulse       = newPoint.GetComponent<PointPulse>();

            if (fuse        != null) fuse.StartBurn(dynamicTime);
            if (lineFuse    != null) lineFuse.StartBurn(dynamicTime);
            if (sparks      != null) { sparks.SetQuadMode(tap != null); sparks.StartBurn(dynamicTime); }
            if (countdownSq != null) countdownSq.StartCountdown(dynamicTime);
            if (pulse       != null) pulse.StartPulsing();

            if (tap != null) TrySpawnFake(newPoint, dynamicTime);
        }

        if (portalFlash != null) portalFlash.FlashParticles();
    }

    // Findet eine Position für Slot i: vermeidet alle anderen belegten Slots + Activation Orb.
    private Vector2 FindSlotPosition(int slotIndex, GameObject prefab)
    {
        float halfSizePx      = GetHalfSizePixels(prefab);
        Rect  allowedViewport = ScreenRectToViewportRect(GetAllowedSpawnRect());
        int   maxAttempts     = 120;

        // Sibling-Positionen und -Größen einmal cachen (nicht pro Attempt neu berechnen)
        var siblingVP   = new Vector2[_slots.Length];
        var siblingHalf = new float[_slots.Length];
        var siblingActive = new bool[_slots.Length];
        for (int j = 0; j < _slots.Length; j++)
        {
            if (j == slotIndex) continue;
            if (_slots[j].point != null)
            {
                siblingVP[j]     = mainCamera.WorldToViewportPoint(_slots[j].point.transform.position);
                siblingHalf[j]   = GetHalfSizePixels(_slots[j].point);
                siblingActive[j] = true;
            }
            else if (_pendingSlotPositions[j].HasValue)
            {
                siblingVP[j]     = mainCamera.WorldToViewportPoint(_pendingSlotPositions[j].Value);
                siblingHalf[j]   = halfSizePx;
                siblingActive[j] = true;
            }
        }

        float orbHalfPx = currentActivationPoint != null ? GetHalfSizePixels(currentActivationPoint) : 0f;
        float exHalfPx  = _extraThunder            != null ? GetHalfSizePixels(_extraThunder)            : 0f;

        // Fallback: Kandidat mit dem größten Mindestabstand zu allen Hindernissen
        Vector2 bestFallback     = new Vector2(0.5f, 0.5f);
        float   bestFallbackDist = -1f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 vp = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            float minDistToAny = float.MaxValue;
            bool  valid        = true;

            // Abstand zu allen anderen belegten Slots (auch pending / Beam im Flug)
            for (int j = 0; j < _slots.Length; j++)
            {
                if (j == slotIndex || !siblingActive[j]) continue;
                float dist = PixelDistance(vp, siblingVP[j]);
                minDistToAny = Mathf.Min(minDistToAny, dist);
                if (!IsFarEnoughFromOrb(vp, siblingVP[j], halfSizePx, siblingHalf[j])) { valid = false; break; }
            }

            if (!valid)
            {
                if (minDistToAny > bestFallbackDist) { bestFallbackDist = minDistToAny; bestFallback = vp; }
                continue;
            }

            // Abstand zum Activation Orb
            if (currentActivationPoint != null)
            {
                Vector2 orbVP = mainCamera.WorldToViewportPoint(currentActivationPoint.transform.position);
                float   dist  = PixelDistance(vp, orbVP);
                minDistToAny = Mathf.Min(minDistToAny, dist);
                if (!IsFarEnoughFromOrb(vp, orbVP, halfSizePx, orbHalfPx))
                {
                    if (minDistToAny > bestFallbackDist) { bestFallbackDist = minDistToAny; bestFallback = vp; }
                    continue;
                }
            }

            // Abstand zum Extra-Shocker
            if (_extraThunder != null)
            {
                Vector2 exVP = mainCamera.WorldToViewportPoint(_extraThunder.transform.position);
                float   dist = PixelDistance(vp, exVP);
                minDistToAny = Mathf.Min(minDistToAny, dist);
                if (!IsFarEnoughFromOrb(vp, exVP, halfSizePx, exHalfPx))
                {
                    if (minDistToAny > bestFallbackDist) { bestFallbackDist = minDistToAny; bestFallback = vp; }
                    continue;
                }
            }

            return vp; // perfekte Position gefunden
        }

        return bestFallback; // beste verfügbare Position nach allen Versuchen
    }

    // Slot-Timeout: überwacht ein einzelnes Element und behandelt Ablauf / Leben-verlust.
    private IEnumerator Co_SlotTimeout(int i, GameObject point, float seconds)
    {
        if (_slots[i].isThunder)
        {
            // Thunder verwaltet seinen eigenen Invoke-Timer; wir warten bis er verschwindet.
            while (point != null && running && !gameOver)
                yield return null;

            _slots[i].point   = null;
            _slots[i].timeout = null;
            if (CurrentSwipePoint != null && point != null && CurrentSwipePoint.gameObject == point)
                CurrentSwipePoint = null;

            if (!running || gameOver) yield break;

            // Andere Slots lautlos leeren + Extra-Shocker wegräumen, dann frische Dreier-Reihe
            ClearOtherSlots(i);
            DismissExtraThunder();

            // War es ein Tap (Leben verloren)? Dann auf Animation warten.
            if (LivesManager.IsLifeLostAnimating)
            {
                yield return new WaitUntil(() => !LivesManager.IsLifeLostAnimating);
                yield return null; // FIFO-Sicherheitsframe für RunPhaseTimer
            }
            if (running && !gameOver) SpawnNextPoint();
            yield break;
        }

        // Normales Element — herunterzählen
        float t = 0f;
        while (t < seconds && running && !gameOver)
        {
            if (_slots[i].point != point) yield break; // bereits durch Hit gelöscht
            t += Time.deltaTime;
            yield return null;
        }

        if (!running || gameOver || _slots[i].point != point) yield break;

        // Timeout: Leben verlieren
        DismissCurrentFake();
        ComboManager.Instance?.RegisterMiss();

        Vector3 pos = point.transform.position;
        _slots[i].timeout = null;
        _slots[i].point   = null;
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;
        Destroy(point);

        // Andere Slots + Extra-Shocker lautlos leeren, dann frische Dreier-Reihe
        ClearOtherSlots(i);
        DismissExtraThunder();

        bool stillAlive = LivesManager.Instance.LoseLife(pos);
        if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);

        if (stillAlive)
        {
            yield return new WaitForSecondsRealtime(LivesManager.Instance.TotalGameOverAnimDuration);
            yield return null; // FIFO-Sicherheitsframe
            if (running && !gameOver) SpawnNextPoint();
        }
        else
        {
            float delay = LivesManager.Instance?.TotalGameOverAnimDuration ?? 0f;
            yield return new WaitForSecondsRealtime(delay);
            GameOver();
        }
    }

    // ─── Extra-Thunder ─────────────────────────────────────────────────────────

    // Spawnt den Extra-Shocker an einer Position, die alle 3 Slots + Activation Orb vermeidet.
    private void SpawnExtraThunder()
    {
        if (ActiveThunderPrefab == null) return;

        float   halfPx        = GetHalfSizePixels(ActiveThunderPrefab);
        Rect    allowedVP     = ScreenRectToViewportRect(GetAllowedSpawnRect());
        int     maxAttempts   = 80;
        Vector2 best          = new Vector2(0.5f, 0.5f);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 vp = new Vector2(
                Random.Range(allowedVP.xMin, allowedVP.xMax),
                Random.Range(allowedVP.yMin, allowedVP.yMax)
            );

            bool valid = true;

            // Abstand zu allen belegten Slots
            foreach (var s in _slots)
            {
                if (s.point == null) continue;
                Vector2 sVP   = mainCamera.WorldToViewportPoint(s.point.transform.position);
                float   sHalf = GetHalfSizePixels(s.point);
                if (!IsFarEnoughFromOrb(vp, sVP, halfPx, sHalf)) { valid = false; break; }
            }

            if (!valid) continue;

            if (currentActivationPoint != null)
            {
                Vector2 orbVP   = mainCamera.WorldToViewportPoint(currentActivationPoint.transform.position);
                float   orbHalf = GetHalfSizePixels(currentActivationPoint);
                if (!IsFarEnoughFromOrb(vp, orbVP, halfPx, orbHalf)) continue;
            }

            best = vp;
            break;
        }

        float dynamicTime = CurrentReactionTime;
        Vector3 worldPos  = ViewportToWorldOnZ0(best);
        _extraThunder     = Instantiate(ActiveThunderPrefab, worldPos, Quaternion.identity);

        var tp = _extraThunder.GetComponent<ThunderPoint>();
        if (tp != null) tp.Activate(dynamicTime);

        _extraThunderRoutine = StartCoroutine(Co_ExtraThunderMonitor(dynamicTime));
    }

    // Räumt den Extra-Shocker lautlos auf (kein Leben-Verlust).
    private void DismissExtraThunder()
    {
        if (_extraThunderRoutine != null) { StopCoroutine(_extraThunderRoutine); _extraThunderRoutine = null; }
        if (_extraThunder == null) return;
        var tp = _extraThunder.GetComponent<ThunderPoint>();
        if (tp != null) tp.Vanish(); else Destroy(_extraThunder);
        _extraThunder = null;
    }

    // Überwacht den Extra-Shocker. Wird er angetippt (Leben verloren), leert er alle Slots
    // und startet die nächste Runde. Verpufft er natürlich, läuft die Runde normal weiter.
    private IEnumerator Co_ExtraThunderMonitor(float seconds)
    {
        float t = 0f;
        while (_extraThunder != null && t < seconds + 1f && running && !gameOver)
        {
            t += Time.deltaTime;
            yield return null;
        }

        _extraThunder        = null;
        _extraThunderRoutine = null;

        if (!running || gameOver) yield break;

        // Angetippt → Leben verloren → farbige Elemente leeren + Runde neu starten
        if (LivesManager.IsLifeLostAnimating)
        {
            ClearAllSlots();
            yield return new WaitUntil(() => !LivesManager.IsLifeLostAnimating);
            yield return null; // FIFO-Sicherheitsframe
            if (running && !gameOver) SpawnNextPoint();
        }
        // Natürlich verschwunden → farbige Elemente laufen weiter, nichts tun
    }

    // ─── Abstand-Helpers ───────────────────────────────────────────────────────

    private float PixelDistance(Vector2 vpA, Vector2 vpB)
    {
        Vector2 a = vpA * new Vector2(Screen.width, Screen.height);
        Vector2 b = vpB * new Vector2(Screen.width, Screen.height);
        return Vector2.Distance(a, b);
    }

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
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].point == null) continue;
            Vector2 slotVP   = mainCamera.WorldToViewportPoint(_slots[i].point.transform.position);
            float   slotHalf = GetHalfSizePixels(_slots[i].point);
            if (!IsFarEnoughFromOrb(candidateVP, slotVP, orbHalfSizePx, slotHalf)) return false;
        }
        return true;
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

    // Wird nur noch für Tutorial / PortalBeam-Callback genutzt (→ Slot 0).
    public void CreatePoint(GameObject prefab, Vector3 worldPos)
    {
        // Slot 0 freigeben
        if (_slots[0].timeout != null) { StopCoroutine(_slots[0].timeout); _slots[0].timeout = null; }
        if (_slots[0].point  != null) { Destroy(_slots[0].point);         _slots[0].point  = null; }
        DismissCurrentFake();

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var tap = newPoint.GetComponent<TapPoint>();
        if (tap) tap.spawner = this;

        var swipe = newPoint.GetComponent<SwipePoint>();
        if (swipe) { swipe.spawner = this; CurrentSwipePoint = swipe; }
        else       { CurrentSwipePoint = null; }

        _slots[0].point = newPoint;
        _slots[0].color = PointColor.Red; // Tutorial hat keine Farbe → Platzhalter
        lastPoint = newPoint;

        if (IsInfinityMode && !IsTutorialMode)
        {
            float dynamicTime = CurrentReactionTime;
            _slots[0].timeout = StartCoroutine(Co_SlotTimeout(0, newPoint, dynamicTime));
            if (debugLogs) Debug.Log($"[Spawner] Timer gestartet: {dynamicTime:F2}s");

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

            if (tap != null) TrySpawnFake(newPoint, dynamicTime);
        }
        else if (!IsTutorialMode)
        {
            if (debugLogs) Debug.Log("[Spawner] Kein Timer gestartet.");
        }

        if (portalFlash != null) portalFlash.FlashParticles();
    }

    // Fallback-Adapter für externe Aufrufer (kein Score/Combo, nur Slot freigeben + respawn).
    public void PointCleared(GameObject point)
    {
        if (isConvertingPoints) return;

        int idx = FindSlotIndex(point);
        if (idx >= 0)
        {
            if (_slots[idx].timeout != null) { StopCoroutine(_slots[idx].timeout); _slots[idx].timeout = null; }
            _slots[idx].point = null;
        }
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;
        Destroy(point);

        if (idx >= 0) ClearOtherSlots(idx);

        if (running && !gameOver && !spawnPausedForBanner)
            SpawnNextPoint();
    }

    public bool ForceClearCurrentPoint()
    {
        var pt = currentPoint;
        if (pt == null) return false;
        HandlePointHit(pt);
        return true;
    }

    // Gibt den aktuellen Point zurück und entfernt ihn aus dem Spawner-Tracking.
    // Collider wird deaktiviert damit er nicht mehr tappbar ist.
    // Der Aufrufer ist verantwortlich für die Destroy.
    public GameObject StealCurrentPoint()
    {
        int idx = -1;
        for (int i = 0; i < _slots.Length; i++) { if (_slots[i].point != null) { idx = i; break; } }
        if (idx < 0) return null;

        if (_slots[idx].timeout != null) { StopCoroutine(_slots[idx].timeout); _slots[idx].timeout = null; }

        var col = _slots[idx].point.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var stolen = _slots[idx].point;
        _slots[idx].point = null;
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == stolen) CurrentSwipePoint = null;
        return stolen;
    }

    // ─── Countdown ────────────────────────────────────────────────────────────

    private void StopPointTimer()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].timeout != null) { StopCoroutine(_slots[i].timeout); _slots[i].timeout = null; }
    }


    // ─── Game Over ────────────────────────────────────────────────────────────

    private async void EndGame(int score)
    {
        if (gameOver) return;
        gameOver = true;

        running = false;
        spawnPausedForBanner = false;
        ClearAllSlots();

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
        ClearAllSlots();
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
        DismissCurrentFake();

        int idx = FindSlotIndex(point);
        PointColor color = idx >= 0 ? _slots[idx].color : PointColor.Red;

        int scored = ScoreManager.Instance?.AddPointsFromHit() ?? 0;
        ComboManager.Instance?.RegisterHit(color);

        // Sound NACH RegisterHit → Streak bereits aktualisiert → richtiger Pitch
        int streak = ComboManager.Instance?.ComboCount ?? 0;

        SpawnFloatingScore(scored, point.transform.position, color, streak);
        bool isSwipe = point.GetComponent<SwipePoint>() != null;
        if (isSwipe) AudioManager.Instance?.PlaySwipePoint(streak);
        else         AudioManager.Instance?.PlayNormalPoint(streak);

        var bp = point.GetComponent<BasePoint>();
        if (bp != null) bp.SendMessage("SpawnExplosion");

        if (idx >= 0)
        {
            if (_slots[idx].timeout != null) { StopCoroutine(_slots[idx].timeout); _slots[idx].timeout = null; }
            _slots[idx].point = null;
        }
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;

        Destroy(point);

        // Ein Treffer → alle anderen Slots lautlos leeren + Extra-Shocker wegräumen, dann frische Reihe
        if (idx >= 0) ClearOtherSlots(idx);
        DismissExtraThunder();

        if (running && !gameOver && !spawnPausedForBanner)
            SpawnNextPoint();
    }

    private void SpawnFloatingScore(int score, Vector3 worldPos, PointColor color, int streak)
    {
        if (floatingScorePrefab == null || score <= 0) return;

        // Bei aktivem Kombo (streak >= 5) Streak-Farbe, sonst weiß
        Color c = streak >= 5 ? color switch
        {
            PointColor.Red    => floatingColorRed,
            PointColor.Green  => floatingColorGreen,
            PointColor.Purple => floatingColorPurple,
            _                 => floatingColorDefault
        } : floatingColorDefault;

        var go  = Instantiate(floatingScorePrefab, worldPos, Quaternion.identity);
        var fst = go.GetComponent<FloatingScoreText>();
        fst?.Play(score, c);
    }

    // Vom PeekABooSystem: registriert das Swipe-Peek-Element für den Swipe-Input.
    public void SetPeekSwipePoint(SwipePoint sp)
    {
        CurrentSwipePoint = sp;
    }

    public void ResetCurrentPointTimer()
    {
        StopPointTimer();
        if (!IsInfinityMode) return;
        float dynamicTime = CurrentReactionTime;
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].point != null)
                _slots[i].timeout = StartCoroutine(Co_SlotTimeout(i, _slots[i].point, dynamicTime));
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

        // Slots wurden bereits in HandlePointHit bereinigt
        CurrentSwipePoint = null;
    }

    /// <summary>True, solange noch irgendein spielbares Element in der Szene ist (currentPoint oder
    /// frei fliegende Tap/Swipe/Gravity/Fountain/Fake/Thunder/Peek-Elemente). Genutzt vom PhaseManager,
    /// um am Phasenende die Restelemente natürlich auslaufen zu lassen, bevor es weitergeht.</summary>
    public bool HasActiveGameplayPoints()
    {
        foreach (var s in _slots) if (s.point != null) return true;
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
        ClearAllSlots();

        foreach (var s in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
            Destroy(s.gameObject);

        foreach (var t in FindObjectsByType<TapPoint>(FindObjectsSortMode.None))
            Destroy(t.gameObject);

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
        if (currentPoint != null) { Destroy(currentPoint); _slots[0].point = null; _slots[0].timeout = null; CurrentSwipePoint = null; }
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