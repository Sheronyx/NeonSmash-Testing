using UnityEngine;
using System.Collections;

public class FountainModeSystem : MonoBehaviour
{
    [SerializeField] private GameObject fountainPointPrefab;
    [SerializeField] private Transform portal;

    [Header("Spawn Settings")]
    [SerializeField] private float shootForceY = 6f;
    [SerializeField] private float shootForceX = 6f;
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private int totalPoints = 20;

    private int activePoints = 0;
    private int spawnedPoints = 0;

    private MixedPointSpawner spawner;

    // 🔥 Wird von außen getriggert
    public void Activate()
    {
        Debug.Log("💧 Fountain Mode ACTIVATED");

        spawnedPoints = 0;
        activePoints = 0;

        spawner = FindFirstObjectByType<MixedPointSpawner>();

        if (spawner != null)
        {
            spawner.PauseSpawning(true);

            // 🔥 WICHTIG – wie bei Gravity
            spawner.ClearAllGameplayPoints();
        }

        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (spawnedPoints < totalPoints)
        {
            SpawnPoint();
            spawnedPoints++;

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnPoint()
    {
        if (portal == null || fountainPointPrefab == null)
        {
            Debug.LogError("❌ FountainModeSystem: Missing references!");
            return;
        }

        Vector3 pos = portal.position;

        var go = Instantiate(fountainPointPrefab, pos, Quaternion.identity);
        var point = go.GetComponent<FountainPoint>();

        Vector3 velocity = new Vector3(
            Random.Range(-shootForceX, shootForceX),
            shootForceY,
            0f
        );

        point.Init(this, velocity);

        activePoints++;
    }

    public void OnPointFinished(bool hit)
    {
        if (hit)
        {
            ScoreManager.Instance?.AddPointsFromHit();
        }

        activePoints--;

        CheckEnd();
    }

    private void CheckEnd()
    {
        if (spawnedPoints >= totalPoints && activePoints <= 0)
        {
            EndMode();
        }
    }

    private void EndMode()
    {
        Debug.Log("💧 Fountain Mode END");

        if (spawner != null)
        {
            spawner.PauseSpawning(false);

            // 🔥 GANZ WICHTIG
            spawner.SpawnNextPoint();
        }

        SpecialModeManager.Instance?.EndCurrentMode();
    }
}