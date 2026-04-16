using UnityEngine;
using System;

public class LevelUp : MonoBehaviour
{
    public event Action<int> OnLevelChanged;
    public int CurrentLevel => currentLevel;
    public bool IsShowingPanel => false;

    private int currentLevel = 1;

    public int GetLevelForScore(int score)
    {
        if (score >= 500) return 10;
        if (score >= 400) return 9;
        if (score >= 350) return 8;
        if (score >= 300) return 7;
        if (score >= 250) return 6;
        if (score >= 200) return 5;
        if (score >= 150) return 4;
        if (score >= 100) return 3;
        if (score >= 50) return 2;
        return 1;
    }

    public float GetReactionTimeForScore(int score, float defaultTime)
    {
        if (score >= 500) return 0.5f;      // Level 10
        if (score >= 400) return 0.6f;      // Level 9
        if (score >= 350) return 0.7f;      // Level 8
        if (score >= 300) return 0.8f;      // Level 7
        if (score >= 250) return 0.9f;      // Level 6
        if (score >= 200) return 1.0f;      // Level 5
        if (score >= 150) return 1.5f;      // Level 4
        if (score >= 100) return 2.0f;      // Level 3
        if (score >= 50) return 2.5f;       // Level 2
        return defaultTime;                 // Level 1
    }

    public bool TryTriggerLevelUp(int score)
    {
        int level = GetLevelForScore(score);

        if (level > currentLevel)
        {
            currentLevel = level;
            OnLevelChanged?.Invoke(currentLevel);
            return true;
        }

        return false;
    }
}
