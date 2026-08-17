using UnityEngine;

// Lässt das Objekt (z.B. der Fee-Körper) dauerhaft leicht pulsieren -- Sinus auf der Basis-Scale, Stärke
// und Geschwindigkeit im Inspector einstellbar.
public class BodyPulse : MonoBehaviour
{
    [Tooltip("Wie stark die Scale um die Basisgröße herum schwankt (0.05 = ±5%).")]
    [SerializeField] private float pulseAmount = 0.05f;
    [Tooltip("Wie schnell pulsiert wird.")]
    [SerializeField] private float pulseSpeed = 2f;

    private Vector3 baseScale;
    private float pulseTimer;

    private void Awake()
    {
        baseScale = transform.localScale;
        // Zufällige Start-Phase, damit mehrere Body-Instanzen nicht exakt synchron pulsieren.
        pulseTimer = Random.Range(0f, 10f);
    }

    private void Update()
    {
        pulseTimer += Time.deltaTime;
        float pulse = 1f + Mathf.Sin(pulseTimer * Mathf.PI * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
