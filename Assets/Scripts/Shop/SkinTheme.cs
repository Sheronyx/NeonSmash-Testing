using UnityEngine;

[CreateAssetMenu(fileName = "SkinTheme", menuName = "NeonSmash/Skin Theme")]
public class SkinTheme : ScriptableObject
{
    [Header("Prefab-Tausch (Null = Default)")]
    [Tooltip("Ersetzt das Standard Tap-Prefab. Null = Spawner-Default.")]
    public GameObject tapPointPrefab;

    [Tooltip("Ersetzt das Standard Swipe-Prefab. Null = Spawner-Default.")]
    public GameObject swipePointPrefab;

    [Tooltip("Ersetzt das Standard Slash-Trail-Prefab (SlashTrail.trailPrefab). Null = Default.")]
    public GameObject slashTrailPrefab;

    [Tooltip("Ersetzt das Standard Beam-Projectile (PortalSpawnBeam, vfx_NeonProjectile). Null = Default.")]
    public GameObject beamProjectilePrefab;

    [Tooltip("Ersetzt das Gravity-Point-Prefab. Null = Default.")]
    public GameObject gravityPointPrefab;

    [Tooltip("Ersetzt das Fountain-Point-Prefab. Null = Default.")]
    public GameObject fountainPointPrefab;

    [Tooltip("Ersetzt das Fake-Element-Prefab. Null = Default.")]
    public GameObject fakePointPrefab;

    [Tooltip("Ersetzt das Shock-/Thunder-Element-Prefab. Null = Default.")]
    public GameObject thunderPointPrefab;

    [Tooltip("Ersetzt das Peek-a-boo-Wolken-Prefab. Null = Default.")]
    public GameObject peekCloudPrefab;

    [Header("Peek-a-boo Ein-/Ausflug (optional)")]
    [Tooltip("Wenn aktiv, kommt das Peek-Element auf einem eigenen Flugweg rein/raus (z.B. Papagei von oben). Aus = Default-Seitenflug.")]
    public bool overridePeekFlightPath;
    [Tooltip("Viewport-Punkt, wo es reinkommt. (0.5, 1.3) = von oben mittig.")]
    public Vector2 peekEntryViewport = new Vector2(0.5f, 1.3f);
    [Tooltip("Viewport-Punkt, wohin es rausfliegt. (0.5, 1.3) = oben mittig.")]
    public Vector2 peekExitViewport = new Vector2(0.5f, 1.3f);

    // Szenen-Objekte (Portal-Plattform, Background, Top Bar) werden NICHT hier
    // referenziert, sondern per Enable/Disable über SkinObjectSwap /
    // PortalSkinApplier in der Szene geschaltet (theme-Matching auf dieses Asset).

    [Header("Portal-Farbe (Normal-Modus)")]
    [Tooltip("Wenn aktiv, werden Particles + Voronoi des Portals im Normal-Modus umgefärbt.")]
    public bool overridePortalColor;

    [ColorUsage(true, true)]
    public Color portalParticleColor = Color.green;

    [ColorUsage(true, true)]
    public Color portalVoronoiColor = Color.green;

    [Header("UI Top Bar Neon-Linien")]
    [Tooltip("Wenn aktiv, werden die Neon-Linien der Top Bar umgefärbt.")]
    public bool overrideTopBarColor;

    [ColorUsage(true, true)]
    public Color topBarColor = Color.green;
}
