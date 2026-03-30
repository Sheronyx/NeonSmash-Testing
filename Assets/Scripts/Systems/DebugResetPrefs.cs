using UnityEngine;
using UnityEngine.InputSystem;

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
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("Alle PlayerPrefs gelöscht via neuem Input System.");
    }

    void OnDestroy()
    {
        resetAction.Disable();
    }
}
