using UnityEngine;

public class InfinityRunManager : MonoBehaviour
{
    public static InfinityRunManager Instance { get; private set; }

    [System.Serializable]
    public struct DifficultyLevel
    {
        [Tooltip("Ab diesem Score gilt diese Stufe")]
        public int   scoreThreshold;
        [Tooltip("Reaktionszeit in Sekunden")]
        public float reactionTime;
    }

    [Header("Intensitätsstufen (absteigend nach Score sortieren)")]
    [SerializeField] private DifficultyLevel[] levels = new DifficultyLevel[]
    {
        new DifficultyLevel { scoreThreshold = 10000, reactionTime = 0.5f },
        new DifficultyLevel { scoreThreshold =  8000, reactionTime = 0.6f },
        new DifficultyLevel { scoreThreshold =  6000, reactionTime = 0.7f },
        new DifficultyLevel { scoreThreshold =  4000, reactionTime = 0.8f },
        new DifficultyLevel { scoreThreshold =  3000, reactionTime = 0.9f },
        new DifficultyLevel { scoreThreshold =  2000, reactionTime = 1.0f },
        new DifficultyLevel { scoreThreshold =  1500, reactionTime = 1.5f },
        new DifficultyLevel { scoreThreshold =  1000, reactionTime = 2.0f },
        new DifficultyLevel { scoreThreshold =   500, reactionTime = 2.5f },
    };

    [Header("Standard (unter erstem Threshold)")]
    [SerializeField] private float defaultReactionTime = 3.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public float GetReactionTime()
    {
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;

        foreach (var level in levels)
            if (score >= level.scoreThreshold) return level.reactionTime;

        return defaultReactionTime;
    }
}
