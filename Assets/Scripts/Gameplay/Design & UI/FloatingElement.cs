using UnityEngine;

public class FloatingElement : MonoBehaviour
{
    [Header("Floating Path")]
    [SerializeField] private float floatSpeed = 2f;  // Geschwindigkeit der Bewegung
    [SerializeField] private float amplitudeX = 1.5f;  // Wie weit links/rechts
    [SerializeField] private float amplitudeY = 1f;    // Wie weit oben/unten
    [SerializeField] private float yFrequencyMultiplier = 0.8f;  // Y-Frequenz relativ zu X (für Ellipse)

    [Header("Tilting")]
    [SerializeField] private float maxTiltAngle = 15f;  // Max Kipp-Winkel
    [SerializeField] private bool enableAutoTilt = true;  // Automatisches Kippen basierend auf Richtung

    private Vector3 basePosition;
    private Vector3 previousPosition;

    private void Start()
    {
        basePosition = transform.position;
        previousPosition = transform.position;
    }

    private void Update()
    {
        // Elliptische Bewegung: X und Y mit unterschiedlichen Frequenzen
        float x = Mathf.Sin(Time.time * floatSpeed * Mathf.PI * 2f) * amplitudeX;
        float y = Mathf.Cos(Time.time * floatSpeed * yFrequencyMultiplier * Mathf.PI * 2f) * amplitudeY;

        Vector3 newPosition = basePosition + new Vector3(x, y, 0);
        transform.position = newPosition;

        // Automatisches Kippen basierend auf Bewegungsrichtung
        if (enableAutoTilt)
        {
            Vector3 velocity = newPosition - previousPosition;
            if (velocity.magnitude > 0.001f)
            {
                // Berechne Winkel basierend auf Bewegungsrichtung
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

                // Konvertiere zu Tilt-Winkel um Z-Achse (-90° zu +90°)
                float tiltAngle = Mathf.Clamp(angle, -maxTiltAngle, maxTiltAngle);

                transform.rotation = Quaternion.Euler(0, 0, -tiltAngle);
            }
        }

        previousPosition = newPosition;
    }

    // Debug: Zeige die Bewegungsbahn im Scene View
    private void OnDrawGizmosSelected()
    {
        Vector3 pos = Application.isPlaying ? basePosition : transform.position;

        // Zeichne Ellipse
        Gizmos.color = Color.yellow;
        for (int i = 0; i < 20; i++)
        {
            float t1 = (i / 20f) * Mathf.PI * 2f;
            float t2 = ((i + 1) / 20f) * Mathf.PI * 2f;

            float x1 = Mathf.Sin(t1) * amplitudeX;
            float y1 = Mathf.Cos(t1 * yFrequencyMultiplier) * amplitudeY;

            float x2 = Mathf.Sin(t2) * amplitudeX;
            float y2 = Mathf.Cos(t2 * yFrequencyMultiplier) * amplitudeY;

            Gizmos.DrawLine(pos + new Vector3(x1, y1, 0), pos + new Vector3(x2, y2, 0));
        }
    }
}
