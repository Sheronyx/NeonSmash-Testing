#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// Nur Editor/Development-Builds - siehe RewardDebugHelper.cs für dasselbe Muster. Ohne diesen Guard
// haette ein Spieler mit physischer Tastatur (Desktop/WebGL-Build, o.ae.) per Strg+R versehentlich
// ALLE PlayerPrefs (kompletten Spielstand) loeschen koennen (Bug gefunden 2026-08-22, Night Shift).
public class DebugResetPrefs : MonoBehaviour
{
    private InputAction resetAction;

    void Awake()
    {
        resetAction = new InputAction(binding: "<Keyboard>/r");
        resetAction.performed += ctx => OnResetPressed();
        resetAction.Enable();
    }

    private void OnResetPressed()
    {
        if (!UnityEngine.InputSystem.Keyboard.current.ctrlKey.isPressed) return;
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("Alle PlayerPrefs gelöscht via neuem Input System.");
    }

    void OnDestroy()
    {
        resetAction.Disable();
    }
}
#endif
