using UnityEngine;
using System;

public class LevelUp : MonoBehaviour
{
    public event Action<int> OnLevelChanged;
    public int CurrentLevel => currentIntensity;
    public bool IsShowingPanel => false;

    // Phase (sec) = Startzeitpunkt, Intensität = Schwierigkeitsgrad 1–8
    private static readonly (float startTime, int intensity)[] Phases =
    {
        (0f,   1),
        (15f,  2),
        (30f,  3),
        (40f,  2),
        (50f,  4),
        (55f,  5),
        (60f,  3),
        (90f,  5),
        (110f, 4),
        (125f, 6),
        (150f, 5),
        (165f, 6),
        (180f, 5),
        (220f, 6),
        (250f, 5),
        (270f, 7),
        (300f, 6),
        (320f, 7),
        (360f, 6),
        (400f, 7),
        (440f, 6),
        (460f, 8),
        (500f, 7),
        (530f, 8),
    };

    // Index = Intensität (1–8), Index 0 ist Platzhalter
    private static readonly float[] ReactionTimes = { 0f, 3.0f, 2.5f, 2.0f, 1.5f, 1.2f, 0.9f, 0.7f, 0.5f };

    private float gameStartTime = -1f;
    private int currentIntensity = 1;
    private bool timerRunning = false;

    public void StartTimer()
    {
        gameStartTime = Time.time;
        currentIntensity = 1;
        timerRunning = true;
    }

    private void Update()
    {
        if (!timerRunning) return;
        int intensity = IntensityForTime(Time.time - gameStartTime);
        if (intensity == currentIntensity) return;
        currentIntensity = intensity;
        OnLevelChanged?.Invoke(currentIntensity);
    }

    private int IntensityForTime(float elapsed)
    {
        int result = Phases[0].intensity;
        for (int i = 1; i < Phases.Length; i++)
        {
            if (elapsed >= Phases[i].startTime) result = Phases[i].intensity;
            else break;
        }
        return result;
    }

    public float GetCurrentReactionTime(float fallback = 3f) =>
        currentIntensity >= 1 && currentIntensity <= 8 ? ReactionTimes[currentIntensity] : fallback;
}
