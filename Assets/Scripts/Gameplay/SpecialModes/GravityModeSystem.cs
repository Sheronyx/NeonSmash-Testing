using System.Collections;
using UnityEngine;

public class GravityModeSystem : MonoBehaviour
{
    public static GravityModeSystem Instance;

    [Header("Setup")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject gravityTapPrefab;
    [Tooltip("Bewegtes Shocker-Element (GravityPoint-Prefab mit isShocker=true). Über thunderChance eingestreut.")]
    [SerializeField] private GameObject gravityShockerPrefab;
    [Tooltip("Bewegtes Fake-Element (GravityPoint-Prefab mit isFake=true). Über fakeChance eingestreut.")]
    [SerializeField] private GameObject gravityFakePrefab;

    GameObject ActiveGravityPrefab =>
        SkinManager.Instance?.ActiveTheme?.gravityPointPrefab ?? gravityTapPrefab;

    [Header("Settings")]
    [Tooltip("Spawn-Abstand = Reaktionszeit des PhaseManagers × Faktor → folgt dem Intensitäts-Curve.")]
    [SerializeField] private float spawnIntervalFactor = 1f;
    [Tooltip("Fallback-Spawn-Abstand (s), falls kein PhaseManager vorhanden ist.")]
    [SerializeField] private float spawnInterval = 2f;
    [Tooltip("Zusätzliche Fallgeschwindigkeit bei maximaler Intensität (0 = konstant).")]
    [SerializeField] private float maxExtraSpeed = 0.6f;

    private bool isActive = false;
    private bool spawnLoopActive = false;

    // Spawn-Abstand folgt der Intensität des PhaseManagers (wie die Reaktionszeit der Spielphasen).
    private float CurrentSpawnInterval() =>
        PhaseManager.Instance != null
            ? PhaseManager.Instance.CurrentReactionTime * spawnIntervalFactor
            : spawnInterval;

    public bool IsActive => isActive;

    private void Awake()
    {
        Instance = this;
    }


public void Activate()
{
    if (isActive) return;

    NeonAnalytics.LogSpecialModeTriggered("gravity");
    AchievementManager.OnSpecialModeTriggered("gravity");
    MissionManager.OnSpecialModeTriggered();
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
        spawnLoopActive = true;

        // 👉 normalen Spawner pausieren
        spawner.PauseSpawning(true);
        spawner.ClearAllGameplayPoints();

        // Dauerbasiert: spawnt im Intervall, bis der PhaseManager StopSpawning()/StopMode() ruft.
        while (spawnLoopActive)
        {
            // Pro Tick EIN Element: Shocker / Fake / normal (nicht-überlappende Chancen).
            float r       = Random.value;
            float thunder = spawner != null ? spawner.thunderSpawnChance : 0f;
            float fake    = spawner != null ? spawner.fakeSpawnChance    : 0f;

            bool triggeredThunder = false;
            if (gravityShockerPrefab != null && r < thunder)
            { SpawnGravitySpecial(gravityShockerPrefab); triggeredThunder = true; }
            else if (gravityFakePrefab != null && r < thunder + fake)
                SpawnGravitySpecial(gravityFakePrefab);
            else
                SpawnGravityPoint();

            if (!triggeredThunder && spawner != null)
            {
                var pe = PortalElectrifier.Instance;
                if (pe != null && pe.CanActivate() && spawner.electricPortalChance > 0f
                    && Random.value < spawner.electricPortalChance)
                    pe.Activate();
            }

            yield return new WaitForSeconds(CurrentSpawnInterval());
        }
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

    GameObject obj = Instantiate(ActiveGravityPrefab, worldPos, Quaternion.identity);
    PortalElectrifier.Instance?.ElectrifyElement(obj);

    var gp = obj.GetComponent<GravityPoint>();
    if (gp != null)
    {
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.GravityPoint, worldPos);

        gp.Init(this);

        float multiplier = GetSpeedMultiplier();
        gp.SetSpeedMultiplier(multiplier);
    }
}

    // Bewegtes Special-Element (Shocker oder Fake) — fällt wie ein Gravity-Point.
    private void SpawnGravitySpecial(GameObject prefab)
    {
        if (prefab == null) return;

        Camera cam = Camera.main;
        float randomX = Random.Range(0.1f, 0.9f);
        Vector2 vp = new Vector2(randomX, 1.1f);
        Vector3 worldPos = cam.ViewportToWorldPoint(
            new Vector3(vp.x, vp.y, Mathf.Abs(cam.transform.position.z)));
        worldPos.z = 0f;

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
        PortalElectrifier.Instance?.ElectrifyElement(obj);
        var gp = obj.GetComponent<GravityPoint>();
        if (gp != null)
        {
            gp.Init(this);
            gp.SetSpeedMultiplier(GetSpeedMultiplier());
        }
    }

    public void OnPointDestroyed(bool tapped, Vector3 position = default)
    {
        if (tapped)
        {
            SpecialModeManager.RegisterSpecialHit();
        }
        else
        {
            SpecialModeManager.RegisterSpecialMiss();
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
    }

public void ForceStop()
{
    if (!isActive) return;
    StopAllCoroutines();
    isActive = false;
    spawnLoopActive = false;
    foreach (var gp in FindObjectsByType<GravityPoint>(FindObjectsSortMode.None))
        Destroy(gp.gameObject);
}

/// <summary>Phasenende, Schritt 1: nur den Spawn-Loop stoppen. Mode bleibt aktiv
/// (Portal/Scoring/Input), damit die Restelemente normal zu Ende gespielt werden können.
/// Der PhaseManager ruft danach StopMode(), wenn alle Elemente ausgelaufen sind.</summary>
public void StopSpawning()
{
    spawnLoopActive = false;
}

/// <summary>Vom PhaseManager am Phasenende: Gravity-Spawn-Loop stoppen + Modus beenden.
/// Die noch aktiven Gravity-Punkte räumt der PhaseManager anschließend positiv (PositiveClearAll).
/// Das Pause-Flag bleibt gesetzt, bis der nächste Phasen-Banner das Spawning wieder freigibt.</summary>
public void StopMode()
{
    if (!isActive) return;
    Debug.Log("🌪️ Gravity Mode END (StopMode)");

    isActive = false;
    spawnLoopActive = false;
    StopAllCoroutines();
    SpecialModeManager.Instance.EndCurrentMode();
}

private float GetSpeedMultiplier()
{
    // Fallgeschwindigkeit folgt der Intensität des PhaseManagers (Curve).
    if (PhaseManager.Instance == null) return 1f;
    return 1f + PhaseManager.Instance.CurrentIntensity01 * maxExtraSpeed;
}
}