using UnityEngine;

// Lässt ein Körperteil kontinuierlich sanft nach links und rechts wippen (Z-Achse, seitliches Kippen) --
// nie stillstehend. Wiederverwendbar: einfach mehrfach draufpacken (z.B. einmal fürs Torso, einmal für
// Mask), jeweils mit eigenem Target und eigenen Werten -- Kind-Objekte (z.B. Arme am Torso) wippen dank
// der Eltern-Kind-Hierarchie automatisch mit.
public class FairySideSway : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("Maximaler Ausschlag (Grad) nach jeder Seite.")]
    [SerializeField] private float maxAngle = 4f;
    [Tooltip("Dauer eines vollen Wipp-Zyklus (links-rechts-links) in Sekunden.")]
    [SerializeField] private float period = 4f;
    [Tooltip("Wie stark die Umkehrpunkte (jeweils am weitesten links/rechts) abgerundet werden " +
             "(Anteil des Zyklus, 0-0.49) -- klein halten für fast durchgehend konstante Geschwindigkeit, " +
             "sonst wirkt's an den Extremen wie ein kurzes Stehenbleiben (siehe FairyLegIKSway).")]
    [Range(0f, 0.49f)] [SerializeField] private float cornerRoundness = 0.05f;

    private float timer;
    private float bindZ;
    private bool  bindCaptured;

    private void Update()
    {
        if (target == null) return;

        // Bind-Pose-Z EINMALIG sichern -- Rotation wird jeden Frame NEU aus (0, 0, bindZ + delta) gebaut
        // statt bindPose*delta zu multiplizieren, siehe FairyWingFlap für die ausführliche Begründung
        // (Euler-Komposition ist nicht kommutativ).
        if (!bindCaptured)
        {
            bindZ = target.localRotation.eulerAngles.z;
            bindCaptured = true;
        }

        timer += Time.deltaTime;

        // Abgerundete Dreieckswelle statt Sinus -- konstante Geschwindigkeit über den Großteil des
        // Zyklus, nur an den beiden Umkehrpunkten (ganz links/ganz rechts) kurz weich abgerundet.
        float cycle = timer / Mathf.Max(0.0001f, period);
        float rawT  = Mathf.PingPong(cycle * 2f, 1f); // 0..1..0, konstante Steigung
        float t     = RoundTriangleCorner(rawT, cornerRoundness); // 0..1, an den Enden weich abgerundet
        float angle = (t * 2f - 1f) * maxAngle; // -maxAngle..+maxAngle

        target.localRotation = Quaternion.Euler(0f, 0f, bindZ + angle);
    }

    // Rundet nur die Ecken (k=0 und k=1) einer 0..1-Dreieckswelle sanft ab -- rein positionsbasiert
    // (state-los, kein Gedächtnis zwischen Frames, kann daher nicht nachfedern/überschießen).
    private static float RoundTriangleCorner(float k, float cornerFraction)
    {
        if (cornerFraction <= 0.0001f) return k;
        cornerFraction = Mathf.Min(cornerFraction, 0.49f);

        if (k < cornerFraction)
        {
            float local = k / cornerFraction;
            return cornerFraction * (local * local * (2f - local));
        }
        if (k > 1f - cornerFraction)
        {
            float local = (1f - k) / cornerFraction;
            return 1f - cornerFraction * (local * local * (2f - local));
        }
        return k;
    }
}
