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

    GameObject ActiveProjectilePrefab =>
        SkinManager.Instance?.ActiveTheme?.beamProjectilePrefab ?? projectilePrefab;

    public void SpawnWithBeam(GameObject prefab, Vector3 targetPosition, System.Action onPointCreated = null)
    {
        StartCoroutine(Co_SpawnProjectile(prefab, targetPosition, onPointCreated));
    }

    private IEnumerator Co_SpawnProjectile(GameObject pointPrefab, Vector3 target, System.Action onPointCreated)
    {
        Vector3 start = portalOrigin.position;
        start.z  = 0f;
        target.z = 0f;

        GameObject projectile = Instantiate(ActiveProjectilePrefab, start, Quaternion.identity);

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

        spawner.CreatePoint(pointPrefab, target);
        onPointCreated?.Invoke();
    }
}
