using UnityEngine;

public abstract class BasePoint : MonoBehaviour
{
    public PointColor Color { get; set; }

    [Header("VFX")]
    [Tooltip("Explosions-Prefab — normales Partikelsystem (kein VFX Graph mehr). Beliebig viele Kind-" +
             "Partikelsysteme werden alle abgespielt, das Prefab räumt sich nach der längsten Laufzeit selbst auf.")]
    [SerializeField] protected GameObject explodeVFXPrefab;

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
        float dur = 0f;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Play();
            dur = Mathf.Max(dur, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        Destroy(fx, dur);
    }
}