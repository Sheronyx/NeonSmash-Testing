using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// Erkennt bevorstehende Intensitäts-Sprünge und benachrichtigt andere Systeme (Portal).
/// Kein visueller Effekt auf den Hintergrund.
/// </summary>
public class BackgroundIntensityWarning : MonoBehaviour
{
    public static BackgroundIntensityWarning Instance { get; private set; }

    /// <summary>Gefeuert wenn ein Speedup in warningLeadTime Sekunden kommt.</summary>
    public static event Action OnSpeedupWarning;
    /// <summary>Gefeuert beim Phasen-Reset (für andere Systeme zum Zurücksetzen).</summary>
    public static event Action OnPhaseReset;

    [SerializeField] private float warningLeadTime = 2f;

    private bool _warningTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void ResetPhase()
    {
        _warningTriggered = false;
        OnPhaseReset?.Invoke();
    }

    private void Update()
    {
        var pm = PhaseManager.Instance;
        if (pm == null) return;

        float secs = pm.GetSecondsUntilNextSpeedup();

        if (!_warningTriggered && secs <= warningLeadTime && secs > 0.1f)
        {
            _warningTriggered = true;
            OnSpeedupWarning?.Invoke();
        }

        if (_warningTriggered && secs > warningLeadTime * 1.5f)
            _warningTriggered = false;
    }
}
