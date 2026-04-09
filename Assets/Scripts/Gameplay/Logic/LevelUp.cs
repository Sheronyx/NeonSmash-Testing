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
        if (score >= 700) return 12;
        if (score >= 600) return 11;
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
        if (score >= 700) return 0.3f;
        if (score >= 600) return 0.4f;
        if (score >= 500) return 0.5f;
        if (score >= 400) return 0.6f;
        if (score >= 350) return 0.7f;
        if (score >= 300) return 0.8f;
        if (score >= 250) return 0.9f;
        if (score >= 200) return 1.0f;
        if (score >= 150) return 1.5f;
        if (score >= 100) return 2.0f;
        if (score >= 50) return 2.5f;
        return defaultTime;
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
