using System;
using UnityEngine;

/// <summary>
/// Erkennt Score-Schwellen (Reaktionszeit-Sprünge) und benachrichtigt das Portal-System.
/// Feuert OnSpeedupWarning wenn eine Score-Schwelle überschritten wird.
/// </summary>
public class BackgroundIntensityWarning : MonoBehaviour
{
    public static BackgroundIntensityWarning Instance { get; private set; }

    public static event Action OnSpeedupWarning;
    public static event Action OnPhaseReset;

    // Dieselben Schwellen wie in InfinityRunManager.GetReactionTime()
    private static readonly int[] ScoreThresholds = { 500, 1000, 1500, 2000, 3000, 4000, 6000, 8000, 10000 };

    private int _lastScore = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void ResetPhase()
    {
        OnPhaseReset?.Invoke();
    }

    private void Update()
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
        if (score == _lastScore) return;

        foreach (int threshold in ScoreThresholds)
        {
            if (_lastScore < threshold && score >= threshold)
                OnSpeedupWarning?.Invoke();
        }

        _lastScore = score;
    }
}
