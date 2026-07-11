using System;
using System.Collections;
using UnityEngine;

public enum PhaseType { Normal, Special }

[Serializable]
public class PhaseDefinition
{
    public string label = "Phase";
    public PhaseType type = PhaseType.Normal;

    [Tooltip("Reaktionszeit/Spawn-Tempo dieser Phase (Sekunden). Kleiner = schneller/intensiver.")]
    public float reactionTime = 3f;

    [Header("Nur Normal-Phasen")]
    [Tooltip("Shocker-Elemente aktiv (ab Phase 5).")]
    public bool shockerEnabled = false;
    [Range(0f, 1f)]
    public float shockerChance = 0.2f;
    [Tooltip("Diamanten spawnen (ab Phase 9): 7 pro Phase, 5 sammeln schaltet den Bonus für die NÄCHSTE Special-Phase frei.")]
    public bool diamondsEnabled = false;

    [Header("Nur Special-Phasen")]
    [Tooltip("Feste Anzahl gespawnter Elemente bis Phasenende.")]
    public int specialElementCount = 20;
    [Tooltip("Score-Multiplikator dieser Special-Phase (normal 3x).")]
    public int specialScoreMultiplier = 3;
    [Tooltip("Geschwindigkeits-/Wucht-Multiplikator für Gravity (Fallgeschwindigkeit+Sogkraft) bzw. Fountain " +
             "(Schusskraft) — welcher Mode läuft, hängt von der auslösenden Farbe ab, daher EIN gemeinsamer Wert.")]
    public float specialIntensityMultiplier = 1f;
}

/// <summary>
/// Dirigent für Infinity Mode: führt persistente Farb-Zähler (Pink/Green/Blue), löst bei 20 Treffern
/// automatisch den passenden Special Mode aus und schaltet durch die 12 fest definierten Phasen
/// (Normal/Special, ab Phase 5 Shocker, ab Phase 9 Diamanten, ab Phase 10 Diamant-Bonus-Elemente).
/// Nur für GameMode.Infinity aktiv — Multiplayer läuft unverändert über die alte, phasenlose Logik.
/// </summary>
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [SerializeField] private MixedPointSpawner spawner;

    [Header("Phasen (Inspector-Feinjustierung)")]
    [SerializeField] private PhaseDefinition[] phases = BuildDefaultPhases();

    [Header("Farb-Trigger")]
    [Tooltip("Wie viele Treffer einer Farbe in einer Normal-Phase nötig sind, um deren Special Mode auszulösen.")]
    [SerializeField] private int colorTriggerThreshold = 20;

    [Header("Diamant-Bonus")]
    [Tooltip("Wie viele der 7 Diamanten pro Normal-Phase gesammelt werden müssen, damit die nächste Special-Phase Bonus-Elemente bekommt.")]
    [SerializeField] private int diamondsNeededForBonus = 5;

    [Header("Phase 13 (Endless, grober Platzhalter)")]
    [Tooltip("Wie viel die Reaktionszeit der letzten 2 Phasen pro Endless-Durchlauf zusätzlich sinkt.")]
    [SerializeField] private float endlessReactionTimeStep = 0.05f;
    [SerializeField] private float endlessMinReactionTime = 0.6f;

    private int _currentIndex = -1;
    private readonly int[] _destroyedCount = new int[3]; // indiziert per PointColor
    private bool _running = false;
    private int _endlessLoopCount = 0;
    private Coroutine _triggerRoutine;

    public PhaseDefinition CurrentPhase =>
        (_currentIndex >= 0 && phases != null && _currentIndex < phases.Length) ? phases[_currentIndex] : null;

    public float CurrentReactionTime => CurrentPhase != null ? CurrentPhase.reactionTime : 3f;
    public int CurrentSpecialMultiplier => CurrentPhase != null && CurrentPhase.type == PhaseType.Special ? CurrentPhase.specialScoreMultiplier : 1;
    public bool ShockerEnabledThisPhase => CurrentPhase != null && CurrentPhase.shockerEnabled;

    public int ColorTriggerThreshold => colorTriggerThreshold;
    public int GetColorCount(PointColor color) => _destroyedCount[(int)color];

    /// <summary>Gefeuert bei jeder Änderung eines Farb-Zählers (Treffer ODER Reset bei Special-Mode-Trigger).
    /// Für UI-Anzeigen wie "12/20". Args: Farbe, aktueller Stand, Schwelle.</summary>
    public static event Action<PointColor, int, int> OnColorProgressChanged;

    private bool IsInfinityRun =>
        GlobalGameManager.Instance != null && GlobalGameManager.Instance.SelectedMode == GameMode.Infinity;

    private void Awake()
    {
        Instance = this;
    }

    // Beim erstmaligen Hinzufügen des Components in Unity (oder via Context-Menu "Reset"):
    // befüllt die 12 Phasen mit sinnvollen Defaults nach der Spec, statt leerer Einträge.
    private void Reset()
    {
        phases = BuildDefaultPhases();
    }

    private static PhaseDefinition[] BuildDefaultPhases()
    {
        float[] reactionTimes  = { 3.0f, 2.6f, 2.4f, 2.1f, 1.9f, 1.7f, 1.6f, 1.4f, 1.3f, 1.15f, 1.05f, 0.9f };
        float[] intensityMults = { 1.0f, 1.0f, 1.05f, 1.1f, 1.15f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 1.45f, 1.5f };
        var list = new PhaseDefinition[12];
        for (int i = 0; i < 12; i++)
        {
            int phaseNumber   = i + 1;
            bool isSpecial    = phaseNumber % 2 == 0;
            bool shocker      = phaseNumber >= 5;
            bool diamonds     = !isSpecial && phaseNumber >= 9;

            list[i] = new PhaseDefinition
            {
                label                     = $"Phase {phaseNumber}",
                type                      = isSpecial ? PhaseType.Special : PhaseType.Normal,
                reactionTime              = reactionTimes[i],
                shockerEnabled            = shocker,
                shockerChance             = shocker ? 0.2f : 0f,
                diamondsEnabled           = diamonds,
                specialElementCount       = 20,
                specialScoreMultiplier    = 3,
                specialIntensityMultiplier = intensityMults[i]
            };
        }
        return list;
    }

    private void OnEnable()
    {
        MixedPointSpawner.OnColorHitRegistered += HandleColorHit;
        GravityModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
    }

    private void OnDisable()
    {
        MixedPointSpawner.OnColorHitRegistered -= HandleColorHit;
        GravityModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Vom GameStartCoordinator statt spawner.Begin() aufgerufen (Infinity Mode). Startet
    /// Phase 1 und übergibt danach an den normalen Spawner.</summary>
    public void BeginRun()
    {
        if (!IsInfinityRun || phases == null || phases.Length == 0)
        {
            spawner.Begin();
            return;
        }

        _running = true;
        Array.Clear(_destroyedCount, 0, _destroyedCount.Length);
        _endlessLoopCount = 0;
        _currentIndex = 0;

        OnColorProgressChanged?.Invoke(PointColor.Pink, 0, colorTriggerThreshold);
        OnColorProgressChanged?.Invoke(PointColor.Green, 0, colorTriggerThreshold);
        OnColorProgressChanged?.Invoke(PointColor.Blue, 0, colorTriggerThreshold);

        spawner.onGameOver.RemoveListener(HandleGameOver); // gegen doppelte Listener bei mehreren Runs (Play Again)
        spawner.onGameOver.AddListener(HandleGameOver);

        ApplyPhaseSettings();
        spawner.Begin();
    }

    private void HandleGameOver()
    {
        _running = false;
        if (_triggerRoutine != null) { StopCoroutine(_triggerRoutine); _triggerRoutine = null; }
    }

    private void ApplyPhaseSettings()
    {
        var def = CurrentPhase;
        if (def == null || spawner == null) return;

        spawner.thunderSpawnChance = def.shockerEnabled ? def.shockerChance : 0f;
        // Portal-Elektrifizierung (Tap-aufs-Portal-zum-Entschärfen) ist im neuen Redesign nicht
        // gewollt — nur die fliegenden/fallenden Shocker-Elemente sollen als Gefahr existieren.
        spawner.electricPortalChance = 0f;

        bool diamondsThisPhase = def.type == PhaseType.Normal && def.diamondsEnabled;
        // Bei JEDER Normal-Phase zurücksetzen (nicht nur diamant-aktiven) — sonst würde ein alter
        // Sammelstand aus einer früheren Diamant-Phase in einer viel späteren Special-Phase fälschlich
        // noch als "Bonus verdient" durchgereicht, obwohl die direkt vorherige Normal-Phase gar keine
        // Diamanten hatte.
        if (def.type == PhaseType.Normal) spawner.ResetDiamondTracking();
        spawner.SetDiamondsEnabled(diamondsThisPhase);
    }

    private void HandleColorHit(PointColor color)
    {
        if (!_running || CurrentPhase == null || CurrentPhase.type != PhaseType.Normal) return;

        _destroyedCount[(int)color]++;
        OnColorProgressChanged?.Invoke(color, _destroyedCount[(int)color], colorTriggerThreshold);

        if (_destroyedCount[(int)color] >= colorTriggerThreshold)
            _triggerRoutine = StartCoroutine(Co_TriggerSpecialMode(color));
    }

    private IEnumerator Co_TriggerSpecialMode(PointColor color)
    {
        _running = false; // Zähler pausieren während des Übergangs

        // Andere Farben, die gerade noch aktiv sind: lautlos entfernen (kein Score, kein Risiko).
        spawner.SetBannerPause(true);
        spawner.ClearAllSlotsSilently();

        _destroyedCount[(int)color] = 0;
        OnColorProgressChanged?.Invoke(color, 0, colorTriggerThreshold);

        // Diamant-Bonus für die kommende Special-Phase: basiert auf der GERADE beendeten Normal-Phase.
        bool bonusEarned = spawner.DiamondsCollectedThisPhase >= diamondsNeededForBonus;

        SpecialMode mode = color switch
        {
            PointColor.Blue  => SpecialMode.Fountain,
            PointColor.Green => SpecialMode.Vortex,
            _                => SpecialMode.Gravity // Pink
        };

        // Zur nächsten Phase (per Definition immer die zugehörige Special-Phase) weiterschalten.
        _currentIndex++;
        ApplyPhaseSettings();
        var def = CurrentPhase;

        int count = def != null ? def.specialElementCount : 20;
        bool diamondBonusActive = bonusEarned; // automatisch, sobald in der vorigen Normal-Phase genug gesammelt wurde
        float intensity = def != null ? def.specialIntensityMultiplier : 1f;

        switch (mode)
        {
            case SpecialMode.Fountain:
                FountainModeSystem.Instance.PendingMaxSpawnCount = count;
                FountainModeSystem.Instance.PendingDiamondBonusActive = diamondBonusActive;
                FountainModeSystem.Instance.PendingIntensity = intensity;
                break;
            case SpecialMode.Vortex:
                VortexModeSystem.Instance.PendingMaxSpawnCount = count;
                VortexModeSystem.Instance.PendingDiamondBonusActive = diamondBonusActive;
                VortexModeSystem.Instance.PendingIntensity = intensity;
                break;
            default:
                GravityModeSystem.Instance.PendingMaxSpawnCount = count;
                GravityModeSystem.Instance.PendingDiamondBonusActive = diamondBonusActive;
                GravityModeSystem.Instance.PendingIntensity = intensity;
                break;
        }

        spawner.SpawnActivationOrb(mode);
        yield return new WaitUntil(() => SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive);

        _triggerRoutine = null;
        _running = true;
    }

    // Wird NICHT bei Game Over gefeuert: ForceStop() der Mode-Systeme killt deren Coroutine
    // (StopAllCoroutines) bevor der Spawn-Count-Loop das Event auslösen kann.
    private void HandleSpecialPhaseComplete()
    {
        AdvanceToNextNormalPhase();
    }

    private void AdvanceToNextNormalPhase()
    {
        _currentIndex++;

        if (phases == null || phases.Length == 0) return;

        if (_currentIndex >= phases.Length)
        {
            // Phase 13 (Endless-Platzhalter): letzte 2 Phasen wiederholen, Intensität leicht steigern.
            _endlessLoopCount++;
            _currentIndex = Mathf.Max(0, phases.Length - 2);
            var loopDef = phases[_currentIndex];
            if (loopDef != null)
                loopDef.reactionTime = Mathf.Max(endlessMinReactionTime, loopDef.reactionTime - endlessReactionTimeStep * _endlessLoopCount);
        }

        ApplyPhaseSettings();
        _running = true;

        spawner.SetBannerPause(false);
    }
}
