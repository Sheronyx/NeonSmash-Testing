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
