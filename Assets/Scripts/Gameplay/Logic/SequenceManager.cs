using System;
using System.Collections;
using UnityEngine;

public class SequenceManager : MonoBehaviour
{
    public static SequenceManager Instance { get; private set; }

    [SerializeField] private SequenceDefinition[] activeSequences = new SequenceDefinition[3];

    // (sequenceIndex, currentStep)
    public static event Action<int, int> OnProgressChanged;
    // sequenceIndex — Sequenz abgeschlossen, Progress bereits auf 0 zurückgesetzt
    public static event Action<int> OnSequenceCompleted;
    // Alle Sequenzen zurückgesetzt (z.B. nach Lebensverlust)
    public static event Action OnAllProgressReset;

    // true wenn beim letzten RegisterHit-Aufruf eine Sequenz mit bonusDeliveries abgeschlossen wurde
    // → MixedPointSpawner soll dann weder regulären Score noch FloatingScore zeigen
    public bool LastHitSuppressedScore { get; private set; }

    // true wenn beim letzten RegisterHit-Aufruf ein Kombo-Effekt gestartet wurde
    // → MixedPointSpawner verlängert den Spawn-Delay um effectPreDelay
    public bool LastHitTriggeredEffect { get; private set; }
    public float EffectPreDelay => effectPreDelay;

    [Header("Kombo-Effekt")]
    [Tooltip("Pause vor dem Freeze (s) — gibt VFX Graphs Zeit sich aufzulösen, bevor timeScale=0 gesetzt wird.")]
    [SerializeField] private float effectPreDelay = 0.15f;

    private int[] _progress;
    private bool  _effectPlaying;

    private void Awake()
    {
        Instance = this;
        _progress = new int[activeSequences.Length];
    }

    public int SequenceCount => activeSequences.Length;

    public SequenceDefinition GetSequence(int index) =>
        index >= 0 && index < activeSequences.Length ? activeSequences[index] : null;

    public int GetProgress(int index) =>
        _progress != null && index >= 0 && index < _progress.Length ? _progress[index] : 0;

    // Gibt den Gesamt-Bonus zurück den dieser Treffer durch abgeschlossene Sequenzen erzeugt.
    // Wenn bonusDeliveries gesetzt sind, wird 0 zurückgegeben — der Bonus wird vom Effekt verteilt.
    public int RegisterHit(PointColor color)
    {
        int totalBonus = 0;
        LastHitSuppressedScore = false;
        LastHitTriggeredEffect = false;
        for (int i = 0; i < activeSequences.Length; i++)
        {
            var seq = activeSequences[i];
            if (seq == null || seq.steps == null || seq.steps.Length == 0) continue;

            if (color == seq.steps[_progress[i]])
            {
                _progress[i]++;
                if (_progress[i] >= seq.steps.Length)
                {
                    bool hasDeliveries = seq.comboEffectPrefab != null
                                      && seq.bonusDeliveries != null
                                      && seq.bonusDeliveries.Length > 0;
                    if (hasDeliveries)
                        LastHitSuppressedScore = true;
                    else
                        totalBonus += seq.bonusPoints;

                    _progress[i] = 0;
                    OnSequenceCompleted?.Invoke(i);

                    if (!_effectPlaying && seq.comboEffectPrefab != null)
                    {
                        LastHitTriggeredEffect = true;
                        StartCoroutine(Co_PlayEffect(seq));
                    }
                }
                OnProgressChanged?.Invoke(i, _progress[i]);
            }
            else if (_progress[i] > 0)
            {
                _progress[i] = color == seq.steps[0] ? 1 : 0;
                OnProgressChanged?.Invoke(i, _progress[i]);
            }
        }
        return totalBonus;
    }

    private IEnumerator Co_PlayEffect(SequenceDefinition seq)
    {
        _effectPlaying = true;

        // VFX Graphs unterstützen kein useUnscaledTime — kurze Pause gibt ihnen Zeit sich aufzulösen
        yield return new WaitForSecondsRealtime(effectPreDelay);

        // Alle aktuell laufenden Particle Systems in der Szene auf Unscaled Time setzen,
        // damit Explosionen und Trails während des Freeze weiterlaufen
        var scenePS = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        var toRestore = new System.Collections.Generic.List<ParticleSystem>();
        foreach (var ps in scenePS)
        {
            var m = ps.main;
            if (!m.useUnscaledTime)
            {
                m.useUnscaledTime = true;
                toRestore.Add(ps);
            }
        }

        var effect = Instantiate(seq.comboEffectPrefab);

        // Effekt-Prefab PS ebenfalls sicherstellen (falls noch nicht über scenePS erfasst)
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            m.useUnscaledTime = true;
        }

        Time.timeScale = 0f;

        bool hasDeliveries = seq.bonusDeliveries != null && seq.bonusDeliveries.Length > 0;
        if (hasDeliveries)
            StartCoroutine(Co_DeliverBonus(seq));

        yield return new WaitForSecondsRealtime(seq.effectDuration);
        Time.timeScale = 1f;

        // Szenen-PS zurücksetzen
        foreach (var ps in toRestore)
            if (ps != null) { var m = ps.main; m.useUnscaledTime = false; }

        if (effect != null) Destroy(effect);
        _effectPlaying = false;
    }

    // Gibt den Bonus schrittweise aus — läuft parallel zur Effekt-Animation auf Realtime.
    private IEnumerator Co_DeliverBonus(SequenceDefinition seq)
    {
        float startTime = Time.realtimeSinceStartup;

        foreach (var d in seq.bonusDeliveries)
        {
            float waitUntil = startTime + d.delay;
            float remaining = waitUntil - Time.realtimeSinceStartup;
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

            int amount = Mathf.RoundToInt(seq.bonusPoints * d.fraction);
            if (amount <= 0) continue;

            ScoreManager.Instance?.AddPoints(amount);
            MixedPointSpawner.Instance?.SpawnBonusFloatingScore(amount, d.viewportPosition, d.textScale, d.textColor);
        }
    }

    public void ResetAllProgress()
    {
        if (_progress == null) return;
        for (int i = 0; i < _progress.Length; i++) _progress[i] = 0;
        OnAllProgressReset?.Invoke();
    }
}
