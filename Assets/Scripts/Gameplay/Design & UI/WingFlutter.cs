using UnityEngine;

public class WingFlutter : MonoBehaviour
{
    [Header("Flutter Settings")]
    [SerializeField] private Transform wingLeft;
    [SerializeField] private Transform wingRight;
    [SerializeField] private float flutterSpeed = 2f;  // Wie schnell flattern (gleiche Frequenz wie Float)
    [SerializeField] private float flutterAngle = 15f; // Max Rotationswinkel (Grad)
    [SerializeField] private float phaseOffset = 0f;   // Phasenversatz (0-1)

    private Quaternion wingLeftBase;
    private Quaternion wingRightBase;

    private void Start()
    {
        // Basis-Rotation speichern
        wingLeftBase = wingLeft.localRotation;
        wingRightBase = wingRight.localRotation;
    }

    private void Update()
    {
        // Sine-Welle für natürliches Flattern
        float flutter = Mathf.Sin((Time.time + phaseOffset) * flutterSpeed * Mathf.PI * 2f) * flutterAngle;

        // Wings gegensätzlich rotieren (linke Wing: +flutter, rechte Wing: -flutter)
        wingLeft.localRotation = wingLeftBase * Quaternion.Euler(0, 0, flutter);
        wingRight.localRotation = wingRightBase * Quaternion.Euler(0, 0, -flutter);
    }
}
