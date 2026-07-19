using UnityEngine;

// Temporär zur Diagnose des "Fairy-Flügel werden immer schneller"-Bugs — loggt Time.timeScale
// einmal pro echter Sekunde (unscaled, damit das Intervall selbst nicht von timeScale abhängt).
// Nach der Diagnose wieder entfernen.
public class DebugTimeScaleLogger : MonoBehaviour
{
    private float timer;

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer >= 1f)
        {
            timer = 0f;
            Debug.Log($"[DebugTimeScaleLogger] Time.timeScale = {Time.timeScale}");
        }
    }
}
