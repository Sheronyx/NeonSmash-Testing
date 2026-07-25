using UnityEngine;

// Wie MagneticFragmentFloat (Schweben + Pulsieren + Wackeln, alles über kontinuierliches Perlin-
// Noise, nie stehenbleibend), zusätzlich umkreist das Fragment dabei im Uhrzeigersinn ein Zentrum
// (z.B. den Energiekern). Start-Radius/-Winkel werden automatisch aus der Ausgangsposition relativ
// zum Zentrum übernommen — die aktuelle Anordnung der Stücke im Editor bleibt erhalten, nur die
// Umkreisung kommt dazu. Jede Instanz startet mit eigenem Zufalls-Offset im Noise, damit mehrere
// Stücke nicht synchron schweben/pulsieren/wackeln.
public class MagneticFragmentOrbit : MonoBehaviour
{
    [Header("Umkreisung")]
    [SerializeField] private Transform center;
    [Tooltip("Grad pro Sekunde im Uhrzeigersinn.")]
    [SerializeField] private float orbitSpeed = 15f;

    [Header("Position (Schweben, zusätzlich zur Umkreisung)")]
    [SerializeField] private float moveRadius = 0.1f;
    [SerializeField] private float moveSpeed  = 0.5f;

    [Header("Größe (Pulsieren)")]
    [Tooltip("Wie stark die Größe schwankt (0.08 = zwischen 92% und 108% der Ausgangsgröße).")]
    [SerializeField] private float scaleVariance = 0.08f;
    [SerializeField] private float scaleSpeed    = 0.6f;

    [Header("Rotation (Wackeln, zusätzlich zur Bahnbewegung)")]
    [Tooltip("Maximaler Ausschlag in Grad, in beide Richtungen von der Ausgangsrotation.")]
    [SerializeField] private float rotationAmount = 6f;
    [SerializeField] private float rotationSpeed  = 0.4f;

    private float orbitRadius;
    private float orbitAngleDeg;

    private Vector3 baseScale;
    private Quaternion baseRotation;

    private float seedX, seedY, seedScale, seedRot;

    private void Start()
    {
        if (center == null)
        {
            Debug.LogWarning($"[MagneticFragmentOrbit] Kein 'Center' zugewiesen auf {name} — Umkreisung deaktiviert.");
            enabled = false;
            return;
        }

        Vector3 offset = transform.position - center.position;
        orbitRadius   = offset.magnitude;
        orbitAngleDeg = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

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
        // Uhrzeigersinn = abnehmender Winkel (Standard-Mathe-Konvention: der Winkel wächst
        // gegen den Uhrzeigersinn, also muss er für "im Uhrzeigersinn" sinken).
        orbitAngleDeg -= orbitSpeed * Time.deltaTime;
        float rad = orbitAngleDeg * Mathf.Deg2Rad;
        Vector3 orbitPos = center.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;

        float offsetX = (Mathf.PerlinNoise(seedX, Time.time * moveSpeed) - 0.5f) * 2f * moveRadius;
        float offsetY = (Mathf.PerlinNoise(seedY, Time.time * moveSpeed) - 0.5f) * 2f * moveRadius;
        transform.position = orbitPos + new Vector3(offsetX, offsetY, 0f);

        float scalePercent = 1f + (Mathf.PerlinNoise(seedScale, Time.time * scaleSpeed) - 0.5f) * 2f * scaleVariance;
        transform.localScale = baseScale * scalePercent;

        float rotOffset = (Mathf.PerlinNoise(seedRot, Time.time * rotationSpeed) - 0.5f) * 2f * rotationAmount;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, rotOffset);
    }
}
