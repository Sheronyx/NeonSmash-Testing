using System.Collections.Generic;
using UnityEngine;

// Fallende Blätter für eine Parallax-Ebene (Mid/Near des Tropical-Jungle-Backgrounds).
// Jedes Blatt fällt von oben nach unten und pendelt dabei sichelförmig nach links/rechts
// (Sinus), wobei es sich sanft in Schwungrichtung neigt. Verlässt ein Blatt unten den
// Bildschirm, wird es oben mit neuen Zufallswerten wieder eingesetzt — Endlos-Pool.
//
// Parallax: Mid und Near nutzen dasselbe Skript, nur mit unterschiedlicher fallSpeed
// (Near schneller) bzw. Anzahl/Amplitude.
public class FallingLeavesLayer : MonoBehaviour
{
    [Header("Blätter")]
    [Tooltip("Eines oder mehrere Leaf-Prefabs (pro Blatt zufällig gewählt).")]
    [SerializeField] private GameObject[] leafPrefabs;
    [Tooltip("Anzahl gleichzeitig fallender Blätter.")]
    [SerializeField] private int leafCount = 12;

    [Header("Fall (oben → unten)")]
    [Tooltip("Fallgeschwindigkeit (Units/Sek). Near > Mid für Parallax.")]
    [SerializeField] private float fallSpeed = 2f;
    [SerializeField] private float fallSpeedVariance = 0.6f;

    [Header("Sichel-Pendeln (links ↔ rechts)")]
    [Tooltip("Horizontale Auslenkung der Pendelbahn (Units).")]
    [SerializeField] private float swayAmplitude = 1.2f;
    [SerializeField] private float swayAmplitudeVariance = 0.5f;
    [Tooltip("Dauer einer vollen Pendelschwingung (Sek).")]
    [SerializeField] private float swayPeriod = 2.5f;
    [SerializeField] private float swayPeriodVariance = 0.8f;

    [Header("Neigung (in Schwungrichtung)")]
    [Tooltip("Grund-Ausrichtung des Blatts (Grad), wird zur Neigung addiert. 180 = umgedreht.")]
    [SerializeField] private float baseRotation = 180f;
    [Tooltip("Max. Kippwinkel des Blatts (Grad). Negativ = Kippung spiegeln.")]
    [SerializeField] private float tiltDegrees = 20f;
    [Tooltip("Zusätzliche langsame Eigenrotation (Grad/Sek), 0 = aus.")]
    [SerializeField] private float spinSpeed = 0f;

    [Header("Zufalls-Skalierung der Blätter")]
    [SerializeField] private float minScale = 0.9f;
    [SerializeField] private float maxScale = 1.1f;

    [Header("Recycle-Ränder (Units über/unter dem Bild)")]
    [Tooltip("Höhe über dem oberen Bildrand, wo Blätter starten/recyceln.")]
    [SerializeField] private float topMargin = 1.5f;
    [Tooltip("Tiefe unter dem unteren Bildrand, ab der recycelt wird.")]
    [SerializeField] private float bottomMargin = 1.5f;
    [Tooltip("Seitlicher Puffer, damit Pendel-Ausschläge nicht hart am Rand abschneiden.")]
    [SerializeField] private float sideMargin = 0.5f;

    private Camera cam;
    private float leftX, rightX, topY, bottomY;

    private class Leaf
    {
        public Transform t;
        public float speed;
        public float amp;
        public float omega;     // 2π / Periode
        public float phase;
        public float baseX;     // Mittelachse des Pendelns
        public float age;
        public float spin;
        public float z;
        public Vector3 scale;
    }

    private readonly List<Leaf> leaves = new();

    private void Start()
    {
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[FallingLeavesLayer] Keine Main-Camera gefunden!");
            enabled = false;
            return;
        }
        if (leafPrefabs == null || leafPrefabs.Length == 0)
        {
            Debug.LogError("[FallingLeavesLayer] Keine Leaf-Prefabs zugewiesen!");
            enabled = false;
            return;
        }

        ComputeBounds();

        for (int i = 0; i < leafCount; i++)
            leaves.Add(CreateLeaf());
    }

    private void ComputeBounds()
    {
        float z = Mathf.Abs(cam.transform.position.z);
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));
        leftX = bl.x; rightX = tr.x;
        bottomY = bl.y; topY = tr.y;
    }

    private Leaf CreateLeaf()
    {
        var prefab = leafPrefabs[Random.Range(0, leafPrefabs.Length)];
        var go = Instantiate(prefab, transform);
        var leaf = new Leaf
        {
            t = go.transform,
            z = go.transform.position.z,
            spin = spinSpeed != 0f ? Random.Range(-spinSpeed, spinSpeed) : 0f,
        };
        ResetLeaf(leaf, scatter: true);
        return leaf;
    }

    // scatter: beim Start zufällig über die ganze Höhe verteilen, sonst oben einsetzen.
    private void ResetLeaf(Leaf leaf, bool scatter)
    {
        leaf.speed = Mathf.Max(0.1f, fallSpeed + Random.Range(-fallSpeedVariance, fallSpeedVariance));
        leaf.amp   = Mathf.Max(0f, swayAmplitude + Random.Range(-swayAmplitudeVariance, swayAmplitudeVariance));
        float period = Mathf.Max(0.1f, swayPeriod + Random.Range(-swayPeriodVariance, swayPeriodVariance));
        leaf.omega = (Mathf.PI * 2f) / period;
        leaf.phase = Random.Range(0f, Mathf.PI * 2f);
        leaf.baseX = Random.Range(leftX + sideMargin, rightX - sideMargin);
        leaf.age   = 0f;

        float s = Random.Range(minScale, maxScale);
        leaf.scale = Vector3.one * s;
        leaf.t.localScale = leaf.scale;

        float startY = scatter ? Random.Range(bottomY, topY + topMargin) : topY + topMargin;
        float x = leaf.baseX + Mathf.Sin(leaf.phase) * leaf.amp;
        leaf.t.position = new Vector3(x, startY, leaf.z);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        foreach (var leaf in leaves)
        {
            if (leaf.t == null) continue;

            leaf.age += dt;

            float angle = leaf.phase + leaf.age * leaf.omega;
            float y = leaf.t.position.y - leaf.speed * dt;
            float x = leaf.baseX + Mathf.Sin(angle) * leaf.amp;
            leaf.t.position = new Vector3(x, y, leaf.z);

            // Neigung in Schwungrichtung: horizontale Geschwindigkeit ~ cos(angle).
            float swingVel = Mathf.Cos(angle);
            float tilt = -swingVel * tiltDegrees;
            float spinAngle = leaf.spin != 0f ? leaf.spin * leaf.age : 0f;
            leaf.t.rotation = Quaternion.Euler(0f, 0f, baseRotation + tilt + spinAngle);

            if (y < bottomY - bottomMargin)
                ResetLeaf(leaf, scatter: false);
        }
    }
}
