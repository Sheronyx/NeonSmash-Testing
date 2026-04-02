using System.Collections;
using UnityEngine;

public class PortalSpawnBeam : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject goldProjectilePrefab;
    private bool isGoldMode = false;

    [Header("Beam Settings")]
    [SerializeField] private float projectileSpeed = 50f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform portalOrigin;

    private void OnEnable()
    {
        GoldModeSystem.OnGoldModeStarted += EnableGold;
        GoldModeSystem.OnGoldModeEnded += DisableGold;
    }

    private void OnDisable()
    {
        GoldModeSystem.OnGoldModeStarted -= EnableGold;
        GoldModeSystem.OnGoldModeEnded -= DisableGold;
    }

    private void EnableGold() => isGoldMode = true;
    private void DisableGold() => isGoldMode = false;

    public void SpawnWithBeam(GameObject prefab, Vector3 targetPosition)
    {
        StartCoroutine(Co_SpawnProjectile(prefab, targetPosition));
    }

    private IEnumerator Co_SpawnProjectile(GameObject pointPrefab, Vector3 target)
    {
        Vector3 start = portalOrigin.position;
        start.z = 0f;
        target.z = 0f;

        GameObject prefabToUse = isGoldMode ? goldProjectilePrefab : projectilePrefab;

        GameObject projectile = Instantiate(prefabToUse, start, Quaternion.identity);

        while (projectile != null && Vector3.Distance(projectile.transform.position, target) > 0.05f)
        {
            projectile.transform.position = Vector3.MoveTowards(
                projectile.transform.position,
                target,
                projectileSpeed * Time.deltaTime
            );

            yield return null;
        }

        if (projectile != null)
            Destroy(projectile);

        // 🔥 DAS ist der wichtigste Moment
        spawner.CreatePoint(pointPrefab, target);
    }
}
