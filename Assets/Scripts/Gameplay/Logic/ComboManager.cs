using System;
using UnityEngine;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }
    public static event Action<int> OnComboChanged;

    public int ComboCount    { get; private set; }
    public int Multiplier    => Mathf.Clamp(ComboCount, 1, 10);
    public bool IsMaxCombo   => ComboCount >= 10;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Aufgerufen bei jedem erfolgreichen Tap/Swipe-Hit
    public void RegisterHit()
    {
        ComboCount++;
        OnComboChanged?.Invoke(ComboCount);
    }

    // Aufgerufen wenn ein Point ausläuft (Timeout)
    public void RegisterMiss()
    {
        if (ComboCount == 0) return;
        ComboCount = 0;
        OnComboChanged?.Invoke(ComboCount);
    }

    public void ResetCombo()
    {
        ComboCount = 0;
        OnComboChanged?.Invoke(0);
    }
}
