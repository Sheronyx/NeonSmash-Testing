using UnityEngine;

// Konfiguriert ein Particle System auf den Glühwürmchen-Look (Tropical Jungle):
// kleine leuchtende Punkte, die langsam und sehr zufällig durchs Bild treiben —
// mal größer, mal kleiner, mal von oben, mal von der Seite rein und seitlich wieder
// raus. Das Umherfliegen kommt vom Noise-Modul, das An-/Abschwellen (Twinkle) über
// die Color-over-Lifetime-Alpha-Kurve.
//
// Setup: leeres GameObject + Particle System + dieses Skript. Im Particle-System-
// Renderer ein additives, weiches Punkt-Sprite/Material zuweisen (Glow). Die Module
// werden komplett von hier aus gesetzt — die Werte unten sind die Stellschrauben.
[RequireComponent(typeof(ParticleSystem))]
public class FireflyParticles : MonoBehaviour
{
    [Header("Menge")]
    [Tooltip("Neue Glühwürmchen pro Sekunde.")]
    [SerializeField] private float spawnRate = 6f;
    [Tooltip("Obergrenze gleichzeitig sichtbarer Partikel.")]
    [SerializeField] private int maxParticles = 120;

    [Header("Lebensdauer (langsames Durchziehen)")]
    [SerializeField] private float lifetimeMin = 7f;
    [SerializeField] private float lifetimeMax = 16f;

    [Header("Größe (mal größer, mal kleiner) — World Units")]
    [SerializeField] private float sizeMin = 0.05f;
    [SerializeField] private float sizeMax = 0.28f;

    [Header("Drift")]
    [Tooltip("Grund-Sinkgeschwindigkeit nach unten (Units/Sek).")]
    [SerializeField] private float fallSpeedMin = 0.15f;
    [SerializeField] private float fallSpeedMax = 0.6f;
    [Tooltip("Seitliche Grunddrift (±, Units/Sek) — manche ziehen quer durchs Bild.")]
    [SerializeField] private float sideDrift = 0.5f;

    [Header("Umherfliegen (Noise)")]
    [Tooltip("Wie weit sie vom geraden Weg abweichen (Stärke).")]
    [SerializeField] private float noiseStrength = 0.35f;
    [Tooltip("Wie 'zappelig' die Bewegung ist (höher = schnellere Richtungswechsel).")]
    [SerializeField] private float noiseFrequency = 0.3f;

    [Header("Glow-Farbe")]
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.45f, 1f);

    [Header("Bereich")]
    [Tooltip("Wie weit die Emissions-Box über den Bildrand hinausragt (Units), damit Partikel auch von außen reinkommen.")]
    [SerializeField] private float areaMargin = 1.5f;

    private void Awake()
    {
        Configure();
    }

    [ContextMenu("Configure Now")]
    private void Configure()
    {
        var ps = GetComponent<ParticleSystem>();
        var cam = Camera.main;

        // Bildgröße in World Units bestimmen (für Emissions-Box + Position)
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = cam != null ? halfH * cam.aspect : 3f;
        Vector3 center = cam != null ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0f) : Vector3.zero;

        bool wasPlaying = Application.isPlaying;
        if (wasPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // ---- Main ----
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = 0f;                       // Bewegung kommt aus Velocity + Noise
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = glowColor;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.playOnAwake = true;

        // ---- Emission ----
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = spawnRate;

        // ---- Shape (Box, leicht über den Bildrand hinaus) ----
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.scale = new Vector3((halfW + areaMargin) * 2f, (halfH + areaMargin) * 2f, 0.1f);
        transform.position = center;

        // ---- Velocity over Lifetime (Sinken + seitliche Drift) ----
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-sideDrift, sideDrift);
        vel.y = new ParticleSystem.MinMaxCurve(-fallSpeedMax, -fallSpeedMin);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // gleicher Modus wie X/Y!

        // ---- Noise (das organische Umherfliegen) ----
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // ---- Color over Lifetime (Twinkle: einblenden → halten → ausblenden) ----
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ---- Size over Lifetime (sanftes Aufglimmen/Verlöschen) ----
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.25f, 1f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 0.4f));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // ---- Renderer (Billboard; Material/Sprite weist du selbst zu) ----
        var renderer = GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (wasPlaying) ps.Play();
    }
}
