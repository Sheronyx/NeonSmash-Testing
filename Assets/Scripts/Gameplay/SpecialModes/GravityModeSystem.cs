using System.Collections;
using UnityEngine;

public class GravityModeSystem : MonoBehaviour
{
    public static GravityModeSystem Instance;

    [Header("Setup")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject gravityTapPrefab;

    [Header("Settings")]
    [SerializeField] private int elementCount = 30;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float speedIncreasePerLevel = 0.15f;

    [SerializeField] private LevelUp levelUp;

    private int remaining;
    private bool isActive = false;

    public bool IsActive => isActive;

    private void Awake()
    {
        Instance = this;
    }

    private BackgroundLooperPerfect looper;

private void Start()
{
    looper = FindFirstObjectByType<BackgroundLooperPerfect>();
}

public void Activate()
{
    if (isActive) return;

    looper?.SetGravityMode(true);

    StartCoroutine(Co_GravityMode());
}


    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleModeStart;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleModeStart;
    }

    private void HandleModeStart(SpecialMode mode)
    {
        if (mode == SpecialMode.Gravity)
        {
            Activate();
        }
    }


    private IEnumerator Co_GravityMode()
    {
        Debug.Log("🌪️ Gravity Mode START");

        isActive = true;
        remaining = elementCount;

        // 👉 normalen Spawner pausieren
        spawner.PauseSpawning(true);

        spawner.ClearAllGameplayPoints();

        for (int i = 0; i < elementCount; i++)
        {
            SpawnGravityPoint();
            yield return new WaitForSeconds(spawnInterval);
        }

        while (remaining > 0)
        {
            yield return null;
        }

        EndMode();
    }


    private void SpawnGravityPoint()
{
    Camera cam = Camera.main;

    float randomX = Random.Range(0.1f, 0.9f);
    Vector2 vp = new Vector2(randomX, 1.1f);

    Vector3 worldPos = cam.ViewportToWorldPoint(
        new Vector3(vp.x, vp.y, Mathf.Abs(cam.transform.position.z))
    );
    worldPos.z = 0f;

    GameObject obj = Instantiate(gravityTapPrefab, worldPos, Quaternion.identity);

    var gp = obj.GetComponent<GravityPoint>();
    if (gp != null)
    {
        gp.Init(this);

        float multiplier = GetSpeedMultiplier();
        gp.SetSpeedMultiplier(multiplier);
    }
}

    public void OnPointDestroyed(bool tapped, Vector3 position = default)
    {
        if (tapped)
        {
            ScoreManager.Instance?.AddPoints(1);
        }
        else
        {
            if (LivesManager.Instance != null)
            {
                bool stillAlive = LivesManager.Instance.LoseLife(position);
                if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
                if (!stillAlive)
                {
                    // Game Over auslösen über den Spawner
                    spawner.TriggerGameOverFromGravity();
                }
            }
        }

        remaining--;
    }

public void ForceStop()
{
    if (!isActive) return;
    StopAllCoroutines();
    isActive = false;
    remaining = 0;
    looper?.SetGravityMode(false);
    foreach (var gp in FindObjectsByType<GravityPoint>(FindObjectsSortMode.None))
        Destroy(gp.gameObject);
}

private void EndMode()
{
    Debug.Log("🌪️ Gravity Mode END");

    looper?.SetGravityMode(false);

    isActive = false;

    spawner.PauseSpawning(false);
    spawner.SpawnNextPoint();

    SpecialModeManager.Instance.EndCurrentMode();
}

private float GetSpeedMultiplier()
{
    if (levelUp == null) return 1f;

    int level = levelUp.CurrentLevel;

    // Beispiel Scaling
    return 1f + (level - 1) * speedIncreasePerLevel;
}
}