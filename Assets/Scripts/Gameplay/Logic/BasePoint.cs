using UnityEngine;
using UnityEngine.VFX;

public abstract class BasePoint : MonoBehaviour
{
    public PointColor Color { get; set; }

    [Header("VFX")]
    [SerializeField] protected VisualEffect explodeVFXPrefab;

    protected void SpawnExplosion()
    {
        if (explodeVFXPrefab == null)
            return;

        Instantiate(
            explodeVFXPrefab,
            transform.position,
            Quaternion.identity
        );
    }
}