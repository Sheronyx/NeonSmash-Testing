using UnityEngine;

public abstract class BasePoint : MonoBehaviour
{
    public PointColor Color { get; set; }

    // Vom Spawner direkt nach Instantiate gemessen und hier zwischengespeichert (siehe
    // MixedPointSpawner.CacheHalfSize/GetHalfSizePixels): mehrteilige Prefabs (MagneticFragmentFloat/
    // -Orbit-Stücke) fliegen beim Spawnen erst aus der Mitte an ihre Position (siehe PointFlyIn) — eine
    // Live-Messung der Collider/SpriteRenderer-Bounds WÄHREND dieser Animation würde die tatsächliche
    // Endgröße unterschätzen und dadurch den Sicherheitsabstand zu neu spawnenden Nachbarn gelegentlich
    // zu klein ansetzen (sichtbares Überlappen, sobald die Stücke fertig auseinandergeschwebt sind).
    [System.NonSerialized] public float? CachedHalfSizePixels;

    [Header("VFX")]
    [Tooltip("Explosions-Prefab — normales Partikelsystem (kein VFX Graph mehr). Beliebig viele Kind-" +
             "Partikelsysteme werden alle abgespielt, das Prefab räumt sich nach der längsten Laufzeit selbst auf.")]
    [SerializeField] protected GameObject explodeVFXPrefab;

    [Tooltip("Zusätzlicher Z-Versatz beim Spawnen (MixedPointSpawner setzt die X/Y-Position sonst " +
             "immer auf Z=0). Für 3D-Modelle nötig, die vor/hinter den Hintergrund-Ebenen liegen " +
             "müssen — 2D-Sprite-Elemente lassen das auf 0.")]
    [SerializeField] private float spawnDepthOffset = 0f;
    public float SpawnDepthOffset => spawnDepthOffset;

    // Vom PlayerInputHandler gesetzt, kurz bevor ein Boost-"Swipe How You Like"/"All Swipe"-Treffer
    // TryTap()/ForceDestroy() auslöst — SpawnExplosion() nutzt das, um bei vorhandenem Fragmenter die
    // Slice-Optik statt der normalen Partikel-Explosion zu zeigen.
    private Vector2? pendingSliceDirection;
    public void SetPendingSliceDirection(Vector2 dir) => pendingSliceDirection = dir;

    protected void SpawnExplosion()
    {
        if (pendingSliceDirection.HasValue)
        {
            Vector2 dir = pendingSliceDirection.Value;
            pendingSliceDirection = null;

            var fragmenter = GetComponent<Fragmenter>();
            if (fragmenter != null && fragmenter.TrySlice(transform.position, dir))
                return;
        }

        if (explodeVFXPrefab == null)
            return;

        var fx = Instantiate(explodeVFXPrefab, transform.position, Quaternion.identity);

        // Neuere 3D-Brocken-Explosionen (ElementChunkExplosion) verwalten Abspielen UND Aufräumen
        // komplett selbst (auch wenn sie zusätzlich ein Glow-Partikelsystem als Kind-Objekt enthalten) —
        // hier also nichts anfassen. Nur bei alten, reinen Partikel-Explosions-Prefabs (kein
        // ElementChunkExplosion) übernehmen wir Play()/Destroy() wie bisher.
        if (fx.GetComponent<ElementChunkExplosion>() != null)
            return;

        var particleSystems = fx.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems.Length > 0)
        {
            float dur = 0f;
            foreach (var ps in particleSystems)
            {
                ps.Play();
                dur = Mathf.Max(dur, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            Destroy(fx, dur);
        }
    }
}