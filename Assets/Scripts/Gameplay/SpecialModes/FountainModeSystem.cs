using UnityEngine;
using System.Collections;

public class FountainModeSystem : MonoBehaviour
{
    public static FountainModeSystem Instance;

    public static event System.Action OnFountainModeStarted;
    public static event System.Action OnFountainModeEnded;

    private void Awake()
    {
        Instance = this;
    }

    [SerializeField] private GameObject fountainPointPrefab;
    [SerializeField] private Transform portal;

    [Header("Spawn Settings")]
    [SerializeField] private float shootForceY = 6f;
    [SerializeField] private float shootForceX = 6f;
    [SerializeField] private float spawnInterval = 1.2f;
    [SerializeField] private int totalPoints = 20;

    [Header("Level Scaling")]
    [SerializeField] private LevelUp levelUp;
    [SerializeField] private float minSpawnInterval = 0.15f;
    [SerializeField] private float spawnIntervalDecreasePerLevel = 0.05f;

    private int activePoints = 0;
    private int spawnedPoints = 0;

    private MixedPointSpawner spawner;

    public void Activate()
    {
        NeonAnalytics.LogSpecialModeTriggered("fountain");
        AchievementManager.OnSpecialModeTriggered("fountain");
        MissionManager.OnSpecialModeTriggered();

        spawnedPoints = 0;
        activePoints = 0;

        spawner = FindFirstObjectByType<MixedPointSpawner>();

        if (spawner != null)
        {
            spawner.PauseSpawning(true);

            // 🔥 WICHTIG – wie bei Gravity
            spawner.ClearAllGameplayPoints();
        }

        OnFountainModeStarted?.Invoke();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (spawnedPoints < totalPoints)
        {
            SpawnPoint();
            spawnedPoints++;

            yield return new WaitForSeconds(GetCurrentSpawnInterval());
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

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.FountainPoint, pos);

        Vector3 velocity = new Vector3(
            Random.Range(-shootForceX, shootForceX),
            shootForceY,
            0f
        );

        point.Init(this, velocity);

        activePoints++;
    }

    private float GetCurrentSpawnInterval()
    {
        int level = levelUp != null ? levelUp.CurrentLevel : 1;
        return Mathf.Max(minSpawnInterval, spawnInterval - (level - 1) * spawnIntervalDecreasePerLevel);
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

    public void ForceStop()
    {
        StopAllCoroutines();
        spawnedPoints = totalPoints; // verhindert weiteres Spawnen
        activePoints = 0;
        foreach (var fp in FindObjectsByType<FountainPoint>(FindObjectsSortMode.None))
            Destroy(fp.gameObject);
        OnFountainModeEnded?.Invoke();
    }

    private void EndMode()
    {
        Debug.Log("💧 Fountain Mode END");

        if (spawner != null)
        {
            spawner.PauseSpawning(false);
            spawner.SpawnNextPoint();
        }

        OnFountainModeEnded?.Invoke();
        SpecialModeManager.Instance?.EndCurrentMode();
    }
}