using System;
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

    private int[] _progress;

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
    public int RegisterHit(PointColor color)
    {
        int totalBonus = 0;
        for (int i = 0; i < activeSequences.Length; i++)
        {
            var seq = activeSequences[i];
            if (seq == null || seq.steps == null || seq.steps.Length == 0) continue;

            if (color == seq.steps[_progress[i]])
            {
                _progress[i]++;
                if (_progress[i] >= seq.steps.Length)
                {
                    totalBonus += seq.bonusPoints;
                    _progress[i] = 0;
                    OnSequenceCompleted?.Invoke(i);
                }
                OnProgressChanged?.Invoke(i, _progress[i]);
            }
            else if (_progress[i] > 0)
            {
                // Falsche Farbe während laufender Sequenz → Reset
                // Prüfen ob die falsche Farbe zufällig den ersten Schritt trifft → direkt neu starten
                _progress[i] = color == seq.steps[0] ? 1 : 0;
                OnProgressChanged?.Invoke(i, _progress[i]);
            }
        }
        return totalBonus;
    }

    public void ResetAllProgress()
    {
        if (_progress == null) return;
        for (int i = 0; i < _progress.Length; i++) _progress[i] = 0;
        OnAllProgressReset?.Invoke();
    }
}
