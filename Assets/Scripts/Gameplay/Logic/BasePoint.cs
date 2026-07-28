using UnityEngine;

public abstract class BasePoint : MonoBehaviour
{
    public PointColor Color { get; set; }

    [Header("VFX")]
    [Tooltip("Explosions-Prefab — normales Partikelsystem (kein VFX Graph mehr). Beliebig viele Kind-" +
             "Partikelsysteme werden alle abgespielt, das Prefab räumt sich nach der längsten Laufzeit selbst auf.")]
    [SerializeField] protected GameObject explodeVFXPrefab;

    protected void SpawnExplosion()
    {
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