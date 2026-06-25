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

    [Header("Phasenende / Auslaufen")]
    [Tooltip("Max. Zeit (s), die auf das natürliche Auslaufen der Restelemente gewartet wird, " +
             "bevor zur Sicherheit hart geräumt wird.")]
    [SerializeField] private float maxDrainSec = 6f;

    [Header("Entscheidung")]
    [Tooltip("Mindest-Wartezeit (s) NACH dem Auslaufen der Restelemente, bevor die Entscheidungs-UI erscheint.")]
    [SerializeField] private float decisionPreDelaySec = 1f;
    [Tooltip("Wartezeit (realtime) fürs Ausblenden der Entscheidungs-UI, bevor der Orb kommt.")]
    [SerializeField] private float decisionFadeOutSec = 0.3f;

    // ----- Events für UI -----
    /// <summary>(phaseNumber 1-basiert, totalPhases)</summary>
    public static event Action<int, int> OnPhaseBanner;
    /// <summary>Entscheidungs-UI einblenden (Special-Mode wählen).</summary>
    public static event Action OnDecisionRequested;
    /// <summary>Entscheidungs-UI ausblenden (Wahl getroffen).</summary>
    public static event Action OnDecisionClosed;
    public static event Action OnGameWon;

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

            if (def.decisionBefore)
                yield return Co_Decision(def);

            if (def.kind == PhaseKind.Play)
            {
                yield return Co_PhaseBanner(_phaseIndex + 1);   // Banner nur vor Spielphasen
                yield return Co_PlayPhase(def);
            }
            else
            {
                yield return Co_SpecialPhase(def);              // Special: Entscheidung + Orb sind der Übergang
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
        Spawner.SetBannerPause(true);   // normaler Spawner: kein Nachspawn

        // 2) Warten, bis der Spieler alle übrigen Elemente normal beendet hat
        //    (Treffer, Timeout oder vom Bildschirm gelaufen). Guard als Sicherheitsnetz.
        float guard = 0f;
        while (Spawner.HasActiveGameplayPoints() && guard < maxDrainSec)
        {
            guard += Time.deltaTime;
            yield return null;
        }

        // 3) Jetzt den Special Mode wirklich beenden (Portal/Scoring waren bis hier aktiv).
        if (special)
        {
            if (_chosenMode == SpecialMode.Gravity)       GravityModeSystem.Instance?.StopMode();
            else if (_chosenMode == SpecialMode.Fountain) FountainModeSystem.Instance?.StopMode();
            else                                          SpecialModeManager.Instance.EndCurrentMode();
        }

        // 4) Sicherheitsnetz: falls der Guard zuschlug, Rest sauber + positiv räumen.
        if (Spawner.HasActiveGameplayPoints())
            Spawner.PositiveClearAll();
    }

    /// <summary>Bei Game Over aufrufen → stoppt den Phasen-Ablauf.</summary>
    public void StopRun()
    {
        if (!_running) return;
        _running = false;
        StopAllCoroutines();
    }

    // ----------------------------------------------------------------- Entscheidung
    IEnumerator Co_Decision(PhaseDef def)
    {
        // Keine Entscheidungs-UI in der Szene? → vorläufig automatisch abwechselnd wählen (Test).
        if (OnDecisionRequested == null)
        {
            _chosenMode = (_chosenMode == SpecialMode.Gravity) ? SpecialMode.Fountain : SpecialMode.Gravity;
            yield break;
        }

        // Mindest-Wartezeit, nachdem das letzte Element der Vorphase ausgelaufen ist,
        // bevor die Entscheidungs-UI erscheint (ruhiger Übergang).
        if (decisionPreDelaySec > 0f)
            yield return new WaitForSecondsRealtime(decisionPreDelaySec);

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
    IEnumerator Co_PhaseBanner(int phaseNumber)
    {
        if (Spawner != null) Spawner.SetBannerPause(true);
        OnPhaseBanner?.Invoke(phaseNumber, phases.Length);
        yield return new WaitForSeconds(bannerDurationSec);
        if (Spawner != null) Spawner.SetBannerPause(false);
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
