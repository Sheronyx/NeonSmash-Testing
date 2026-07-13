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
    [Tooltip("Bewegtes Diamant-Bonus-Element (GravityPoint-Prefab mit isBonusDiamond=true). Nur in Phasen mit erreichtem Diamant-Bonus.")]
    [SerializeField] private GameObject gravityDiamondPrefab;
    [Tooltip("Wie viele Diamant-Bonus-Elemente ZUSÄTZLICH zu den normalen maxSpawnCount-Elementen kommen, " +
             "wenn der Bonus für diese Special-Phase aktiv ist. Fest, kein Zufalls-Roll — nur der Zeitpunkt " +
             "innerhalb der Phase ist zufällig verteilt.")]
    [SerializeField] private int diamondBonusCount = 5;
    [Tooltip("Score-Multiplikator eines Diamant-Bonus-Treffers (stapelt mit dem Special-Mode-Multiplikator).")]
    [SerializeField] private int diamondBonusMultiplier = 5;
    [Tooltip("Font-Material des Floating-Score-Texts bei Gravity-Treffern (wie materialPink/Green/Blue bei Normal-Mode-Treffern).")]
    [SerializeField] private Material scoreTextMaterial;

    /// <summary>Vom PhaseManager VOR dem Orb-Spawn gesetzt — vom nächsten Activate()-Aufruf konsumiert
    /// (egal ob per PhaseManager-Trigger oder automatisch über OnModeStarted, z.B. Tutorial).</summary>
    [HideInInspector] public int PendingMaxSpawnCount = -1;
    [HideInInspector] public bool PendingDiamondBonusActive = false;
    [HideInInspector] public float PendingIntensity = 1f;

    private int _spawnedCount = 0;
    private float _intensity = 1f;

    GameObject ActiveGravityPrefab =>
        SkinManager.Instance?.ActiveTheme?.gravityPointPrefab ?? gravityTapPrefab;

    [Header("Intensität (Wert kommt pro Phase vom PhaseManager)")]
    [Tooltip("Spawn-Intervall (Sekunden) bei Intensität = 1.0.")]
    [SerializeField] private float baseSpawnInterval = 1.5f;
    [Tooltip("Wie stark das Spawn-Intervall pro Intensitäts-Einheit über 1.0 sinkt (höher = schneller bei steigender Intensität).")]
    [SerializeField] private float spawnIntervalIntensityFactor = 0.3f;
    [Tooltip("Untere Grenze fürs Spawn-Intervall, damit es bei hoher Intensität nicht unspielbar wird.")]
    [SerializeField] private float minSpawnInterval = 0.3f;
    [Tooltip("Fallgeschwindigkeit bei Intensität = 1.0 (überschreibt den Fallback-Wert auf dem Prefab).")]
    [SerializeField] private float baseFallSpeed = 3f;
    [Tooltip("Wie stark Fallgeschwindigkeit + Sogkraft pro Intensitäts-Einheit über 1.0 steigen.")]
    [SerializeField] private float speedIntensityFactor = 1f;

    private bool isActive = false;
    private bool spawnLoopActive = false;

    /// <summary>Gefeuert wenn eine anzahl-limitierte Special-Phase (vom PhaseManager gestartet) fertig
    /// abgespielt ist: Spawn-Limit erreicht UND keine GravityPoints mehr aktiv. Nicht gefeuert bei
    /// unlimitierten Aktivierungen (z.B. Tutorial).</summary>
    public static event System.Action OnSpecialPhaseComplete;

    private float CurrentSpawnInterval() =>
        Mathf.Max(minSpawnInterval, baseSpawnInterval - (_intensity - 1f) * spawnIntervalIntensityFactor);

    public bool IsActive => isActive;

    private void Awake()
    {
        Instance = this;
    }


public void Activate()
{
    if (isActive) return;

    int maxSpawnCount        = PendingMaxSpawnCount;
    bool diamondBonusActive  = PendingDiamondBonusActive;
    _intensity               = PendingIntensity;
    PendingMaxSpawnCount       = -1;
    PendingDiamondBonusActive = false;
    PendingIntensity          = 1f;

    NeonAnalytics.LogSpecialModeTriggered("gravity");
    AchievementManager.OnSpecialModeTriggered("gravity");
    MissionManager.OnSpecialModeTriggered();
    StartCoroutine(Co_GravityMode(maxSpawnCount, diamondBonusActive));
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


    private IEnumerator Co_GravityMode(int maxSpawnCount = -1, bool diamondBonusActive = false)
    {
        Debug.Log("🌪️ Gravity Mode START");

        isActive = true;
        spawnLoopActive = true;
        _spawnedCount = 0;

        // 👉 normalen Spawner pausieren
        spawner.PauseSpawning(true);
        spawner.ClearAllGameplayPoints();

        // Diamant-Bonus: ZUSÄTZLICH zu maxSpawnCount (z.B. 20+5=25 statt 20 ersetzt), Zeitpunkte
        // innerhalb der Phase zufällig verteilt — kein Chance-Roll pro Tick mehr, garantiert exakt
        // diamondBonusCount Treffer, wenn der Bonus aktiv ist.
        int totalCount = maxSpawnCount > 0 && diamondBonusActive ? maxSpawnCount + diamondBonusCount : maxSpawnCount;
        var bonusTickIndices = new System.Collections.Generic.HashSet<int>();
        if (maxSpawnCount > 0 && diamondBonusActive && gravityDiamondPrefab != null)
            while (bonusTickIndices.Count < diamondBonusCount)
                bonusTickIndices.Add(Random.Range(0, totalCount));

        // Anzahl-basiert (vom PhaseManager): spawnt bis totalCount erreicht ist (-1 = unlimitiert,
        // z.B. Tutorial — endet dann nur über externes StopSpawning()/StopMode()).
        while (spawnLoopActive)
        {
            if (bonusTickIndices.Contains(_spawnedCount))
            {
                SpawnGravitySpecial(gravityDiamondPrefab);
            }
            else
            {
                // Pro Tick EIN Element: Shocker / Fake / normal (nicht-überlappende Chancen).
                float r       = Random.value;
                float thunder = spawner != null ? spawner.thunderSpawnChance : 0f;
                float fake    = spawner != null ? spawner.fakeSpawnChance    : 0f;

                if (gravityShockerPrefab != null && r < thunder)
                    SpawnGravitySpecial(gravityShockerPrefab);
                else if (gravityFakePrefab != null && r < thunder + fake)
                    SpawnGravitySpecial(gravityFakePrefab);
                else
                    SpawnGravityPoint();
            }

            _spawnedCount++;
            if (totalCount > 0 && _spawnedCount >= totalCount)
                spawnLoopActive = false;

            if (spawnLoopActive)
                yield return new WaitForSeconds(CurrentSpawnInterval());
        }

        // Spawn-Limit erreicht (nur bei anzahl-basierter Aktivierung) → auf Restelemente warten,
        // dann Mode sauber beenden und den PhaseManager benachrichtigen.
        if (totalCount > 0)
        {
            yield return new WaitUntil(() => FindObjectsByType<GravityPoint>(FindObjectsSortMode.None).Length == 0);
            StopMode();
            OnSpecialPhaseComplete?.Invoke();
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

    var gp = obj.GetComponent<GravityPoint>();
    if (gp != null)
    {
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.GravityPoint, worldPos);

        gp.Init(this);
        gp.SetSpeed(baseFallSpeed, GetSpeedMultiplier());
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
        var gp = obj.GetComponent<GravityPoint>();
        if (gp != null)
        {
            gp.Init(this);
            gp.SetSpeed(baseFallSpeed, GetSpeedMultiplier());
        }
    }

    public void OnPointDestroyed(bool tapped, Vector3 position = default, bool isBonusDiamond = false)
    {
        if (tapped)
        {
            SpecialModeManager.RegisterSpecialHit(isBonusDiamond ? diamondBonusMultiplier : 1, position, scoreTextMaterial);
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

/// <summary>Beendet den Gravity Mode. Wird intern aufgerufen, sobald das Spawn-Limit erreicht ist
/// UND keine GravityPoints mehr aktiv sind (kein manuelles Aufräumen nötig).
/// Das Pause-Flag des Spawners bleibt gesetzt, bis der PhaseManager die nächste Normal-Phase startet.</summary>
public void StopMode()
{
    if (!isActive) return;
    Debug.Log("🌪️ Gravity Mode END (StopMode)");

    isActive = false;
    spawnLoopActive = false;
    StopAllCoroutines();
    SpecialModeManager.Instance.EndCurrentMode();
}

private float GetSpeedMultiplier() => 1f + (_intensity - 1f) * speedIntensityFactor;
}