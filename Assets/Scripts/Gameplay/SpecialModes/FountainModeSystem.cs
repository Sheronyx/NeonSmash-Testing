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
    [Tooltip("Bewegtes Shocker-Element (FountainPoint-Prefab mit isShocker=true). Über thunderChance eingestreut.")]
    [SerializeField] private GameObject fountainShockerPrefab;
    [Tooltip("Bewegtes Fake-Element (FountainPoint-Prefab mit isFake=true). Über fakeChance eingestreut.")]
    [SerializeField] private GameObject fountainFakePrefab;
    [SerializeField] private Transform portal;

    GameObject ActiveFountainPrefab =>
        SkinManager.Instance?.ActiveTheme?.fountainPointPrefab ?? fountainPointPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float shootForceY = 6f;
    [SerializeField] private float shootForceX = 6f;
    [Tooltip("Zufalls-Streuung der Boden-Höhe (Y). Größer = unterschiedlichere Wurfhöhen/Kurven.")]
    [SerializeField] private float shootForceYVariance = 2f;

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

    [Header("Spawn-Intervall (folgt dem PhaseManager-Curve)")]
    [Tooltip("Spawn-Abstand = Reaktionszeit des PhaseManagers × Faktor → folgt dem Intensitäts-Curve.")]
    [SerializeField] private float spawnIntervalFactor = 1f;
    [Tooltip("Fallback-Spawn-Abstand (s), falls kein PhaseManager vorhanden ist.")]
    [SerializeField] private float fallbackSpawnInterval = 1.2f;

    private bool isActive = false;
    private bool spawnLoopActive = false;
    public bool IsActive => isActive;

    private MixedPointSpawner spawner;

    public void Activate()
    {
        if (isActive) return;

        NeonAnalytics.LogSpecialModeTriggered("fountain");
        AchievementManager.OnSpecialModeTriggered("fountain");
        MissionManager.OnSpecialModeTriggered();

        isActive = true;
        spawnLoopActive = true;

        spawner = FindFirstObjectByType<MixedPointSpawner>();
        if (spawner != null)
        {
            spawner.PauseSpawning(true);
            spawner.ClearAllGameplayPoints();   // wie bei Gravity
        }

        OnFountainModeStarted?.Invoke();
        StartCoroutine(SpawnRoutine());
    }

    // Dauerbasiert: spawnt im Intervall, bis der PhaseManager StopSpawning()/StopMode() ruft.
    private IEnumerator SpawnRoutine()
    {
        while (spawnLoopActive)
        {
            // Pro Tick EIN Element: Shocker / Fake / normal (nicht-überlappende Chancen).
            float r       = Random.value;
            float thunder = spawner != null ? spawner.thunderSpawnChance : 0f;
            float fake    = spawner != null ? spawner.fakeSpawnChance    : 0f;

            if (fountainShockerPrefab != null && r < thunder)
                SpawnPoint(fountainShockerPrefab);
            else if (fountainFakePrefab != null && r < thunder + fake)
                SpawnPoint(fountainFakePrefab);
            else
                SpawnPoint(ActiveFountainPrefab);

            yield return new WaitForSeconds(GetCurrentSpawnInterval());
        }
    }

    private void SpawnPoint() => SpawnPoint(ActiveFountainPrefab);

    private void SpawnPoint(GameObject prefab)
    {
        if (portal == null || prefab == null)
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

        var go = Instantiate(prefab, pos, Quaternion.identity);
        var point = go.GetComponent<FountainPoint>();

        if (prefab == ActiveFountainPrefab && TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.FountainPoint, pos);

        point.Init(this, velocity);
    }

    // Spawn-Abstand folgt der Intensität des PhaseManagers (wie die Reaktionszeit der Spielphasen).
    private float GetCurrentSpawnInterval() =>
        PhaseManager.Instance != null
            ? PhaseManager.Instance.CurrentReactionTime * spawnIntervalFactor
            : fallbackSpawnInterval;

    public void OnPointFinished(bool hit)
    {
        if (hit)
            SpecialModeManager.RegisterSpecialHit();
        else
            SpecialModeManager.RegisterSpecialMiss();
    }

    /// <summary>Vom PhaseManager am Phasenende: Spawn-Loop stoppen + Modus beenden.
    /// Restliche Fountain-Punkte räumt der PhaseManager positiv (PositiveClearAll → TryTap).</summary>
    public void StopMode()
    {
        if (!isActive) return;
        Debug.Log("💧 Fountain Mode END (StopMode)");

        isActive = false;
        spawnLoopActive = false;
        StopAllCoroutines();
        OnFountainModeEnded?.Invoke();
        SpecialModeManager.Instance.EndCurrentMode();
    }

    /// <summary>Phasenende, Schritt 1: nur den Spawn-Loop stoppen. Mode bleibt aktiv
    /// (Portal/Scoring/Input), damit die Restelemente normal zu Ende gespielt werden können.
    /// Der PhaseManager ruft danach StopMode(), wenn alle Elemente ausgelaufen sind.</summary>
    public void StopSpawning()
    {
        spawnLoopActive = false;
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        isActive = false;
        spawnLoopActive = false;
        foreach (var fp in FindObjectsByType<FountainPoint>(FindObjectsSortMode.None))
            Destroy(fp.gameObject);
        OnFountainModeEnded?.Invoke();
    }
}
