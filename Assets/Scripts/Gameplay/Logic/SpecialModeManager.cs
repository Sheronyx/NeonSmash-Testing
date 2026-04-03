using UnityEngine;
using System;

public class SpecialModeManager : MonoBehaviour
{
    public static SpecialModeManager Instance;

    public static event Action<SpecialMode> OnModeStarted;
    public static event Action<SpecialMode> OnModeEnded;

    private SpecialMode currentMode = SpecialMode.None;

    public SpecialMode CurrentMode => currentMode;

    private void Awake()
    {
        Instance = this;
    }

    public bool IsModeActive => currentMode != SpecialMode.None;

public void StartMode(SpecialMode mode)
{
    if (currentMode != SpecialMode.None) return;

    currentMode = mode;

    Debug.Log($"🚀 Mode START: {mode}");

    // 🔥 NEU: alle Activation Orbs löschen
    var spawner = FindFirstObjectByType<MixedPointSpawner>();
    if (spawner != null)
    {
        spawner.ClearAllActivationOrbs();
    }

    OnModeStarted?.Invoke(mode);
}

    public void EndCurrentMode()
    {
        if (currentMode == SpecialMode.None) return;

        Debug.Log($"🛑 Mode END: {currentMode}");

        OnModeEnded?.Invoke(currentMode);

        currentMode = SpecialMode.None;
    }
}