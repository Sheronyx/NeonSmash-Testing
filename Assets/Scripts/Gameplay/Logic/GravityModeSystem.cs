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
        }
    }

    public void OnPointDestroyed(bool tapped)
    {
        if (tapped)
        {
            ScoreManager.Instance?.AddPoints(2);
        }

        remaining--;
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
}