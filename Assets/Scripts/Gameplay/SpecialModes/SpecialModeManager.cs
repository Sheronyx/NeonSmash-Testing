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
    if (PeekABooSystem.IsActive) return;   // Während Peek-a-boo kein Special Mode

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

        var ended = currentMode;
        currentMode = SpecialMode.None;
        Debug.Log($"🛑 Mode END: {ended}");
        OnModeEnded?.Invoke(ended);
    }

    // Zentrale Hit/Miss-Logik für alle Special Modes
    public static void RegisterSpecialHit(int extraMultiplier = 1, Vector3 position = default, Material textMaterial = null)
    {
        int amount = ScoreManager.Instance?.AddPointsFromHit(10, extraMultiplier) ?? 0;
        // Special-Mode-Elemente haben keine Farbe → neutraler Treffer, kein Kombo-Aufbau
        MixedPointSpawner.Instance?.SpawnSpecialFloatingScore(amount, position, textMaterial);
    }

    public static void RegisterSpecialMiss()
    {
        // Special-Mode-Elemente brechen das Farb-Kombo nicht (getrennte Systeme)
    }
}