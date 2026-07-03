using System.Collections;
using UnityEngine;

public class PortalSpawnBeam : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MixedPointSpawner spawner;

    [Header("Beam Settings")]
    [SerializeField] private float projectileSpeed = 50f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform portalOrigin;

    [Header("Farbige Projektil-Prefabs (Pink / Blau / Grün / Orange)")]
    [SerializeField] private GameObject projectilePrefab_Pink;
    [SerializeField] private GameObject projectilePrefab_Blue;
    [SerializeField] private GameObject projectilePrefab_Green;
    [SerializeField] private GameObject projectilePrefab_Orange;

    GameObject ActiveProjectilePrefab =>
        SkinManager.Instance?.ActiveTheme?.beamProjectilePrefab ?? projectilePrefab;

    GameObject ProjectilePrefabForColor(PointColor color) => color switch
    {
        PointColor.Pink   => projectilePrefab_Pink   ?? ActiveProjectilePrefab,
        PointColor.Blue   => projectilePrefab_Blue   ?? ActiveProjectilePrefab,
        PointColor.Green  => projectilePrefab_Green  ?? ActiveProjectilePrefab,
        PointColor.Orange => projectilePrefab_Orange ?? ActiveProjectilePrefab,
        _                 => ActiveProjectilePrefab
    };

    // Tutorial: feuert Projektil, dann ruft CreatePoint auf dem Spawner auf.
    public void SpawnWithBeam(GameObject prefab, Vector3 targetPosition, System.Action onPointCreated = null)
    {
        StartCoroutine(Co_SpawnProjectile(prefab, targetPosition, onPointCreated));
    }

    // Gameplay: feuert farbiges Projektil-Prefab, ruft dann onArrived auf.
    public void FireBeamTo(Vector3 targetPosition, PointColor color, System.Action onArrived)
    {
        StartCoroutine(Co_FireBeam(targetPosition, color, onArrived));
    }

    private IEnumerator Co_SpawnProjectile(GameObject pointPrefab, Vector3 target, System.Action onPointCreated)
    {
        yield return Co_MoveProjectile(target, ActiveProjectilePrefab);
        spawner.CreatePoint(pointPrefab, target);
        onPointCreated?.Invoke();
    }

    private IEnumerator Co_FireBeam(Vector3 target, PointColor color, System.Action onArrived)
    {
        yield return Co_MoveProjectile(target, ProjectilePrefabForColor(color));
        onArrived?.Invoke();
    }

    private IEnumerator Co_MoveProjectile(Vector3 target, GameObject prefab)
    {
        Vector3 start = portalOrigin.position;
        start.z  = 0f;
        target.z = 0f;

        GameObject projectile = Instantiate(prefab, start, Quaternion.identity);

        while (projectile != null && Vector3.Distance(projectile.transform.position, target) > 0.05f)
        {
            projectile.transform.position = Vector3.MoveTowards(
                projectile.transform.position,
                target,
                projectileSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (projectile != null) Destroy(projectile);
    }
}
