using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections;

public class MixedPointSpawner : MonoBehaviour
{
    public static MixedPointSpawner Instance { get; private set; }

    [SerializeField] private GameObject fountainModeActivationPointPrefab;
    [SerializeField] private GameObject gravityModeActivationPointPrefab;
    [SerializeField] private GameObject vortexModeActivationPointPrefab;

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
    [Range(0f, 0.5f)] public float minDistancePercent = 0.22f;
    public float minScreenDistancePixels = 140f;
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

    [Header("Farbige Tap-Prefabs (Pink / Grün / Blau)")]
    [FormerlySerializedAs("tapPrefab_Red")]
    [SerializeField] private GameObject tapPrefab_Pink;
    [SerializeField] private GameObject tapPrefab_Green;
    [FormerlySerializedAs("tapPrefab_Purple")]
    [SerializeField] private GameObject tapPrefab_Blue;

    [Header("Farbige Swipe-Prefabs (Pink / Grün / Blau)")]
    [FormerlySerializedAs("swipePrefab_Red")]
    [SerializeField] private GameObject swipePrefab_Pink;
    [SerializeField] private GameObject swipePrefab_Green;
    [FormerlySerializedAs("swipePrefab_Purple")]
    [SerializeField] private GameObject swipePrefab_Blue;

    [Header("Farblos (Zufallsbox-Effekt)")]
    [Tooltip("Ersetzt die normalen Farb-Prefabs komplett, solange der Colorless-Effekt aktiv ist (statt " +
             "nur die Farbe zu tinten) — alle 3 Slots nutzen dann dasselbe Prefab, die Spielfarbe " +
             "(PointColor) bleibt intern aber unverändert, nur optisch nicht mehr unterscheidbar.")]
    [SerializeField] private GameObject colorlessTapPrefab;
    [SerializeField] private GameObject colorlessSwipePrefab;

    [Header("Spawn Delay nach Treffer")]
    [SerializeField] private float spawnDelayAfterHit = 0.05f;


    [Header("Floating Score")]
    [SerializeField] private GameObject floatingScorePrefab;
    [SerializeField] private float floatingScoreSpawnOffsetY = 0.8f;
    [Tooltip("Wie lange der Diamant-Sammelzähler-Popup sichtbar stehen bleibt, bevor er wegfadet.")]
    [SerializeField] private float diamondCountHoldDuration = 0.6f;

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

    [Header("Diamant (Normal Mode, ab Phase 9)")]
    [Tooltip("Diamant-Prefab (DiamondPoint). Bonus-Collectible, Sammeln = Punkte, Verpassen = folgenlos.")]
    public GameObject diamondPrefab;
    [Tooltip("Anzahl Diamanten, die pro diamant-aktiver Normal-Phase gespawnt werden.")]
    public int diamondsPerPhase = 7;
    [Range(0f, 1f)]
    [Tooltip("Wahrscheinlichkeit pro Runde (parallel zu den 3 Farb-Slots), dass zusätzlich ein Diamant spawnt.")]
    public float diamondSpawnChance = 0.3f;

    private bool _diamondsEnabledThisPhase = false;
    private bool _roundHasDiamond = false;
    private int _diamondsSpawnedThisPhase = 0;
    private GameObject _currentDiamond;

    /// <summary>Wie viele der in dieser Normal-Phase gespawnten Diamanten bereits eingesammelt wurden.</summary>
    public int DiamondsCollectedThisPhase { get; private set; }

    [Header("Zufallsbox (Normal Mode)")]
    [Tooltip("Zufallsbox-Prefab (MysteryBoxPoint). Sammeln = zufälliger Effekt (positiv oder negativ), Verpassen = folgenlos.")]
    public GameObject mysteryBoxPrefab;
    [Range(0f, 1f)]
    [Tooltip("Wahrscheinlichkeit pro Runde (parallel zu den 3 Farb-Slots), dass zusätzlich eine Zufallsbox spawnt — nur, solange gerade kein Zufalls-Effekt aktiv ist.")]
    public float mysteryBoxSpawnChance = 0.15f;

    private bool _roundHasMysteryBox = false;
    private GameObject _currentMysteryBox;

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

    // Aktuelle Reaktionszeit — Priorität: PhaseManager (Infinity Mode) → InfinityRunManager (Fallback/Multiplayer) → Feld.
    public float CurrentReactionTime =>
        PhaseManager.Instance != null ? PhaseManager.Instance.CurrentReactionTime :
        InfinityRunManager.Instance != null ? InfinityRunManager.Instance.GetReactionTime() : reactionTime;

    /// <summary>Gefeuert bei JEDEM erfolgreichen Normal-Mode-Tap/Swipe-Treffer (nicht bei Special-Mode-Treffern).
    /// Vom PhaseManager genutzt, um die Farb-Zähler für den 20er-Special-Mode-Trigger zu führen.</summary>
    public static event System.Action<PointColor> OnColorHitRegistered;

    /// <summary>Vom PhaseManager beim 20er-Trigger: entfernt alle noch aktiven Slot-Elemente lautlos
    /// (kein Score, kein Schaden) — z.B. wenn ein Special Mode mitten in einer Normal-Phase startet.</summary>
    public void ClearAllSlotsSilently() => ClearAllSlots();

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

        // Ein Element der Runde wurde aufgelöst → frische Dreier-Reihe kommt. Ein evtl. noch aktiver
        // Diamant/Zufallsbox aus der ALTEN Runde muss mit weg (gleiches Prinzip wie früher beim Extra-Shocker).
        DismissCurrentDiamond();
        DismissCurrentMysteryBox();
    }

    // Räumt den aktuell aktiven Diamanten lautlos/folgenlos weg (Puff, kein Score, keine Strafe).
    private void DismissCurrentDiamond()
    {
        if (_currentDiamond == null) return;
        var dp = _currentDiamond.GetComponent<DiamondPoint>();
        if (dp != null) dp.Dismiss(); else Destroy(_currentDiamond);
        _currentDiamond = null;
    }

    // Räumt die aktuell aktive Zufallsbox lautlos/folgenlos weg (Puff, kein Effekt, keine Strafe) —
    // z.B. wenn stattdessen ein Farbelement der Runde getroffen/aufgelöst wurde.
    private void DismissCurrentMysteryBox()
    {
        if (_currentMysteryBox == null) return;
        var mb = _currentMysteryBox.GetComponent<MysteryBoxPoint>();
        if (mb != null) mb.Dismiss(); else Destroy(_currentMysteryBox);
        _currentMysteryBox = null;
    }

    // Hilfsmethoden für farbige Prefabs (Fallback auf Default)
    private GameObject GetTapPrefab(PointColor c) => c switch
    {
        PointColor.Pink   => tapPrefab_Pink   != null ? tapPrefab_Pink   : ActiveNormalPrefab,
        PointColor.Green  => tapPrefab_Green  != null ? tapPrefab_Green  : ActiveNormalPrefab,
        PointColor.Blue   => tapPrefab_Blue   != null ? tapPrefab_Blue   : ActiveNormalPrefab,
        _                 => ActiveNormalPrefab
    };

    private GameObject GetSwipePrefab(PointColor c) => c switch
    {
        PointColor.Pink   => swipePrefab_Pink   != null ? swipePrefab_Pink   : ActiveSwipePrefab,
        PointColor.Green  => swipePrefab_Green  != null ? swipePrefab_Green  : ActiveSwipePrefab,
        PointColor.Blue   => swipePrefab_Blue   != null ? swipePrefab_Blue   : ActiveSwipePrefab,
        _                 => ActiveSwipePrefab
    };

    // Colorless-Effekt: komplettes Prefab-Swap statt Farb-Tint — fällt auf die normale Farbwahl zurück,
    // falls kein farbloses Prefab zugewiesen ist.
    private GameObject GetColorlessPrefab(bool spawnSwipe, PointColor fallbackColor)
    {
        if (spawnSwipe) return colorlessSwipePrefab != null ? colorlessSwipePrefab : GetSwipePrefab(fallbackColor);
        return colorlessTapPrefab != null ? colorlessTapPrefab : GetTapPrefab(fallbackColor);
    }

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
    private bool _roundIsSwipe    = false;

    // Ab Phase 5: EIN Slot der Runde wird (mit thunderSpawnChance) statt eines Farbelements
    // zu einem Shocker — ersetzt, nicht zusätzlich. -1 = kein Shocker diese Runde.
    private int _thunderSlotIndex = -1;


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

        // Alle Slots leer → neuen Rundentyp würfeln.
        bool allEmpty = true;
        foreach (var s in _slots) { if (s.point != null) { allEmpty = false; break; } }

        if (allEmpty)
        {
            // Boosts "All Swipe"/"All Tap": erzwingen den Rundentyp für den kompletten Run — alle
            // Runden sind dann durchgehend Swipe bzw. durchgehend Tap, statt gemischt.
            var activeBoost = BoostManager.Instance != null ? BoostManager.Instance.Selected : BoostType.None;
            if (activeBoost == BoostType.AllSwipe)
            {
                _roundIsSwipe = true;
            }
            else if (activeBoost == BoostType.AllTap)
            {
                _roundIsSwipe = false;
            }
            else
            {
                bool forceSwipe  = maxNormalsInRow > 0 && normalsInRow >= maxNormalsInRow;
                bool forceNormal = maxSwipesInRow  > 0 && swipesInRow  >= maxSwipesInRow;
                _roundIsSwipe = forceSwipe ? true : (forceNormal ? false : Random.value < swipeChance);
            }
            if (_roundIsSwipe) { swipesInRow++; normalsInRow = 0; }
            else               { normalsInRow++; swipesInRow = 0; }

            // Normal-Mode-Shocker (ab Phase 5): EIN Slot der Runde wird statt eines Farbelements
            // zum Shocker (nie mehrere gleichzeitig).
            bool shockerPhaseActive = PhaseManager.Instance != null && PhaseManager.Instance.ShockerEnabledThisPhase;
            bool roundHasShocker = shockerPhaseActive && Random.value < thunderSpawnChance;
            _thunderSlotIndex = roundHasShocker ? Random.Range(0, _slots.Length) : -1;

            // Diamant (ab Phase 9): mit Chance zusätzlich zu den 3 Farb-Slots, solange die
            // Phasen-Quote (diamondsPerPhase) noch nicht ausgeschöpft ist.
            bool diamondsAvailable = _diamondsEnabledThisPhase && _diamondsSpawnedThisPhase < diamondsPerPhase;
            _roundHasDiamond = diamondsAvailable && _currentDiamond == null && Random.value < diamondSpawnChance;

            // Zufallsbox: mit Chance zusätzlich zu den 3 Farb-Slots — aber nur, solange gerade kein
            // Zufalls-Effekt aktiv ist (Design: keine zweite Box, während noch eine wirkt) UND nicht
            // gleichzeitig zum Diamant (Design: die beiden sollen sich nie überschneiden).
            bool mysteryBoxBlocked = (MysteryBoxEffectSystem.Instance != null && MysteryBoxEffectSystem.Instance.IsEffectActive)
                                      || _roundHasDiamond || _currentDiamond != null;
            _roundHasMysteryBox = !mysteryBoxBlocked && _currentMysteryBox == null && Random.value < mysteryBoxSpawnChance;
        }

        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i].point == null)
                SpawnSlot(i);

        // Diamant erst spawnen, wenn alle 3 Slots eine Zielposition haben — bei aktivem Portal-Beam
        // ist .point erst NACH der Flug-Animation gesetzt, die Position aber schon vorher über
        // _pendingSlotPositions reserviert (gleiches Muster wie FindSlotPosition beim Sibling-Check).
        if (_roundHasDiamond && _currentDiamond == null)
        {
            bool allPositioned = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].point == null && !_pendingSlotPositions[i].HasValue) { allPositioned = false; break; }
            }
            if (allPositioned) SpawnDiamond();
        }

        // Zufallsbox ebenso erst, wenn alle 3 Slots eine Zielposition haben (gleiches Muster wie beim Diamant).
        if (_roundHasMysteryBox && _currentMysteryBox == null)
        {
            bool allPositioned = true;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].point == null && !_pendingSlotPositions[i].HasValue) { allPositioned = false; break; }
            }
            if (allPositioned) SpawnMysteryBox();
        }
    }

    // Gibt die Farbe zurück, die in keinem anderen belegten Slot vorhanden ist.
    private PointColor GetUnusedColor(int slotIndex)
    {
        bool pinkUsed = false, greenUsed = false, blueUsed = false;
        for (int j = 0; j < _slots.Length; j++)
        {
            // Slot zählt als belegt wenn er ein point hat ODER sein Beam noch im Flug ist
            if (j == slotIndex || (_slots[j].point == null && !_pendingSlotPositions[j].HasValue)) continue;
            switch (_slots[j].color)
            {
                case PointColor.Pink:  pinkUsed  = true; break;
                case PointColor.Green: greenUsed = true; break;
                case PointColor.Blue:  blueUsed  = true; break;
            }
        }
        var available = new System.Collections.Generic.List<PointColor>(3);
        if (!pinkUsed)   available.Add(PointColor.Pink);
        if (!greenUsed)  available.Add(PointColor.Green);
        if (!blueUsed)   available.Add(PointColor.Blue);
        return available.Count > 0
            ? available[Random.Range(0, available.Count)]
            : (PointColor)Random.Range(0, 3);
    }

    // Spawnt ein neues Element in Slot i (zufällige Farbe, Tap oder Swipe).
    private void SpawnSlot(int i)
    {
        // _pendingSlotPositions[i] ist gesetzt, solange ein Element noch einschwebt (Position schon
        // reserviert, .point aber erst nach onArrived gesetzt) — ohne diesen Check könnte während einer
        // laufenden Einschweb-Animation ein ZWEITES Element in denselben Slot gespawnt werden, sobald
        // z.B. ein Nachbar-Slot getroffen wird und SpawnNextPoint erneut läuft (Slot gilt für dessen
        // simplen null-Check ja noch als "leer"). Das erste Element würde dann nie in _slots[i].point
        // landen und wäre für Treffer-Erkennung (GetActiveSwipePoints etc.) unsichtbar.
        if (!running || spawnPausedForBanner || _slots[i].point != null || _pendingSlotPositions[i].HasValue || isConvertingPoints) return;

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
        bool isThunder  = (i == _thunderSlotIndex);

        // Boost "Stay Positive": das ERSTE Shocker-Vorkommen im Run wird zu einem normalen Tap-
        // Element umgewandelt (isThunder wird lokal false — der Rest von SpawnSlot/FinishSlotSpawn
        // behandelt den Slot danach exakt wie ein normales Farbelement, keine Sonderbehandlung nötig).
        bool convertShocker = isThunder && BoostManager.Instance != null
            && BoostManager.Instance.Selected == BoostType.StayPositive
            && !BoostManager.Instance.StayPositiveConsumed;
        if (convertShocker)
        {
            isThunder = false;
            BoostManager.Instance.StayPositiveConsumed = true;
        }

        bool colorlessActive = MysteryBoxEffectSystem.Instance != null && MysteryBoxEffectSystem.Instance.IsColorlessActive;
        GameObject samplePrefab = isThunder ? ActiveThunderPrefab
                                 : colorlessActive ? GetColorlessPrefab(spawnSwipe, color)
                                 : convertShocker ? GetTapPrefab(color)
                                 : (spawnSwipe ? GetSwipePrefab(color) : GetTapPrefab(color));
        Vector2 viewportPos = FindSlotPosition(i, samplePrefab);
        Vector3 worldPos    = ViewportToWorldOnZ0(viewportPos);

        // Position sofort reservieren, damit Geschwister-Slots sie in FindSlotPosition vermeiden
        _pendingSlotPositions[i] = worldPos;

        var visual = Instantiate(samplePrefab, worldPos, Quaternion.identity);
        ApplyMysteryBoxVisualEffects(visual);

        float reactionTime = CurrentReactionTime;

        // Slot-Bookkeeping (Timer/Fuse/Pulse, _slots[i].point) läuft SOFORT, nicht erst nach der
        // Einschweb-Animation — das Element ist von der ersten Sekunde an treffbar/zerstörbar, die
        // Animation (siehe PointFlyIn) läuft rein optisch parallel dazu.
        _pendingSlotPositions[i] = null;
        FinishSlotSpawn(i, visual, worldPos, color, spawnSwipe, isThunder, reactionTime);

        // ALLE Elemente (Farbe wie Thunder/Shocker) bekommen denselben Spawn-Effekt ("Zaubermaterie"
        // entsteht an Ort und Stelle, siehe PointFlyIn) — entfällt nur im Tutorial. Rein optisch, kein
        // Einfluss mehr auf Treffbarkeit/Timing (siehe oben).
        if (!IsTutorialMode && PointFlyIn.Instance != null)
            PointFlyIn.Instance.PlaySpawnAnimation(visual, worldPos, visual.transform.localScale, null);
    }

    // Wendet aktive Zufallsbox-Effekte (Größen-Multiplikator) auf ein frisch instanziiertes Element an —
    // betrifft Farbelemente, Thunder, Diamant UND die Zufallsbox selbst. Der Colorless-Effekt läuft NICHT
    // mehr hier über einen Tint, sondern per komplettem Prefab-Swap direkt in SpawnSlot (GetColorlessPrefab).
    // Direkt nach jedem Instantiate() aufrufen, BEVOR PointFlyIn die Zielgröße für den Dissolve-Spawn
    // ausliest (sonst würde der Größen-Effekt ignoriert).
    private void ApplyMysteryBoxVisualEffects(GameObject visual)
    {
        MysteryBoxEffectSystem.ApplySizeMultiplier(visual);
    }

    // Startet Timer/Fuse/Pulse für ein bereits instanziiertes Element — nach Puls-Countdown-Stufe 1
    // (Materialisierung) bzw. Pop-in-Ankunft, oder direkt im Tutorial. reactionTime kommt vom
    // Aufrufer (SpawnSlot) statt hier neu berechnet zu werden, damit sie exakt dem Wert entspricht,
    // den PointFlyIn für die Puls-Optik verwendet hat.
    private void FinishSlotSpawn(int i, GameObject newPoint, Vector3 worldPos,
                                  PointColor color, bool spawnSwipe, bool isThunder, float reactionTime)
    {
        if (isThunder)
        {
            _slots[i].isThunder = true;
            var tp = newPoint.GetComponent<ThunderPoint>();
            if (tp != null) tp.Activate(reactionTime);

            _slots[i].point   = newPoint;
            _slots[i].timeout = StartCoroutine(Co_SlotTimeout(i, newPoint, reactionTime));
            lastPoint = newPoint;
            return;
        }

        _slots[i].isThunder = false;

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
            _slots[i].timeout = StartCoroutine(Co_SlotTimeout(i, newPoint, reactionTime));

            var fuse        = newPoint.GetComponentInChildren<FuseCountdown>();
            var lineFuse    = newPoint.GetComponentInChildren<LineFuse>();
            var sparks      = newPoint.GetComponentInChildren<BurnSparks>();
            var countdownSq = newPoint.GetComponentInChildren<CountdownSquare>();
            var pulse       = newPoint.GetComponent<PointPulse>();

            if (fuse        != null) fuse.StartBurn(reactionTime);
            if (lineFuse    != null) lineFuse.StartBurn(reactionTime);
            if (sparks      != null) { sparks.SetQuadMode(tap != null); sparks.StartBurn(reactionTime); }
            if (countdownSq != null) countdownSq.StartCountdown(reactionTime);
            if (pulse       != null) pulse.StartPulsing();

            if (tap != null) TrySpawnFake(reactionTime);
        }
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

            return vp; // perfekte Position gefunden
        }

        // Diagnose: der Fallback greift nur, wenn in 120 Versuchen KEINE Position alle Mindestabstände
        // erfüllt hat — landet der beste gefundene Abstand deutlich unter dem geforderten Minimum,
        // war die Aufgabe für den verfügbaren Platz vermutlich geometrisch gar nicht lösbar (z.B. weil
        // GetAllowedSpawnRect() auf sehr kleinen/schmalen Screens auf ihren 100x100px-Mindestwert
        // zurückfällt). Bewusst IMMER geloggt (nicht nur bei debugLogs), da der Bug selten/zufällig
        // auftritt und wir beim nächsten Mal harte Daten dazu brauchen statt zu raten.
        float requiredDist = GetBaseMinDistancePixels();
        if (bestFallbackDist < requiredDist)
        {
            Rect spawnRectPx = GetAllowedSpawnRect();
            Debug.LogWarning(
                $"[Spawner] FindSlotPosition-Fallback für Slot {slotIndex}: erreichter Mindestabstand " +
                $"{bestFallbackDist:F1}px < gefordert {requiredDist:F1}px. " +
                $"Screen={Screen.width}x{Screen.height}, SpawnRect={spawnRectPx.width:F0}x{spawnRectPx.height:F0}px " +
                $"@({spawnRectPx.x:F0},{spawnRectPx.y:F0}), halfSizePx={halfSizePx:F1}, " +
                $"minDistancePercent={minDistancePercent}, safeArea={Screen.safeArea.width:F0}x{Screen.safeArea.height:F0}."
            );
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

            // Andere Slots lautlos leeren, dann frische Dreier-Reihe
            ClearOtherSlots(i);

            // War es ein Tap (Leben verloren)? Dann auf Animation warten.
            if (LivesManager.IsLifeLostAnimating)
            {
                yield return new WaitUntil(() => !LivesManager.IsLifeLostAnimating);
                yield return null; // FIFO-Sicherheitsframe für RunPhaseTimer
            }
            if (running && !gameOver) StartCoroutine(Co_SpawnAfterDelay(GetSpawnDelay()));
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

        // Extraleben-Ladung (Zufallsbox-Effekt)? Dann wird dieser eigentlich tödliche Fehler
        // einmalig verziehen — Element wird nur normal weggeräumt, kein Game Over. Statt sofort
        // weiterzuspawnen: Spawning pausieren, Verbrauchs-Animation abspielen, erst danach freigeben.
        if (MysteryBoxEffectSystem.Instance != null && MysteryBoxEffectSystem.Instance.ConsumeExtraLifeIfActive())
        {
            DismissCurrentFake();

            _slots[i].timeout = null;
            _slots[i].point   = null;
            if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;
            Destroy(point);

            ClearOtherSlots(i);

            SetBannerPause(true);
            MysteryBoxEffectSystem.Instance.PlayExtraLifeConsumedSequence(this);
            yield break;
        }

        // Timeout: sofortiges Game Over (kein Leben-System mehr)
        DismissCurrentFake();

        _slots[i].timeout = null;
        _slots[i].point   = null;
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;
        Destroy(point);

        ClearOtherSlots(i);

        if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
        yield return new WaitForSecondsRealtime(0.4f);
        GameOver();
    }

    // ─── Diamant (Normal Mode, ab Phase 9) ────────────────────────────────────

    /// <summary>Vom PhaseManager: Diamant-Spawning für die aktuelle Normal-Phase an/aus schalten.</summary>
    public void SetDiamondsEnabled(bool enabled)
    {
        _diamondsEnabledThisPhase = enabled;
        if (!enabled)
        {
            _roundHasDiamond = false;
            DismissCurrentDiamond();
        }
    }

    /// <summary>Vom PhaseManager: Zähler für eine neue diamant-aktive Normal-Phase zurücksetzen.</summary>
    public void ResetDiamondTracking()
    {
        _diamondsSpawnedThisPhase = 0;
        DiamondsCollectedThisPhase = 0;
    }

    // Spawnt den Diamanten an einer Position, die alle 3 Slots + Activation Orb vermeidet. Eigene
    // (kleinere) Abstandsregel statt FindSlotPosition/GetBaseMinDistancePixels: die große prozentuale
    // Mindestdistanz ist für 3 gleichzeitige Hauptelemente gedacht — für ein zusätzliches 4. Element
    // ist sie auf vielen Bildschirmen geometrisch kaum erfüllbar (Suche landet dann fast immer im
    // Fallback, der selbst dann keinen brauchbaren Abstand mehr garantieren kann).
    private void SpawnDiamond()
    {
        if (diamondPrefab == null) return;

        Vector2 vp       = FindDiamondPosition(diamondPrefab);
        Vector3 worldPos = ViewportToWorldOnZ0(vp);
        _currentDiamond  = Instantiate(diamondPrefab, worldPos, Quaternion.identity);
        ApplyMysteryBoxVisualEffects(_currentDiamond);

        var dp = _currentDiamond.GetComponent<DiamondPoint>();
        if (dp != null) dp.spawner = this;

        float reactionTime = CurrentReactionTime;
        if (PointFlyIn.Instance != null)
        {
            Vector3 targetScale = _currentDiamond.transform.localScale;
            PointFlyIn.Instance.PlaySpawnAnimation(_currentDiamond, worldPos, targetScale,
                () => dp?.Activate(reactionTime, skipPopIn: true));
        }
        else
        {
            dp?.Activate(reactionTime);
        }

        _diamondsSpawnedThisPhase++;
    }

    // Tastet viele Kandidatenpunkte im erlaubten Bereich ab und nimmt die ERSTE Position, die von allen
    // Hindernissen einen größenbasierten Mindestabstand einhält (Radius + Radius + Gap, exakt wie
    // IsFarEnoughFromOrb es auch für den Activation Orb/Fake-Elemente nutzt) — kein reines "bester von
    // N Versuchen ohne Schwelle" mehr, das konnte Überlappungen mit den Farbelementen durchrutschen
    // lassen. (Kein zusätzlicher Activation-Orb-Check: der Orb erscheint erst NACHDEM der PhaseManager
    // alles pausiert/geräumt hat, existiert also nie parallel zum Diamant-/Box-Spawning.)
    private Vector2 FindDiamondPosition(GameObject placingPrefab)
    {
        Rect  allowedVP    = ScreenRectToViewportRect(GetAllowedSpawnRect());
        int   sampleCount  = 200;
        float placingHalfPx = GetHalfSizePixels(placingPrefab);

        var obstacleVPs   = new System.Collections.Generic.List<Vector2>(5);
        var obstacleHalfs = new System.Collections.Generic.List<float>(5);
        for (int j = 0; j < _slots.Length; j++)
        {
            if (_slots[j].point != null)
            {
                obstacleVPs.Add(mainCamera.WorldToViewportPoint(_slots[j].point.transform.position));
                obstacleHalfs.Add(GetHalfSizePixels(_slots[j].point));
            }
            else if (_pendingSlotPositions[j].HasValue)
            {
                obstacleVPs.Add(mainCamera.WorldToViewportPoint(_pendingSlotPositions[j].Value));
                obstacleHalfs.Add(placingHalfPx);
            }
        }

        // Diamant und Zufallsbox spawnen laut Design nie gleichzeitig (siehe SpawnNextPoint) — diese
        // Prüfung bleibt trotzdem als Absicherung für den kurzen Übergang beim Einsammeln/Verpuffen.
        if (_currentDiamond != null)
        {
            obstacleVPs.Add(mainCamera.WorldToViewportPoint(_currentDiamond.transform.position));
            obstacleHalfs.Add(GetHalfSizePixels(_currentDiamond));
        }
        if (_currentMysteryBox != null)
        {
            obstacleVPs.Add(mainCamera.WorldToViewportPoint(_currentMysteryBox.transform.position));
            obstacleHalfs.Add(GetHalfSizePixels(_currentMysteryBox));
        }

        Vector2 best        = new Vector2(0.5f, 0.5f);
        float   bestMinDist = -1f;

        for (int i = 0; i < sampleCount; i++)
        {
            Vector2 vp = new Vector2(
                Random.Range(allowedVP.xMin, allowedVP.xMax),
                Random.Range(allowedVP.yMin, allowedVP.yMax)
            );

            bool  valid   = true;
            float minDist = float.MaxValue;
            for (int o = 0; o < obstacleVPs.Count; o++)
            {
                minDist = Mathf.Min(minDist, PixelDistance(vp, obstacleVPs[o]));
                if (!IsFarEnoughFromOrb(vp, obstacleVPs[o], placingHalfPx, obstacleHalfs[o]))
                    valid = false;
            }

            if (obstacleVPs.Count == 0) minDist = 0f; // keine Hindernisse → jede Position gleich gut

            if (valid) return vp; // erste Position, die ALLE Mindestabstände einhält

            if (minDist > bestMinDist)
            {
                bestMinDist = minDist;
                best = vp;
            }
        }

        return best;
    }

    /// <summary>Gefeuert bei jedem eingesammelten Diamanten, mit dem neuen Gesamtstand dieser Phase.
    /// Vom PhaseManager genutzt, um den Bonus-Freigeschaltet-Moment (5+) zu erkennen.</summary>
    public static event System.Action<int, Vector3> OnDiamondCollected;

    /// <summary>Vom DiamondPoint: wurde eingesammelt.</summary>
    public void HandleDiamondCollected(Vector3 worldPos)
    {
        DiamondsCollectedThisPhase++;
        _currentDiamond = null;
        SpawnFloatingDiamondCount(DiamondsCollectedThisPhase, worldPos);
        OnDiamondCollected?.Invoke(DiamondsCollectedThisPhase, worldPos);

        // Wie ein Farbtreffer: aktuelle Runde lautlos beenden (kein Score/Risiko für die 3 Farbelemente),
        // dann kommt die nächste Dreier-Reihe — der Diamant löst also genauso wie die Farbelemente die
        // nächste Runde aus, statt dass der Spieler zusätzlich noch ein Farbelement treffen muss.
        ClearAllSlotsSilently();
        if (running && !gameOver && !spawnPausedForBanner)
            StartCoroutine(Co_SpawnAfterDelay(GetSpawnDelay()));
    }

    /// <summary>Vom DiamondPoint: ist ausgelaufen/wurde weggeräumt (folgenlos).</summary>
    public void HandleDiamondResolved()
    {
        _currentDiamond = null;
    }

    // ─── Zufallsbox (Normal Mode) ──────────────────────────────────────────────

    // Position/Abstandslogik identisch zum Diamant (generische "vermeide die 3 Slots + Orb"-Suche,
    // nichts Diamant-Spezifisches darin) — daher direkt FindDiamondPosition() wiederverwendet.
    private void SpawnMysteryBox()
    {
        if (mysteryBoxPrefab == null) return;

        Vector2 vp       = FindDiamondPosition(mysteryBoxPrefab);
        Vector3 worldPos = ViewportToWorldOnZ0(vp);
        _currentMysteryBox = Instantiate(mysteryBoxPrefab, worldPos, Quaternion.identity);
        ApplyMysteryBoxVisualEffects(_currentMysteryBox);

        var mb = _currentMysteryBox.GetComponent<MysteryBoxPoint>();
        if (mb != null) mb.spawner = this;

        float reactionTime = CurrentReactionTime;
        if (PointFlyIn.Instance != null)
        {
            Vector3 targetScale = _currentMysteryBox.transform.localScale;
            PointFlyIn.Instance.PlaySpawnAnimation(_currentMysteryBox, worldPos, targetScale,
                () => mb?.Activate(reactionTime, skipPopIn: true));
        }
        else
        {
            mb?.Activate(reactionTime);
        }
    }

    /// <summary>Vom MysteryBoxPoint: wurde eingesammelt — Slots sofort lautlos leeren, Spawning aber
    /// pausieren (wie beim Phasen-Banner), bis die Ankündigungs-Sequenz (Partikel → Effekt-Name-Text
    /// → Effekt-Start, siehe MysteryBoxEffectSystem.PlayCollectSequence) fertig ist.</summary>
    public void HandleMysteryBoxCollected()
    {
        _currentMysteryBox = null;
        ClearAllSlotsSilently();
        SetBannerPause(true);
    }

    /// <summary>Allgemeines Ende einer per SetBannerPause(true) pausierten Zwischensequenz (Mystery-Box-
    /// Ankündigung ODER Extra-Life-Verbrauchs-Animation, siehe MysteryBoxEffectSystem) — Spawning wieder
    /// freigeben, die nächste Dreier-Reihe kommt smooth weiter.</summary>
    public void ResumeSpawningAfterPause()
    {
        if (running && !gameOver)
            SetBannerPause(false);
    }

    /// <summary>Vom MysteryBoxPoint: ist ausgelaufen/wurde weggeräumt (folgenlos).</summary>
    public void HandleMysteryBoxResolved()
    {
        _currentMysteryBox = null;
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

        // Bereits gelandete Elemente pulsieren laufend um ±pulseAmount (PointPulse) — eine Live-Messung
        // würde je nach zufälligem Zeitpunkt im Pulse-Zyklus einen zu kleinen Radius liefern und dadurch
        // den Sicherheitsabstand zu neu gespawnten Nachbarn gelegentlich unterschätzen (sichtbares
        // Überlappen, sobald der Pulse wieder aufwächst). Korrektur: gemessene Größe auf die stabile
        // Basis-Scale hochrechnen statt die live schwankende zu übernehmen.
        if (!isPrefab)
        {
            var pulse = target.GetComponent<PointPulse>();
            if (pulse != null)
            {
                float liveUniformScale = target.transform.lossyScale.x; // Pulse skaliert immer uniform
                float baseUniformScale = pulse.BaseScale.x;
                if (liveUniformScale > 0.0001f)
                    half *= baseUniformScale / liveUniformScale;
            }
        }

        if (isPrefab) Destroy(target);

        return half;
    }


    // ─── Fake Point ────────────────────────────────────────────────────────────

    // Spawnt mit fakeSpawnChance ein Fake-Element parallel zum echten Tap-Point.
    // Nur wenn noch kein Fake existiert und eine Position ohne Overlap gefunden wird.
    private void TrySpawnFake(float lifetime)
    {
        if (ActiveFakePrefab == null) return;
        if (currentFake != null) return;                 // schon ein Fake da
        if (Random.value > fakeSpawnChance) return;      // Chance verfehlt

        if (!TryFindFakePosition(out Vector3 worldPos)) return;

        currentFake = Instantiate(ActiveFakePrefab, worldPos, Quaternion.identity);
        var fp = currentFake.GetComponent<FakePoint>();

        if (PointFlyIn.Instance != null)
        {
            Vector3 targetScale = currentFake.transform.localScale;
            PointFlyIn.Instance.PlaySpawnAnimation(currentFake, worldPos, targetScale,
                () => fp?.Activate(lifetime));
        }
        else
        {
            fp?.Activate(lifetime);
        }
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

    // Sucht eine Position im erlaubten Spawn-Bereich, die weit genug von ALLEN aktuell belegten
    // Slots entfernt ist (kein optisches Überlappen) — nicht nur vom einen zugehörigen realPoint.
    // realPoint ist zum Aufrufzeitpunkt bereits selbst einer der _slots (siehe FinishSlotSpawn:
    // _slots[i].point wird VOR dem TrySpawnFake-Aufruf gesetzt), taucht also automatisch mit auf.
    // Ohne diesen Check gegen die ANDEREN Slots konnte ein Fake ungebremst genau auf einem der
    // beiden übrigen Farbelemente derselben Runde landen (komplettes Overlap, nicht nur zu knapp).
    private bool TryFindFakePosition(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        Rect allowedScreen   = GetAllowedSpawnRect();
        Rect allowedViewport = ScreenRectToViewportRect(allowedScreen);

        float fakeHalfPx = GetHalfSizePixels(ActiveFakePrefab);

        var obstacleVPs   = new System.Collections.Generic.List<Vector2>(_slots.Length);
        var obstacleHalfs = new System.Collections.Generic.List<float>(_slots.Length);
        for (int j = 0; j < _slots.Length; j++)
        {
            if (_slots[j].point != null)
            {
                obstacleVPs.Add(mainCamera.WorldToViewportPoint(_slots[j].point.transform.position));
                obstacleHalfs.Add(GetHalfSizePixels(_slots[j].point));
            }
            else if (_pendingSlotPositions[j].HasValue)
            {
                obstacleVPs.Add(mainCamera.WorldToViewportPoint(_pendingSlotPositions[j].Value));
                obstacleHalfs.Add(fakeHalfPx);
            }
        }

        for (int i = 0; i < 40; i++)
        {
            Vector2 vp = new Vector2(
                Random.Range(allowedViewport.xMin, allowedViewport.xMax),
                Random.Range(allowedViewport.yMin, allowedViewport.yMax)
            );

            bool valid = true;
            for (int k = 0; k < obstacleVPs.Count; k++)
            {
                if (!IsFarEnoughFromOrb(vp, obstacleVPs[k], fakeHalfPx, obstacleHalfs[k])) { valid = false; break; }
            }
            if (!valid) continue;

            worldPos = ViewportToWorldOnZ0(vp);
            return true;
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
        _slots[0].color = PointColor.Pink; // Tutorial hat keine Farbe → Platzhalter
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

            if (tap != null) TrySpawnFake(dynamicTime);
        }
        else if (!IsTutorialMode)
        {
            if (debugLogs) Debug.Log("[Spawner] Kein Timer gestartet.");
        }
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

        // Special Modes laufen als eigene Coroutinen auf ihrem eigenen Component — MixedPointSpawner.running
        // stoppt die NICHT automatisch mit. Ohne das hier würde z.B. FountainModeSystem nach Game Over
        // munter weiter spawnen.
        GravityModeSystem.Instance?.ForceStop();
        FountainModeSystem.Instance?.ForceStop();
        VortexModeSystem.Instance?.ForceStop();
        CrystalEndPhaseSystem.Instance?.ForceStop();

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
        int dreamEnergyReward = DreamEnergyManager.OnGameFinished(score);

        onGameOver?.Invoke();
        uiManager?.ShowGameOver(score, dreamEnergyReward);

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
        yield return new WaitForSecondsRealtime(0.4f);
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

    /// <summary>Liefert eine zufällige Weltposition innerhalb des normalen Spawn-Bereichs (derselbe
    /// Bereich wie die 3 Farb-Slots, siehe GetAllowedSpawnRect) — für Effekte, die wie die Elemente
    /// selbst im Spielbereich erscheinen sollen (z.B. Zufallsbox-Rauch statt eines festen Weltbereichs).</summary>
    public Vector3 GetRandomWorldPosInSpawnArea()
    {
        Rect allowedVP = ScreenRectToViewportRect(GetAllowedSpawnRect());
        Vector2 vp = new Vector2(Random.Range(allowedVP.xMin, allowedVP.xMax), Random.Range(allowedVP.yMin, allowedVP.yMax));
        return ViewportToWorldOnZ0(vp);
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
        // Kristall-Endphase: eigene Punktzahl, kein Slot-Ökosystem (Farben/Fake/Respawn-Runde) —
        // komplett eigener Ablauf statt der normalen Treffer-Logik unten.
        if (_crystalPhaseActive) { HandleCrystalPointHit(point); return; }

        DismissCurrentFake();

        int idx = FindSlotIndex(point);
        PointColor color = idx >= 0 ? _slots[idx].color : PointColor.Pink;

        OnColorHitRegistered?.Invoke(color);

        int scored = ScoreManager.Instance?.AddPointsFromHit(10) ?? 0;
        SpawnFloatingScore(scored, point.transform.position, color, 0);

        bool isSwipe = point.GetComponent<SwipePoint>() != null;
        if (isSwipe) AudioManager.Instance?.PlaySwipePoint(0);
        else         AudioManager.Instance?.PlayNormalPoint(0);

        var bp = point.GetComponent<BasePoint>();
        if (bp != null) bp.SendMessage("SpawnExplosion");

        // Parallel zur VFX-Explosion: Energiekugel Richtung der farblich passenden Fairy.
        FairyEnergyManager.Instance?.SpawnEnergyOrb(color, point.transform.position);

        if (idx >= 0)
        {
            if (_slots[idx].timeout != null) { StopCoroutine(_slots[idx].timeout); _slots[idx].timeout = null; }
            _slots[idx].point = null;
        }
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;

        Destroy(point);

        // Ein Treffer → alle anderen Slots lautlos leeren, dann frische Reihe
        if (idx >= 0) ClearOtherSlots(idx);

        if (running && !gameOver && !spawnPausedForBanner)
            StartCoroutine(Co_SpawnAfterDelay(GetSpawnDelay()));
    }

    // ─── Kristall-Endphase ──────────────────────────────────────────────────────

    private bool _crystalPhaseActive = false;

    /// <summary>Vom CrystalEndPhaseSystem gesetzt/gelöscht — solange aktiv, läuft HandlePointHit
    /// komplett am normalen Farb-/Slot-System vorbei (siehe HandleCrystalPointHit).</summary>
    public void SetCrystalPhaseActive(bool active) => _crystalPhaseActive = active;

    /// <summary>Spawnt ein Kristall-Element in Slot 0 — wie CreatePoint (Tutorial), aber ohne
    /// Timer/Fuse/Fake-Verkabelung: die Kristall-Endphase steuert Timing/Punkte komplett selbst
    /// über HandleCrystalPointHit + ihre eigene Reaktionszeit-Coroutine.</summary>
    public void CreateCrystalPoint(GameObject prefab, Vector3 worldPos)
    {
        if (_slots[0].point != null) { Destroy(_slots[0].point); _slots[0].point = null; }

        var newPoint = Instantiate(prefab, worldPos, Quaternion.identity);

        var tap = newPoint.GetComponent<TapPoint>();
        if (tap) tap.spawner = this;

        var swipe = newPoint.GetComponent<SwipePoint>();
        if (swipe) { swipe.spawner = this; CurrentSwipePoint = swipe; }
        else       { CurrentSwipePoint = null; }

        _slots[0].point = newPoint;
        lastPoint = newPoint;
    }

    private void HandleCrystalPointHit(GameObject point)
    {
        int scored = ScoreManager.Instance?.AddPointsFromHit(CrystalEndPhaseSystem.Instance != null ? CrystalEndPhaseSystem.Instance.PointsPerHit : 50) ?? 0;
        SpawnFloatingScore(scored, point.transform.position, PointColor.Pink, 0); // Farbe hier nur für Textfarbe, Kristall hat keine eigene

        bool isSwipe = point.GetComponent<SwipePoint>() != null;
        if (isSwipe) AudioManager.Instance?.PlaySwipePoint(0);
        else         AudioManager.Instance?.PlayNormalPoint(0);

        var bp = point.GetComponent<BasePoint>();
        if (bp != null) bp.SendMessage("SpawnExplosion");

        if (_slots[0].point == point) _slots[0].point = null;
        if (CurrentSwipePoint != null && CurrentSwipePoint.gameObject == point) CurrentSwipePoint = null;

        Destroy(point);

        CrystalEndPhaseSystem.Instance?.OnElementHit();
    }

    public void TriggerGameOverFromCrystalPhase()
    {
        _gameOverCause = "crystal_phase";
        StartCoroutine(Co_DelayedGameOver());
    }

    private float GetSpawnDelay() => spawnDelayAfterHit;

    private IEnumerator Co_SpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (running && !gameOver && !spawnPausedForBanner)
            SpawnNextPoint();
    }


    private void SpawnFloatingScore(int score, Vector3 worldPos, PointColor color, int streak)
    {
        // score == 0 statt <= 0: negative Werte (Zufallsbox-Minuspunkte-Effekt) sollen weiterhin
        // als Floating Score angezeigt werden, nur ein Treffer für exakt 0 Punkte bleibt stumm.
        if (floatingScorePrefab == null || score == 0) return;

        Vector3 spawnPos = worldPos + Vector3.up * floatingScoreSpawnOffsetY;
        var go  = Instantiate(floatingScorePrefab, spawnPos, Quaternion.identity);
        var fst = go.GetComponent<FloatingScoreText>();
        fst?.Play(score, Color.white, color);
    }

    /// <summary>Gleiches Prinzip wie SpawnFloatingScore (Pop-In + Fade an der Trefferposition), aber zeigt
    /// keine Punktzahl an, sondern den laufenden Diamant-Sammelstand dieser Phase.</summary>
    private void SpawnFloatingDiamondCount(int count, Vector3 worldPos)
    {
        if (floatingScorePrefab == null || count <= 0) return;

        Vector3 spawnPos = worldPos + Vector3.up * floatingScoreSpawnOffsetY;
        var go  = Instantiate(floatingScorePrefab, spawnPos, Quaternion.identity);
        var fst = go.GetComponent<FloatingScoreText>();

        // Bei Erreichen der Bonus-Schwelle statt der Zahl "BONUS" anzeigen.
        bool bonusJustReached = PhaseManager.Instance != null && count == PhaseManager.Instance.DiamondsNeededForBonus;
        fst?.Play(count, Color.white, showPlusPrefix: false, holdDuration: diamondCountHoldDuration,
            overrideText: bonusJustReached ? "BONUS" : null);
    }

    /// <summary>Floating-Score für Special-Mode-Treffer (Gravity/Fountain) — die haben keine PointColor,
    /// daher eigenes Material statt der materialPink/Green/Blue-Auswahl (pro Special-Mode auf
    /// GravityModeSystem/FountainModeSystem einstellbar).</summary>
    public void SpawnSpecialFloatingScore(int score, Vector3 worldPos, Material textMaterial)
    {
        // score == 0 statt <= 0: negative Werte (Zufallsbox-Minuspunkte-Effekt) sollen weiterhin
        // als Floating Score angezeigt werden, nur ein Treffer für exakt 0 Punkte bleibt stumm.
        if (floatingScorePrefab == null || score == 0) return;

        Vector3 spawnPos = worldPos + Vector3.up * floatingScoreSpawnOffsetY;
        var go  = Instantiate(floatingScorePrefab, spawnPos, Quaternion.identity);
        var fst = go.GetComponent<FloatingScoreText>();
        fst?.Play(score, Color.white, null, 1f, textMaterial);
    }

    // Bonus-Floating-Score an einer Viewport-Position spawnen (aktuell ungenutzt, da SequenceManager
    // nicht mehr in HandlePointHit eingebunden ist — bleibt als Utility für spätere Bonus-Anzeigen stehen).
    public void SpawnBonusFloatingScore(int amount, Vector2 viewportPos, float scale, PointColor pointColor)
    {
        if (floatingScorePrefab == null || amount <= 0) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 worldPos = cam.ViewportToWorldPoint(new Vector3(viewportPos.x, viewportPos.y, Mathf.Abs(cam.transform.position.z)));
        worldPos.z = 0f;
        var go = Instantiate(floatingScorePrefab, worldPos, Quaternion.identity);
        go.GetComponent<FloatingScoreText>()?.Play(amount, Color.white, pointColor, scale);
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
        GameObject prefab = mode switch
        {
            SpecialMode.Fountain => fountainModeActivationPointPrefab,
            SpecialMode.Vortex   => vortexModeActivationPointPrefab,
            _                    => gravityModeActivationPointPrefab
        };
        if (prefab == null) { Debug.LogWarning($"[Spawner] Kein Activation-Orb-Prefab für {mode}."); return; }

        Vector3 pos = ViewportToWorldOnZ0(new Vector2(0.5f, 0.5f));
        var orb = Instantiate(prefab, pos, Quaternion.identity);

        var g = orb.GetComponent<GravityModeActivationPoint>();  if (g != null) g.spawner = this;
        var f = orb.GetComponent<FountainModeActivationPoint>(); if (f != null) f.spawner = this;
        var v = orb.GetComponent<VortexModeActivationPoint>();   if (v != null) v.spawner = this;

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

        CreatePoint(prefab, worldPos);
        if (lockUntilOverlay)
            LockTutorialPoint(isTap, forcedDir);
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