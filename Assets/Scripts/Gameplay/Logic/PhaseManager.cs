using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Dirigent des Infinity-Mode-Phasensystems: feste 30-s-Spielphasen, Entscheidungs-
/// Zwischenphasen (vor Special-Mode-Phasen), Reaktionszeit-Curve, Special-Mode-Phasen
/// und Win nach der letzten Phase. Ersetzt das alte kontinuierliche LevelUp-System.
///
/// Schritt 1: Play-Phasen + Curve + Banner + Win laufen. Special-Phasen starten vorerst
/// automatisch den Default-Modus (Entscheidungs-UI + Orb-Inszenierung folgen in Schritt 2).
/// </summary>
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    public enum PhaseKind { Play, Special }

    [Serializable]
    public class PhaseDef
    {
        public PhaseKind kind = PhaseKind.Play;
        [Range(0f, 1f)] public float thunderChance = 0f;   // Shocker-Wahrscheinlichkeit
        [Range(0f, 1f)] public float fakeChance    = 0f;    // Fake-Wahrscheinlichkeit
        [Range(0f, 1f)] public float peekChance    = 0f;    // Peek-a-boo (Standard aus)
        [Tooltip("Vor dieser Phase erscheint die Entscheidungs-UI (Special-Mode-Wahl).")]
        public bool decisionBefore = false;
    }

    [Serializable]
    public class ReactionSegment
    {
        [Tooltip("Dauer dieser Unterphase in Sekunden.")]
        public float duration;
        [Tooltip("Reaktionszeit (Zeit pro Element) während dieser Unterphase, in Sekunden.")]
        public float reactionTime;
    }

    [Header("Phasen")]
    [SerializeField] private float phaseDurationSec = 30f;
    [SerializeField] private PhaseDef[] phases =
    {
        new PhaseDef { kind = PhaseKind.Play,    thunderChance = 0f,   fakeChance = 0f },                       // P1
        new PhaseDef { kind = PhaseKind.Special, decisionBefore = true },                                       // P2
        new PhaseDef { kind = PhaseKind.Play,    thunderChance = 0.2f, fakeChance = 0f },                       // P3
        new PhaseDef { kind = PhaseKind.Special, thunderChance = 0.2f, decisionBefore = true },                 // P4 (+Shocker)
        new PhaseDef { kind = PhaseKind.Play,    thunderChance = 0.2f, fakeChance = 0.2f },                     // P5
        new PhaseDef { kind = PhaseKind.Special, thunderChance = 0.2f, fakeChance = 0.2f, decisionBefore = true }, // P6 (Special +Shocker +Fake)
    };

    [Header("Reaktionszeit-Curve / Unterphasen (gilt in JEDER Phase; Summe der Dauern = Phase Duration)")]
    [SerializeField] private ReactionSegment[] reactionCurve =
    {
        new ReactionSegment { duration = 5f,  reactionTime = 2.0f },
        new ReactionSegment { duration = 10f, reactionTime = 1.2f },
        new ReactionSegment { duration = 5f,  reactionTime = 1.6f },
        new ReactionSegment { duration = 10f, reactionTime = 0.8f },
    };
    [Tooltip("Zieht pro Phase X Sekunden von jedem Curve-Segment ab (0 = nicht schneller).")]
    [SerializeField] private float perPhaseSpeedup = 0f;
    [SerializeField] private float minReactionTime = 0.3f;

    [Header("Banner")]
    [SerializeField] private float bannerDurationSec = 1.2f;

    [Header("Zwischenphase nach Special Mode")]
    [Tooltip("Gesamtdauer der Pause nach einer Special-Mode-Phase (wird hälftig auf beide Texte verteilt).")]
    [SerializeField] private float interphaseDurationSec = 5f;
    [Tooltip("Text in der ersten Hälfte. {0} = gerade abgeschlossene Phasennummer.")]
    [SerializeField] private string clearedFormat = "Phase {0} cleared";
    [Tooltip("Text in der zweiten Hälfte / Start-Banner. {0} = Phasennummer.")]
    [SerializeField] private string startingFormat = "Phase {0}";

    [Header("Phasenende / Auslaufen")]
    [Tooltip("Max. Zeit (s), die auf das natürliche Auslaufen der Restelemente gewartet wird, " +
             "bevor zur Sicherheit hart geräumt wird.")]
    [SerializeField] private float maxDrainSec = 6f;

    [Header("Entscheidung")]
    [Tooltip("Wartezeit (realtime) fürs Ausblenden der Entscheidungs-UI, bevor der Orb kommt.")]
    [SerializeField] private float decisionFadeOutSec = 0.3f;

    // ----- Events für UI -----
    /// <summary>(phaseNumber 1-basiert, totalPhases)</summary>
    public static event Action<int, int> OnPhaseBanner;
    /// <summary>Freitext-Banner mit Sichtbarkeits-Fenster in Sekunden (text, visibleSeconds).
    /// Genutzt für die „Phase X cleared" / „Starting Phase X+1"-Zwischenphase.</summary>
    public static event Action<string, float> OnPhaseTextBanner;
    /// <summary>Entscheidungs-UI einblenden (Special-Mode wählen).</summary>
    public static event Action OnDecisionRequested;
    /// <summary>Entscheidungs-UI ausblenden (Wahl getroffen).</summary>
    public static event Action OnDecisionClosed;
    public static event Action OnGameWon;
    /// <summary>SequenceTrackerRow ausblenden (Special-Phase beginnt).</summary>
    public static event Action OnSequenceRowHide;
    /// <summary>SequenceTrackerRow einblenden (Play-Phase beginnt).</summary>
    public static event Action OnSequenceRowShow;

    /// <summary>Aktuelle Reaktionszeit aus der Curve — vom Spawner gelesen (ersetzt LevelUp).</summary>
    public float CurrentReactionTime { get; private set; } = 2f;

    /// <summary>Normalisierte Intensität 0..1 (0 = langsamstes Curve-Segment, 1 = schnellstes).
    /// Für Special Modes (Spawn-Tempo / Element-Geschwindigkeit), damit sie dem Curve folgen.</summary>
    public float CurrentIntensity01 =>
        _maxRT > _minRT ? Mathf.Clamp01((_maxRT - CurrentReactionTime) / (_maxRT - _minRT)) : 0f;

    public int CurrentPhaseNumber => _phaseIndex + 1;

    float _minRT = 0.8f, _maxRT = 2f;

    int          _phaseIndex = -1;
    float        _phaseElapsed;
    bool         _running;
    SpecialMode  _chosenMode = SpecialMode.Gravity;
    bool         _decisionMade;
    SpecialMode  _decisionResult;

    /// <summary>Von der Entscheidungs-UI aufrufen, wenn der Spieler einen Modus gewählt hat.</summary>
    public void ChooseMode(SpecialMode mode)
    {
        _decisionResult = mode;
        _decisionMade   = true;
    }

    MixedPointSpawner Spawner => MixedPointSpawner.Instance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        CurrentReactionTime = reactionCurve != null && reactionCurve.Length > 0
            ? reactionCurve[0].reactionTime : 2f;

        // Min/Max der Curve für die normalisierte Intensität bestimmen.
        if (reactionCurve != null && reactionCurve.Length > 0)
        {
            _minRT = float.MaxValue; _maxRT = float.MinValue;
            foreach (var seg in reactionCurve)
            {
                if (seg.reactionTime < _minRT) _minRT = seg.reactionTime;
                if (seg.reactionTime > _maxRT) _maxRT = seg.reactionTime;
            }
        }
    }

    /// <summary>Vom GameStartCoordinator nach dem Countdown aufrufen (statt spawner.Begin()).</summary>
    public void BeginRun()
    {
        if (_running) return;
        _running = true;
        StartCoroutine(Co_RunPhases());
    }

    IEnumerator Co_RunPhases()
    {
        // PhaseManager steuert die Orbs komplett — kein zufälliges Orb-Spawning in Play-Phasen.
        if (Spawner != null) Spawner.allowRandomActivationOrbs = false;

        for (_phaseIndex = 0; _phaseIndex < phases.Length; _phaseIndex++)
        {
            var def = phases[_phaseIndex];
            ResetCurveForPhaseStart();   // Curve auf Phasenanfang, bevor irgendetwas spawnt

            // Entscheidungsphase: „Phase X cleared" → Decision-UI → „Starting Phase X+1".
            if (def.decisionBefore)
                yield return Co_DecisionInterphase(def, _phaseIndex, _phaseIndex + 1);

            if (def.kind == PhaseKind.Play)
            {
                // „Phase X"-Banner nur, wenn KEINE Special-Phase davor war — sonst hat die
                // Zwischenphase („Starting Phase X") den Übergang schon angekündigt.
                bool prevWasSpecial = _phaseIndex > 0 && phases[_phaseIndex - 1].kind == PhaseKind.Special;
                if (!prevWasSpecial)
                    yield return Co_PhaseBanner(_phaseIndex + 1);
                yield return Co_PlayPhase(def);
            }
            else
            {
                yield return Co_SpecialPhase(def);              // Special: Entscheidung + Orb sind der Übergang

                // Nach jeder Special-Phase 5-s-Zwischenphase („Phase X cleared" / „Starting Phase X+1"),
                // sofern noch eine Phase folgt (nach der letzten kommt der Win-Screen).
                if (_phaseIndex + 1 < phases.Length)
                    yield return Co_InterphaseBanner(_phaseIndex + 1, _phaseIndex + 2);
            }
        }

        // Alle Phasen geschafft → gewonnen
        Spawner?.StopSpawning();
        Spawner?.ClearAllGameplayPoints();
        OnGameWon?.Invoke();
        Debug.Log("[Phase] Alle Phasen abgeschlossen → GEWONNEN.");

        // Schritt 1 (Platzhalter): bestehendes Ergebnis-Panel zeigen.
        // Eigener „Gewonnen"-Screen kommt später.
        var ui = FindFirstObjectByType<GameUIManager>();
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
        ui?.ShowGameOver(score);
    }

    // ----------------------------------------------------------------- Play-Phase
    IEnumerator Co_PlayPhase(PhaseDef def)
    {
        if (Spawner != null)
        {
            Spawner.thunderSpawnChance = def.thunderChance;
            Spawner.fakeSpawnChance    = def.fakeChance;
            Spawner.peekABooChance     = def.peekChance;
            if (!Spawner.IsRunning) Spawner.Begin();
            else                    Spawner.SetBannerPause(false);   // ggf. von Vorphase/Zwischenphase pausiert → fortsetzen
        }
        yield return RunPhaseTimer();
        yield return Co_EndPhaseDrain();
    }

    // ----------------------------------------------------------------- Special-Phase
    IEnumerator Co_SpecialPhase(PhaseDef def)
    {
        // WICHTIG: Spawner NICHT stoppen! Die Mode-Systeme (Gravity/Fountain) pausieren den
        // normalen Spawner selbst (PauseSpawning) und beenden sich über StopMode.
        if (Spawner != null)
        {
            Spawner.thunderSpawnChance = def.thunderChance;   // Shocker-Anteil im Mode
            Spawner.fakeSpawnChance    = def.fakeChance;      // Fake-Anteil im Mode
            // Normales Spawning hart aus, solange der Special Mode läuft (kein „eines noch"-Element).
            // KEIN Begin() hier — das würde entpausieren UND einen normalen Point spawnen.
            Spawner.SetBannerPause(true);

            // Orb-Inszenierung: spielt seine Animation selbst ab und ruft am Ende StartMode(_chosenMode).
            Debug.Log($"[Phase] Special-Phase {_phaseIndex + 1}: Orb({_chosenMode})");
            Spawner.SpawnActivationOrb(_chosenMode);
        }

        // Warten, bis der Orb fertig ist und der Mode wirklich läuft (Intro zählt NICHT zur Phasenzeit).
        float guard = 0f;
        while ((SpecialModeManager.Instance == null || !SpecialModeManager.Instance.IsModeActive) && guard < 10f)
        {
            guard += Time.deltaTime;
            yield return null;
        }

        // 30 s reine Mode-Zeit (folgt dem Curve).
        yield return RunPhaseTimer();

        // Phasenende: nichts mehr spawnen, Restelemente normal auslaufen lassen, dann Mode beenden.
        yield return Co_EndPhaseDrain();
        Debug.Log($"[Phase] Special-Phase {_phaseIndex + 1} beendet.");
    }

    // Am Ende JEDER Phase: KEIN Nachspawn mehr, aber die noch aktiven Elemente laufen normal aus
    // (der Spieler spielt sie regulär zu Ende — Treffer geben Punkte, Verpassen kostet wie sonst).
    // Erst wenn das letzte Element weg ist, wird der Special Mode endgültig beendet und es geht weiter.
    IEnumerator Co_EndPhaseDrain()
    {
        if (Spawner == null) yield break;

        bool special = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;

        // 1) Nachspawn stoppen — laufende Elemente bleiben aber spielbar.
        //    Special Mode: nur den Spawn-Loop anhalten (Portal/Scoring/Input bleiben bis zum Drain-Ende aktiv).
        if (special)
        {
            if (_chosenMode == SpecialMode.Gravity)       GravityModeSystem.Instance?.StopSpawning();
            else if (_chosenMode == SpecialMode.Fountain) FountainModeSystem.Instance?.StopSpawning();
        }

        // 2) Auslaufen lassen (Spieler beendet Restelemente normal), dann Sicherheitsnetz-Clear.
        yield return Co_DrainGameplay();

        // 3) Jetzt den Special Mode wirklich beenden (Portal/Scoring waren bis hier aktiv).
        if (special)
        {
            if (_chosenMode == SpecialMode.Gravity)       GravityModeSystem.Instance?.StopMode();
            else if (_chosenMode == SpecialMode.Fountain) FountainModeSystem.Instance?.StopMode();
            else                                          SpecialModeManager.Instance.EndCurrentMode();
        }
    }

    /// <summary>Pausiert das Nachspawnen und wartet, bis KEIN spielbares Element mehr da ist
    /// (der Spieler beendet die Restelemente normal). Greift der Sicherheits-Cap, wird hart positiv
    /// geräumt. Garantiert: danach ist die Szene leer (wichtig, bevor ein „… cleared"-Banner kommt).</summary>
    IEnumerator Co_DrainGameplay()
    {
        if (Spawner == null) yield break;

        Spawner.SetBannerPause(true);   // kein Nachspawn

        // Cap gegen 0/nicht-gesetzt absichern (neu hinzugefügtes SerializeField kann in einer
        // bestehenden Szene als 0 serialisiert sein → würde sofort hart räumen statt auslaufen).
        float cap = maxDrainSec > 0f ? maxDrainSec : 6f;
        float guard = 0f;
        while (Spawner.HasActiveGameplayPoints() && guard < cap)
        {
            guard += Time.deltaTime;
            yield return null;
        }

        // Sicherheitsnetz: falls noch etwas hängt (Cap erreicht), positiv räumen + 1 Frame,
        // damit die Destroy()-Aufrufe wirklich greifen, bevor es weitergeht.
        if (Spawner.HasActiveGameplayPoints())
        {
            Spawner.PositiveClearAll();
            yield return null;
        }
    }

    /// <summary>Bei Game Over aufrufen → stoppt den Phasen-Ablauf.</summary>
    public void StopRun()
    {
        if (!_running) return;
        _running = false;
        StopAllCoroutines();
    }

    // ----------------------------------------------------------------- Entscheidung
    // Wickelt die Entscheidung in die Zwischenphasen-Banner:
    //   „Phase X cleared" (Vorphase) → Decision-UI (Wahl) → „Starting Phase X+1" (Special-Phase).
    IEnumerator Co_DecisionInterphase(PhaseDef def, int clearedPhase, int nextPhase)
    {
        // Garantie: erst zeigen, wenn wirklich kein Element mehr da ist (sonst „cleared" trotz Restelement).
        yield return Co_DrainGameplay();

        float half = InterphaseHalf;

        // 1) „Phase X cleared" — SequenceRow fliegt parallel nach links raus.
        if (clearedPhase >= 1)
        {
            OnSequenceRowHide?.Invoke();
            OnPhaseTextBanner?.Invoke(ClearedText(clearedPhase), half);
            yield return new WaitForSecondsRealtime(half);
        }

        // 2) Entscheidungs-UI: Wahl treffen.
        yield return Co_Decision(def);

        // 3) „Starting Phase X+1".
        OnPhaseTextBanner?.Invoke(StartingText(nextPhase), half);
        yield return new WaitForSecondsRealtime(half);
    }

    // Sichere Werte, falls die SerializeFields in einer bestehenden Szene als 0/leer serialisiert wurden.
    float InterphaseHalf => Mathf.Max(0.1f, (interphaseDurationSec > 0f ? interphaseDurationSec : 5f) * 0.5f);
    string ClearedText(int n)  => string.Format(string.IsNullOrEmpty(clearedFormat)  ? "Phase {0} cleared"   : clearedFormat,  n);
    string StartingText(int n) => string.Format(string.IsNullOrEmpty(startingFormat) ? "Phase {0}" : startingFormat, n);

    IEnumerator Co_Decision(PhaseDef def)
    {
        // Keine Entscheidungs-UI in der Szene? → vorläufig automatisch abwechselnd wählen (Test).
        if (OnDecisionRequested == null)
        {
            _chosenMode = (_chosenMode == SpecialMode.Gravity) ? SpecialMode.Fountain : SpecialMode.Gravity;
            yield break;
        }

        // KEIN Time.timeScale=0: Das Gameplay ist während der Entscheidung ohnehin im Leerlauf
        // (Phase geräumt, Spawning pausiert, kein Timer, kein Special Mode). So bleibt das Spiel
        // effektiv „pausiert", aber UI-VFX/Animationen laufen weiter (timeScale=0 würde VFX einfrieren).
        _decisionMade = false;
        OnDecisionRequested.Invoke();     // UI einblenden

        yield return new WaitUntil(() => _decisionMade);

        _chosenMode = _decisionResult;
        OnDecisionClosed?.Invoke();       // UI ausblenden
        yield return new WaitForSecondsRealtime(decisionFadeOutSec);
    }

    // ----------------------------------------------------------------- Banner
    // Erster Banner (nur vor P1): „Starting Phase 1" — gleiche Schreibweise wie die Zwischenphasen.
    IEnumerator Co_PhaseBanner(int phaseNumber)
    {
        if (Spawner != null) Spawner.SetBannerPause(true);
        OnPhaseTextBanner?.Invoke(StartingText(phaseNumber), bannerDurationSec);
        yield return new WaitForSecondsRealtime(bannerDurationSec);
        if (Spawner != null) Spawner.SetBannerPause(false);
    }

    // 5-s-Zwischenphase nach einer Special-Mode-Phase:
    //   erste Hälfte „Phase X cleared", zweite Hälfte „Starting Phase X+1" (je rein/raus faden).
    // Spawning bleibt pausiert (vom Drain her) — die nächste Spielphase setzt es selbst fort.
    IEnumerator Co_InterphaseBanner(int clearedPhase, int nextPhase)
    {
        // Garantie: erst zeigen, wenn wirklich kein Element mehr da ist.
        yield return Co_DrainGameplay();

        float half = InterphaseHalf;

        OnPhaseTextBanner?.Invoke(ClearedText(clearedPhase), half);
        yield return new WaitForSecondsRealtime(half);

        // „Starting Phase X+1" — SequenceRow fliegt parallel von links wieder rein.
        OnSequenceRowShow?.Invoke();
        OnPhaseTextBanner?.Invoke(StartingText(nextPhase), half);
        yield return new WaitForSecondsRealtime(half);
    }

    // ----------------------------------------------------------------- Timer + Curve
    // Setzt den Curve auf den Phasenanfang, BEVOR das erste Element/Mode startet
    // (sonst übernimmt das erste Element noch die Intensität vom Ende der Vorphase).
    void ResetCurveForPhaseStart()
    {
        _phaseElapsed = 0f;
        CurrentReactionTime = ComputeReactionTime(0f);
    }

    IEnumerator RunPhaseTimer()
    {
        _phaseElapsed = 0f;
        while (_phaseElapsed < phaseDurationSec)
        {
            _phaseElapsed += Time.deltaTime;   // respektiert Pause (timeScale=0)
            CurrentReactionTime = ComputeReactionTime(_phaseElapsed);
            yield return null;
        }
        // Sofort Spawn stoppen, sobald die Phase endet — kein Fenster für Nachelemente.
        if (Spawner != null) Spawner.SetBannerPause(true);
    }

    float ComputeReactionTime(float elapsed)
    {
        float speedup = perPhaseSpeedup * _phaseIndex;   // _phaseIndex 0-basiert → P1 = 0 Abzug
        float t = 0f;
        foreach (var seg in reactionCurve)
        {
            t += seg.duration;
            if (elapsed < t)
                return Mathf.Max(minReactionTime, seg.reactionTime - speedup);
        }
        // Über die Curve hinaus: letztes Segment halten
        float last = reactionCurve.Length > 0 ? reactionCurve[reactionCurve.Length - 1].reactionTime : 1f;
        return Mathf.Max(minReactionTime, last - speedup);
    }
}
