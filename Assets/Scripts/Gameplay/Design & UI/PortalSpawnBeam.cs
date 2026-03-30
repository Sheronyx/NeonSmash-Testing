using System.Collections;
using UnityEngine;

public class PortalSpawnBeam : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private Transform portalOrigin;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Beam Settings")]
    [SerializeField] private float projectileSpeed = 25f;

    public void SpawnWithBeam(GameObject prefab, Vector3 targetPosition)
    {
        StartCoroutine(Co_SpawnProjectile(prefab, targetPosition));
    }

    private IEnumerator Co_SpawnProjectile(GameObject pointPrefab, Vector3 target)
    {
        Vector3 start = portalOrigin.position;
        start.z = 0f;
        target.z = 0f;

        GameObject projectile = Instantiate(projectilePrefab, start, Quaternion.identity);

        while (projectile != null && Vector3.Distance(projectile.transform.position, target) > 0.05f)
        {
            projectile.transform.position =
                Vector3.MoveTowards(projectile.transform.position, target, projectileSpeed * Time.deltaTime);

            yield return null;
        }

        if (projectile != null)
            Destroy(projectile);

        spawner.CreatePoint(pointPrefab, target);
    }
}