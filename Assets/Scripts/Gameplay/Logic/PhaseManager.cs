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

    [Header("Übergang Special Mode → Normal Mode")]
    [Tooltip("Pause nach Ende eines Special Modes, bevor der nächste Normal Mode zu spawnen beginnt.")]
    [SerializeField] private float postSpecialModePause = 2f;

    [Header("Ramp-up bei Phasenwechsel")]
    [Tooltip("Über wie viele Elementreihen mindestens von der alten zur neuen Reaktionszeit übergeblendet wird.")]
    [SerializeField] private int minRampRows = 1;
    [Tooltip("Über wie viele Elementreihen höchstens übergeblendet wird — bei sehr großen Tempo-Sprüngen.")]
    [SerializeField] private int maxRampRows = 4;
    [Tooltip("Relative Reaktionszeit-Änderung (0.3 = 30%), ab der die maximale Ramp-Länge (maxRampRows) erreicht wird. Kleinere Sprünge bekommen proportional weniger Reihen.")]
    [SerializeField] private float relativeChangeForMaxRamp = 0.3f;
    [Tooltip("Verlauf der Überblendung innerhalb der Ramp (X=Fortschritt 0-1, Y=Anteil neue Reaktionszeit 0-1). Default: langsamer Start, dann zügiger ans Ziel.")]
    [SerializeField] private AnimationCurve rampEaseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.3f),
        new Keyframe(1f, 1f, 1.7f, 0f));

    public int DiamondsNeededForBonus => diamondsNeededForBonus;

    // Ramp-Zustand: bei jedem Phasenwechsel neu gesetzt (siehe ApplyPhaseSettings), von
    // MixedPointSpawner.NotifyRowSpawned() pro neu gestarteter Elementreihe hochgezählt.
    private float _rampFromReactionTime;
    private float _rampToReactionTime;
    private int   _rampLength;
    private int   _rowsSinceRamp;
    private bool  _hasAppliedPhaseBefore;
    private float _rowStartTime;

    private int _currentIndex = -1;
    private readonly int[] _destroyedCount = new int[3]; // indiziert per PointColor
    private bool _running = false;
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

    /// <summary>Reaktionszeit für die GERADE aktuelle Elementreihe — direkt nach einem Phasenwechsel noch
    /// sanft von der alten zur neuen Phase übergeblendet (siehe rampEaseCurve), danach der reine
    /// Phasenwert. MixedPointSpawner UND die Eye-Rays-Intensität lesen beide diesen einen Wert, bleiben
    /// dadurch automatisch synchron.</summary>
    public float CurrentReactionTime
    {
        get
        {
            if (CurrentPhase == null) return 3f;
            if (_rampLength <= 0 || _rowsSinceRamp >= _rampLength) return _rampToReactionTime;

            float t = rampEaseCurve.Evaluate((float)_rowsSinceRamp / _rampLength);
            return Mathf.Lerp(_rampFromReactionTime, _rampToReactionTime, t);
        }
    }

    /// <summary>Vom MixedPointSpawner aufgerufen, sobald eine neue Elementreihe zu spawnen beginnt —
    /// zählt NUR den Ramp-Fortschritt seit dem letzten Phasenwechsel hoch. Der Startzeitpunkt für
    /// CurrentRowProgress01 wird bewusst NICHT hier gesetzt, sondern schon früher in
    /// NotifyElementDestroyed() — sonst würde der Eye-Rays-Reset erst nach dem kurzen Spawn-Delay
    /// nach einem Treffer einsetzen, statt sofort beim Treffer selbst.</summary>
    public void NotifyRowSpawned()
    {
        if (_rowsSinceRamp < _rampLength) _rowsSinceRamp++;
    }

    /// <summary>Vom MixedPointSpawner SOFORT beim Zerstören eines Elements aufgerufen (als eines der
    /// ersten Dinge in HandlePointHit, nicht erst beim tatsächlichen Start der nächsten Reihe) — setzt
    /// CurrentRowProgress01 unmittelbar zurück, damit z.B. EyeRaysIntensity ohne spürbare Verzögerung
    /// zu reagieren beginnt.</summary>
    public void NotifyElementDestroyed()
    {
        _rowStartTime = Time.time;
    }

    /// <summary>0 = die aktuelle Elementreihe ist gerade erst gespawnt bzw. das letzte Element wurde
    /// gerade zerstört (volle Reaktionszeit übrig), 1 = ihre Reaktionszeit ist komplett verstrichen. Für
    /// Optik, die live mit dem Zeitdruck pro Reihe mitgehen soll (z.B. EyeRaysIntensity) — steigt
    /// kontinuierlich an, springt bei NotifyElementDestroyed() sofort zurück auf 0.</summary>
    public float CurrentRowProgress01
    {
        get
        {
            // Vor BeginRun() (z.B. während der Boost-Auswahl) bzw. nach Game Over läuft noch keine
            // echte Reihe — ohne diesen Guard würde der Fallback-Reaktionszeitwert (3s) den Fortschritt
            // trotzdem hochzählen lassen, obwohl das gar nichts mit tatsächlichem Zeitdruck zu tun hat.
            if (!_running) return 0f;

            // Special Mode: läuft über ein komplett anderes Trefferystem (GravityModeSystem/etc.), das
            // NotifyElementDestroyed() nie aufruft — _rowStartTime bliebe stehen, während Time.time
            // weiterläuft, wodurch der Fortschritt einfach bis zum Anschlag hochlaufen würde. Passt
            // ohnehin zur Design-Entscheidung "Special Mode = sichere Verschnaufpause" → fest aufs
            // Minimum.
            if (CurrentPhase != null && CurrentPhase.type == PhaseType.Special) return 0f;

            // Spawning gerade pausiert (Banner/Zwischensequenz, z.B. Zufallsbox-Extraleben-Verbrauch,
            // Special-Mode-Übergang): NUR während dieser kurzen Animation/Pause aufs Minimum zwingen —
            // bewusst NICHT an MysteryBoxEffectSystem.IsEffectActive gekoppelt, das bei manchen Effekten
            // (z.B. Größen-Multiplikator, Colorless) noch über viele NACHFOLGENDE, ganz normal
            // weiterlaufende Reihen aktiv bleibt, in denen der Ray wieder normal reagieren soll.
            if (spawner != null && spawner.IsSpawnPausedForBanner) return 0f;

            float rt = CurrentReactionTime;
            if (rt <= 0.0001f) return 1f;
            return Mathf.Clamp01((Time.time - _rowStartTime) / rt);
        }
    }

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

        // Ohne das bliebe _rowStartTime beim C#-Default 0 stehen, bis der erste Treffer/BeginRun()
        // passiert — CurrentRowProgress01 (z.B. für EyeRaysIntensity) würde schon VOR Spielstart (z.B.
        // während der Boost-Auswahl) fälschlich "komplett abgelaufen" berechnen, weil Time.time zu dem
        // Zeitpunkt schon deutlich über 0 liegt.
        _rowStartTime = Time.time;
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

    private void HandleDiamondCollected(int totalThisPhase, Vector3 worldPos)
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

        // Energiekugel vom 5. (gerade zerstörten) Diamanten zur Fairy, die den Bonus bekommt.
        FairyEnergyManager.Instance?.SpawnEnergyOrb(chosen, worldPos);

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
        _currentIndex = 0;

        // Ramp-Zustand für einen frischen Run zurücksetzen (bei "Play Again" darf kein Ramp-Rest vom
        // vorherigen Run überleben) — Phase 1 startet direkt ohne Überblendung, es gibt ja nichts,
        // wovon aus geglättet werden könnte.
        _hasAppliedPhaseBefore = false;
        _rampLength = 0;
        _rowsSinceRamp = 0;
        // Ohne diesen Reset bliebe _rowStartTime auf 0 (C#-Default) stehen, bis der erste Treffer
        // NotifyElementDestroyed() aufruft — CurrentRowProgress01 würde die allererste Reihe fälschlich
        // als "komplett abgelaufen" berechnen (Time.time ist beim Run-Start ja meist schon > 0).
        _rowStartTime = Time.time;

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

        // "Play Again" lädt die Szene nicht neu → Zufallsbox-Effektzustand (laufende Coroutinen,
        // Rauch-Overlay, Extra-Life-Ladung, Größen-/Farblos-Zustand) sonst in den nächsten Run
        // übernommen. Special-Mode-Systeme räumt MixedPointSpawner.EndGame() bereits via ForceStop() auf.
        MysteryBoxEffectSystem.Instance?.ResetState();
    }

    private void ApplyPhaseSettings()
    {
        var def = CurrentPhase;
        if (def == null || spawner == null) return;

        // Ramp-Setup: von der zuletzt EFFEKTIVEN Reaktionszeit (falls die vorherige Phase selbst noch
        // mitten in einer Überblendung war, nicht vom rohen Phasenwert) sanft zum neuen Phasenwert
        // übergehen. Länge proportional zur relativen Größe des Sprungs, innerhalb Min/Max gekappt.
        bool wasFirstPhase = !_hasAppliedPhaseBefore;
        float fromRT = wasFirstPhase ? def.reactionTime : CurrentReactionTime;
        _rampFromReactionTime = fromRT;
        _rampToReactionTime   = def.reactionTime;
        _rowsSinceRamp        = 0;
        _hasAppliedPhaseBefore = true;

        if (wasFirstPhase || fromRT <= 0.0001f || Mathf.Approximately(fromRT, def.reactionTime))
        {
            _rampLength = 0; // nichts zu überblenden (erste Phase oder keine echte Änderung)
        }
        else
        {
            float relativeChange = Mathf.Abs(fromRT - def.reactionTime) / fromRT;
            float p = Mathf.Clamp01(relativeChange / Mathf.Max(0.0001f, relativeChangeForMaxRamp));
            _rampLength = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minRampRows, maxRampRows, p)), minRampRows, maxRampRows);
        }

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
        StartCoroutine(Co_DelayedAdvanceToNormalPhase());
    }

    // spawner.SetBannerPause(true) läuft seit Beginn des Special Modes (Co_TriggerSpecialMode)
    // bereits durchgehend — das Spawning bleibt also während dieser Pause automatisch angehalten,
    // ohne dass hier zusätzlich etwas gesetzt werden muss.
    private IEnumerator Co_DelayedAdvanceToNormalPhase()
    {
        yield return new WaitForSeconds(postSpecialModePause);
        AdvanceToNextNormalPhase();
    }

    private void AdvanceToNextNormalPhase()
    {
        _currentIndex++;

        if (phases == null || phases.Length == 0) return;

        if (_currentIndex >= phases.Length)
        {
            // Alle regulären Phasen durchlaufen → finale Kristall-Endphase statt endlosem
            // Weiterlaufen. spawner.SetBannerPause(false) bleibt hier bewusst aus — die
            // Kristallphase pausiert das normale Spawning ohnehin komplett selbst.
            _running = false;
            CrystalEndPhaseSystem.Instance?.Begin(spawner);
            return;
        }

        ApplyPhaseSettings();
        _running = true;

        // Die neue Normal-Phase kann einen anderen colorTriggerThreshold haben als die vorherige.
        // Die nicht-auslösende(n) Farbe(n) behalten ihren Sammelstand über den Special Mode hinweg —
        // ohne dieses Re-Broadcast würde ColorProgressUI die Balken weiter gegen den ALTEN Schwellenwert
        // füllen, bis diese Farbe das nächste Mal getroffen wird (sichtbarer Sprung im Balken).
        BroadcastAllColorProgress();

        spawner.SetBannerPause(false);
    }

    private void BroadcastAllColorProgress()
    {
        int threshold = ColorTriggerThreshold;
        OnColorProgressChanged?.Invoke(PointColor.Pink,  _destroyedCount[(int)PointColor.Pink],  threshold);
        OnColorProgressChanged?.Invoke(PointColor.Green, _destroyedCount[(int)PointColor.Green], threshold);
        OnColorProgressChanged?.Invoke(PointColor.Blue,  _destroyedCount[(int)PointColor.Blue],  threshold);
    }
}
