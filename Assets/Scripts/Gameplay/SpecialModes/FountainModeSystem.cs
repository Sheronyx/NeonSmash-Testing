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

    GameObject ActiveFountainPrefab =>
        SkinManager.Instance?.ActiveTheme?.fountainPointPrefab ?? fountainPointPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float shootForceY = 6f;
    [SerializeField] private float shootForceX = 6f;
    [Tooltip("Zufalls-Streuung der Boden-Höhe (Y). Größer = unterschiedlichere Wurfhöhen/Kurven.")]
    [SerializeField] private float shootForceYVariance = 2f;
    [SerializeField] private float spawnInterval = 1.2f;
    [SerializeField] private int totalPoints = 20;

    [Header("Seiten-Schuss (links/rechts rein)")]
    [Tooltip("Wahrscheinlichkeit, dass von der Seite statt von unten geschossen wird.")]
    [Range(0f, 1f)]
    [SerializeField] private float sideSpawnChance = 0.5f;
    [Tooltip("Horizontale Schussstärke beim Seiten-Schuss (Richtung Bildschirmmitte).")]
    [SerializeField] private float sideShootForceX = 8f;
    [Tooltip("Vertikale Schussstärke beim Seiten-Schuss (meist kleiner als von unten).")]
    [SerializeField] private float sideShootForceY = 4.5f;
    [Tooltip("Viewport-Höhe (0..1), auf der die Seiten-Elemente reinkommen.")]
    [Range(0f, 1f)]
    [SerializeField] private float sideSpawnHeight = 0.25f;
    [Tooltip("Zufalls-Streuung der Seiten-Einstiegshöhe (Viewport).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float sideSpawnHeightVariance = 0.15f;
    [Tooltip("Zufalls-Streuung der Seiten-Velocity (Wucht/Kurve).")]
    [SerializeField] private float sideForceVariance = 1.5f;

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

        Vector3 pos;
        Vector3 velocity;

        if (Random.value < sideSpawnChance)
        {
            // Seiten-Schuss: von links nach rechts-oben oder von rechts nach links-oben
            bool fromLeft = Random.value < 0.5f;
            Camera cam = Camera.main;
            float camZ = cam != null ? Mathf.Abs(cam.transform.position.z) : 10f;
            float vx = fromLeft ? -0.05f : 1.05f;   // knapp außerhalb des Bildschirms
            float vy = Mathf.Clamp01(sideSpawnHeight + Random.Range(-sideSpawnHeightVariance, sideSpawnHeightVariance));
            pos = cam != null
                ? cam.ViewportToWorldPoint(new Vector3(vx, vy, camZ))
                : portal.position;
            pos.z = 0f;

            float dirX = fromLeft ? sideShootForceX : -sideShootForceX;
            velocity = new Vector3(
                dirX + Random.Range(-sideForceVariance, sideForceVariance),
                sideShootForceY + Random.Range(-sideForceVariance, sideForceVariance),
                0f
            );
        }
        else
        {
            // Klassischer Schuss von unten (Portal)
            pos = portal.position;
            velocity = new Vector3(
                Random.Range(-shootForceX, shootForceX),
                shootForceY + Random.Range(-shootForceYVariance, shootForceYVariance),
                0f
            );
        }

        var go = Instantiate(ActiveFountainPrefab, pos, Quaternion.identity);
        var point = go.GetComponent<FountainPoint>();

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.FountainPoint, pos);

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
            SpecialModeManager.RegisterSpecialHit();
        else
            SpecialModeManager.RegisterSpecialMiss();

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

        OnFountainModeEnded?.Invoke();
        SpecialModeManager.Instance?.EndCurrentMode(); // Display aktualisiert sich VOR dem ersten neuen Point

        if (spawner != null)
        {
            spawner.PauseSpawning(false);
            spawner.SpawnNextPoint();
        }
    }
}