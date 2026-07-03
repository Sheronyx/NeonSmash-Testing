using UnityEngine;

/// <summary>
/// Steuert den vereinfachten Infinity-Mode-Run.
/// Keine Phasen, keine Special Modes — nur ein durchgängiger Run mit Score-basierter Reaktionszeit.
/// Ein Fehler = sofortiges Game Over.
/// </summary>
public class InfinityRunManager : MonoBehaviour
{
    public static InfinityRunManager Instance { get; private set; }

    [Header("Reaktionszeit (Standard vor Score 500)")]
    [SerializeField] private float defaultReactionTime = 3.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>
    /// Gibt die aktuelle Reaktionszeit basierend auf dem Score zurück.
    /// Hinweis: Score 600 im Original war wahrscheinlich ein Tippfehler → hier 6000.
    /// </summary>
    public float GetReactionTime()
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;

        if (score >= 10000) return 0.5f;   // Level 10
        if (score >= 8000)  return 0.6f;   // Level 9
        if (score >= 6000)  return 0.7f;   // Level 8
        if (score >= 4000)  return 0.8f;   // Level 7
        if (score >= 3000)  return 0.9f;   // Level 6
        if (score >= 2000)  return 1.0f;   // Level 5
        if (score >= 1500)  return 1.5f;   // Level 4
        if (score >= 1000)  return 2.0f;   // Level 3
        if (score >= 500)   return 2.5f;   // Level 2
        return defaultReactionTime;         // Level 1
    }
}
