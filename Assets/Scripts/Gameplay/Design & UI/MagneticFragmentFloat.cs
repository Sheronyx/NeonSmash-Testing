using UnityEngine;

// Sanfter, KONTINUIERLICHER Schwebe- + Puls- + Wackel-Effekt für "Meteoritenstück"-artige Fragmente,
// die per Magnetfeld an einem Energiekern hängen. Nutzt Perlin-Noise statt diskreter Zielpunkte —
// dadurch nie ein fester "angekommen"-Zustand, der Effekt bleibt garantiert nie stehen. Position,
// Größe UND Rotation schwanken unabhängig voneinander. Jede Instanz startet mit eigenem Zufalls-
// Offset im Noise, damit mehrere Stücke nicht synchron laufen.
public class MagneticFragmentFloat : MonoBehaviour
{
    [Header("Position (Schweben)")]
    [SerializeField] private float moveRadius = 0.15f;
    [SerializeField] private float moveSpeed  = 0.5f;

    [Header("Größe (Pulsieren)")]
    [Tooltip("Wie stark die Größe schwankt (0.08 = zwischen 92% und 108% der Ausgangsgröße).")]
    [SerializeField] private float scaleVariance = 0.08f;
    [SerializeField] private float scaleSpeed    = 0.6f;

    [Header("Rotation (Wackeln)")]
    [Tooltip("Maximaler Ausschlag in Grad, in beide Richtungen von der Ausgangsrotation.")]
    [SerializeField] private float rotationAmount = 6f;
    [SerializeField] private float rotationSpeed  = 0.4f;

    private Vector3 basePos;
    private Vector3 baseScale;
    private Quaternion baseRotation;

    private float seedX, seedY, seedScale, seedRot;

    private void Start()
    {
        basePos      = transform.position;
        baseScale    = transform.localScale;
        baseRotation = transform.rotation;

        // Weit auseinanderliegende Noise-Offsets pro Kanal — Perlin-Noise liefert bei denselben
        // Koordinaten immer denselben Wert, ohne Versatz würden alle Kanäle (und mehrere Instanzen)
        // exakt gleich schwanken.
        seedX     = Random.Range(0f, 1000f);
        seedY     = Random.Range(1000f, 2000f);
        seedScale = Random.Range(2000f, 3000f);
        seedRot   = Random.Range(3000f, 4000f);
    }

    private void Update()
    {
        float offsetX = (Mathf.PerlinNoise(seedX, Time.time * moveSpeed) - 0.5f) * 2f * moveRadius;
        float offsetY = (Mathf.PerlinNoise(seedY, Time.time * moveSpeed) - 0.5f) * 2f * moveRadius;
        transform.position = basePos + new Vector3(offsetX, offsetY, 0f);

        float scalePercent = 1f + (Mathf.PerlinNoise(seedScale, Time.time * scaleSpeed) - 0.5f) * 2f * scaleVariance;
        transform.localScale = baseScale * scalePercent;

        float rotOffset = (Mathf.PerlinNoise(seedRot, Time.time * rotationSpeed) - 0.5f) * 2f * rotationAmount;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, rotOffset);
    }
}
