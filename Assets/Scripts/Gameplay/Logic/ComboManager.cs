using System;
using UnityEngine;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }
    public static event Action<int> OnComboChanged;

    public int ComboCount    { get; private set; }
    // ComboCount 0 → x1.0, ComboCount 1 → x2.0, ..., ComboCount 9 → x10.0 (max)
    public int Multiplier    => Mathf.Min(ComboCount + 1, 10);
    public bool IsMaxCombo   => ComboCount >= 9;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Aufgerufen bei jedem erfolgreichen Tap/Swipe-Hit
    public void RegisterHit()
    {
        int next = Mathf.Min(ComboCount + 1, 9); // Multiplier cap: 9+1 = x10.0
        if (next == ComboCount) return;           // schon am Maximum, kein Event nötig
        ComboCount = next;
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
