using System;
using UnityEngine;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    // Liefert den aktuellen Streak (0 = kein Streak, 5+ = Kombo aktiv).
    // ComboDisplay abonniert dieses Event.
    public static event Action<int> OnComboChanged;

    private const int ComboThreshold = 5;

    private PointColor? _lastColor;
    private int         _streak;

    /// <summary>True sobald 5x dieselbe Farbe hintereinander getroffen wurde.</summary>
    public bool IsComboActive { get; private set; }

    /// <summary>5 im Kombo-Modus, sonst 1.</summary>
    public int Multiplier => IsComboActive ? 5 : 1;

    /// <summary>Aktueller Streak (für UI). Entspricht der Streak-Zählung.</summary>
    public int ComboCount => _streak;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Nach jedem erfolgreichen Tap/Swipe aufrufen.</summary>
    public void RegisterHit(PointColor color)
    {
        if (_lastColor == color)
        {
            _streak++;
        }
        else
        {
            _streak    = 1;
            _lastColor = color;
            SetActive(false);
        }

        if (_streak >= ComboThreshold)
            SetActive(true);

        OnComboChanged?.Invoke(_streak);
    }

    /// <summary>Bei Timeout oder Thunderpoint-Treffer aufrufen.</summary>
    public void RegisterMiss() => BreakCombo();

    public void ResetCombo() => BreakCombo();

    public void BreakCombo()
    {
        _streak    = 0;
        _lastColor = null;
        SetActive(false);
        OnComboChanged?.Invoke(0);
    }

    private void SetActive(bool active)
    {
        IsComboActive = active;
    }
}
