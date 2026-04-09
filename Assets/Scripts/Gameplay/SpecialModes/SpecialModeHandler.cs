using UnityEngine;

public class SpecialModeHandler : MonoBehaviour
{
    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleMode;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleMode;
    }

    private void HandleMode(SpecialMode mode)
    {
        if (mode == SpecialMode.Fountain)
        {
            var system = FindFirstObjectByType<FountainModeSystem>();
            system?.Activate();
        }
    }
}