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
    [Tooltip("Wie viele Treffer einer Farbe in dieser Phase nötig sind, um deren Special Mode auszulösen.")]
    public int colorTriggerThreshold = 15;
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

    [Header("Diamant-Bonus")]
    [Tooltip("Wie viele der 7 Diamanten pro Normal-Phase gesammelt werden müssen, damit eine zufällige Farbe den Bonus für ihren NÄCHSTEN Special Mode zugelost bekommt.")]
    [SerializeField] private int diamondsNeededForBonus = 5;

    public int DiamondsNeededForBonus => diamondsNeededForBonus;

    [Header("Phase 13 (Endless, grober Platzhalter)")]
    [Tooltip("Wie viel die Reaktionszeit der letzten 2 Phasen pro Endless-Durchlauf zusätzlich sinkt.")]
    [SerializeField] private float endlessReactionTimeStep = 0.05f;
    [SerializeField] private float endlessMinReactionTime = 0.6f;

    private int _currentIndex = -1;
    private readonly int[] _destroyedCount = new int[3]; // indiziert per PointColor
    private bool _running = false;
    private int _endlessLoopCount = 0;
    private Coroutine _triggerRoutine;
    // Pro Farbe unabhängig: true = diese Farbe trägt aktuell einen unverbrauchten Diamant-Bonus.
    // Mehrere Farben können GLEICHZEITIG einen Bonus tragen — ein fremder Special Mode verbraucht/
    // verwirft ihn NICHT mehr, nur der eigene Special Mode der jeweiligen Farbe tut das (siehe
    // Co_TriggerSpecialMode). _bonusRolledThisPhase verhindert nur ein zweites Losen INNERHALB
    // derselben Diamant-Phase, sobald die Schwelle einmal erreicht wurde.
    private readonly bool[] _colorHasBonus = new bool[3];
    private bool _bonusRolledThisPhase = false;

    public PhaseDefinition CurrentPhase =>
        (_currentIndex >= 0 && phases != null && _currentIndex < phases.Length) ? phases[_currentIndex] : null;

    public float CurrentReactionTime => CurrentPhase != null ? CurrentPhase.reactionTime : 3f;
    public int CurrentSpecialMultiplier => CurrentPhase != null && CurrentPhase.type == PhaseType.Special ? CurrentPhase.specialScoreMultiplier : 1;
    public bool ShockerEnabledThisPhase => CurrentPhase != null && CurrentPhase.shockerEnabled;

    public int ColorTriggerThreshold => CurrentPhase != null ? CurrentPhase.colorTriggerThreshold : 15;
    public int GetColorCount(PointColor color) => _destroyedCount[(int)color];

    /// <summary>Welcher Special Mode zu welcher Farbe gehört (Pink→Gravity, Green→Vortex, Blue→Fountain).
    /// Zentral hier definiert, damit z.B. UI-Scripts dieselbe Zuordnung nutzen wie der Trigger selbst.</summary>
    public static SpecialMode SpecialModeForColor(PointColor color) => color switch
    {
        PointColor.Blue  => SpecialMode.Fountain,
        PointColor.Green => SpecialMode.Vortex,
        _                => SpecialMode.Gravity // Pink
    };

    /// <summary>Gefeuert bei jeder Änderung eines Farb-Zählers (Treffer ODER Reset bei Special-Mode-Trigger).
    /// Für UI-Anzeigen wie "12/20". Args: Farbe, aktueller Stand, Schwelle.</summary>
    public static event Action<PointColor, int, int> OnColorProgressChanged;

    /// <summary>Gefeuert, sobald in einer diamant-aktiven Normal-Phase die Bonus-Schwelle erreicht wird
    /// UND noch mindestens eine Farbe ohne Bonus übrig ist. Übergibt die zufällig unter den Farben OHNE
    /// Bonus geloste Farbe — der Bonus bleibt aktiv, bis GENAU diese Farbe ihren eigenen Special Mode
    /// auslöst (andere Special Modes verbrauchen ihn nicht). Für UI: Bonus-Icon über der passenden
    /// Farb-Anzeige einblenden.</summary>
    public static event Action<PointColor> OnDiamondBonusEarned;

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
        MixedPointSpawner.OnDiamondCollected += HandleDiamondCollected;
        GravityModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete += HandleSpecialPhaseComplete;
    }

    private void OnDisable()
    {
        MixedPointSpawner.OnColorHitRegistered -= HandleColorHit;
        MixedPointSpawner.OnDiamondCollected -= HandleDiamondCollected;
        GravityModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        FountainModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
        VortexModeSystem.OnSpecialPhaseComplete -= HandleSpecialPhaseComplete;
    }

    private void HandleDiamondCollected(int totalThisPhase)
    {
        if (_bonusRolledThisPhase) return; // in dieser Phase schon gelost
        if (totalThisPhase < diamondsNeededForBonus) return;
        if (CurrentPhase == null || CurrentPhase.type != PhaseType.Normal || !CurrentPhase.diamondsEnabled) return;

        _bonusRolledThisPhase = true;

        // Bonus-Schwelle erreicht → für den Rest dieser Normal-Phase spawnen keine weiteren Diamanten
        // mehr (dismisst auch einen evtl. gerade fliegenden Diamanten). Erst die nächste diamant-aktive
        // Normal-Phase (via ApplyPhaseSettings) schaltet das Spawning wieder frei.
        spawner.SetDiamondsEnabled(false);

        // Nur unter den Farben losen, die noch KEINEN Bonus tragen. Haben alle 3 schon einen, bleibt
        // schlicht nichts mehr zu vergeben (kein Event, kein Fehler).
        var eligible = new System.Collections.Generic.List<PointColor>(3);
        for (int i = 0; i < 3; i++)
            if (!_colorHasBonus[i]) eligible.Add((PointColor)i);
        if (eligible.Count == 0) return;

        PointColor chosen = eligible[UnityEngine.Random.Range(0, eligible.Count)];
        _colorHasBonus[(int)chosen] = true;

        // Einzeln statt via ?.Invoke(): sonst würde eine Exception in EINEM Subscriber (z.B. einer der
        // 3 DiamondBonusIndicatorUI-Instanzen) die Invocation für alle NACHFOLGENDEN Subscriber in der
        // Liste stillschweigend abbrechen — inklusive evtl. der Farbe, die eigentlich gewonnen hat.
        if (OnDiamondBonusEarned != null)
        {
            foreach (Action<PointColor> handler in OnDiamondBonusEarned.GetInvocationList())
            {
                try { handler(chosen); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
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

        OnColorProgressChanged?.Invoke(PointColor.Pink, 0, ColorTriggerThreshold);
        OnColorProgressChanged?.Invoke(PointColor.Green, 0, ColorTriggerThreshold);
        OnColorProgressChanged?.Invoke(PointColor.Blue, 0, ColorTriggerThreshold);

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

        bool diamondsThisPhase = def.type == PhaseType.Normal && def.diamondsEnabled;
        // Bei JEDER Normal-Phase zurücksetzen (nicht nur diamant-aktiven) — sonst würde ein alter
        // Sammelstand aus einer früheren Diamant-Phase in einer viel späteren Special-Phase fälschlich
        // noch als "Bonus verdient" durchgereicht, obwohl die direkt vorherige Normal-Phase gar keine
        // Diamanten hatte.
        if (def.type == PhaseType.Normal) spawner.ResetDiamondTracking();
        // Nur die "schon gelost"-Sperre für DIESE Phase zurücksetzen — _colorHasBonus bleibt unberührt,
        // bereits vergebene (aber noch nicht verbrauchte) Boni überleben Phasenwechsel unverändert.
        if (diamondsThisPhase) _bonusRolledThisPhase = false;
        spawner.SetDiamondsEnabled(diamondsThisPhase);
    }

    private void HandleColorHit(PointColor color)
    {
        if (!_running || CurrentPhase == null || CurrentPhase.type != PhaseType.Normal) return;

        _destroyedCount[(int)color]++;
        OnColorProgressChanged?.Invoke(color, _destroyedCount[(int)color], ColorTriggerThreshold);

        if (_destroyedCount[(int)color] >= ColorTriggerThreshold)
            _triggerRoutine = StartCoroutine(Co_TriggerSpecialMode(color));
    }

    private IEnumerator Co_TriggerSpecialMode(PointColor color)
    {
        _running = false; // Zähler pausieren während des Übergangs

        // Andere Farben, die gerade noch aktiv sind: lautlos entfernen (kein Score, kein Risiko).
        spawner.SetBannerPause(true);
        spawner.ClearAllSlotsSilently();

        _destroyedCount[(int)color] = 0;
        OnColorProgressChanged?.Invoke(color, 0, ColorTriggerThreshold);

        // Diamant-Bonus für die kommende Special-Phase: greift, wenn DIESE Farbe gerade einen Bonus
        // trägt. Nur ihr eigener Bonus wird dabei verbraucht — Boni anderer Farben bleiben unberührt
        // und warten weiter auf ihren eigenen Special-Mode-Trigger.
        bool diamondBonusActive = _colorHasBonus[(int)color];
        if (diamondBonusActive) _colorHasBonus[(int)color] = false;

        SpecialMode mode = SpecialModeForColor(color);

        // Zur nächsten Phase (per Definition immer die zugehörige Special-Phase) weiterschalten.
        _currentIndex++;
        ApplyPhaseSettings();
        var def = CurrentPhase;

        int count = def != null ? def.specialElementCount : 20;
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
