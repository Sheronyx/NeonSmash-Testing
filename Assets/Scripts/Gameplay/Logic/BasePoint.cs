using UnityEngine;
using UnityEngine.VFX;

public abstract class BasePoint : MonoBehaviour
{
    public PointColor Color { get; set; }

    [Header("VFX")]
    [Tooltip("VFX Graph Explosion (optional).")]
    [SerializeField] protected VisualEffect explodeVFXPrefab;
    [Tooltip("Legacy Particle System Explosion (optional). Kann zusätzlich oder statt der VFX Graph genutzt werden.")]
    [SerializeField] protected ParticleSystem explodeParticlePrefab;

    protected void SpawnExplosion()
    {
        if (explodeVFXPrefab != null)
            Instantiate(explodeVFXPrefab, transform.position, Quaternion.identity);

        if (explodeParticlePrefab != null)
        {
            var ps = Instantiate(explodeParticlePrefab, transform.position, Quaternion.identity);
            ps.Play();
            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(ps.gameObject, duration);
        }
    }
}