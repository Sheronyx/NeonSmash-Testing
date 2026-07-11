using System.Collections;
using UnityEngine;

public class VortexModeSystem : MonoBehaviour
{
    public static VortexModeSystem Instance;

    [Header("Setup")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject vortexTapPrefab;
    [Tooltip("Vortex-Element mit isShocker=true. Über thunderChance eingestreut.")]
    [SerializeField] private GameObject vortexShockerPrefab;
    [Tooltip("Vortex-Element mit isFake=true. Über fakeChance eingestreut.")]
    [SerializeField] private GameObject vortexFakePrefab;
    [Tooltip("Vortex-Element mit isBonusDiamond=true. Nur in Phasen mit erreichtem Diamant-Bonus.")]
    [SerializeField] private GameObject vortexDiamondPrefab;
    [Range(0f, 1f)]
    [Tooltip("Spawn-Chance des Diamant-Bonus-Elements, wenn der Bonus für diese Special-Phase aktiv ist.")]
    [SerializeField] private float diamondBonusChance = 0.15f;
    [Tooltip("Score-Multiplikator eines Diamant-Bonus-Treffers (stapelt mit dem Special-Mode-Multiplikator).")]
    [SerializeField] private int diamondBonusMultiplier = 5;
    [Tooltip("Font-Material des Floating-Score-Texts bei Vortex-Treffern (wie materialPink/Green/Blue bei Normal-Mode-Treffern).")]
    [SerializeField] private Material scoreTextMaterial;

    /// <summary>Vom PhaseManager VOR dem Orb-Spawn gesetzt — vom nächsten Activate()-Aufruf konsumiert.</summary>
    [HideInInspector] public int PendingMaxSpawnCount = -1;
    [HideInInspector] public bool PendingDiamondBonusActive = false;
    [HideInInspector] public float PendingIntensity = 1f;

    private int _spawnedCount = 0;
    private float _intensity = 1f;

    [Header("Intensität (Wert kommt pro Phase vom PhaseManager)")]
    [Tooltip("Spawn-Intervall (Sekunden) bei Intensität = 1.0.")]
    [SerializeField] private float baseSpawnInterval = 1.2f;
    [Tooltip("Wie stark das Spawn-Intervall pro Intensitäts-Einheit über 1.0 sinkt.")]
    [SerializeField] private float spawnIntervalIntensityFactor = 0.25f;
    [Tooltip("Untere Grenze fürs Spawn-Intervall, damit es bei hoher Intensität nicht unspielbar wird.")]
    [SerializeField] private float minSpawnInterval = 0.3f;
    [Tooltip("Reisezeit (Sekunden) vom Bildschirmrand bis zum Vortex-Zentrum bei Intensität = 1.0.")]
    [SerializeField] private float baseTravelDuration = 2.2f;
    [Tooltip("Wie stark die Reisegeschwindigkeit pro Intensitäts-Einheit über 1.0 steigt (Reisezeit sinkt entsprechend).")]
    [SerializeField] private float speedIntensityFactor = 1f;
    [Tooltip("Untere Grenze für die Reisezeit, damit Elemente bei hoher Intensität nicht unspielbar schnell werden.")]
    [SerializeField] private float minTravelDuration = 0.5f;

    [Header("Kurve")]
    [Tooltip("Gesamter Schwenkwinkel der Spiralkurve in Grad. >180 = mehr als ein Halbkreis.")]
    [SerializeField] private float totalSweepDegrees = 220f;
    [Tooltip("Drehrichtung — EINHEITLICH für alle Elemente (kein Zufall pro Element).")]
    [SerializeField] private bool clockwise = true;
    [Tooltip("Wie früh die Kurve einsetzt: 1 = Kurve läuft von Anfang an gleichmäßig mit, " +
             ">1 = Kurve setzt später/kräftiger ein (2 = quadratisch), <1 = Kurve setzt noch früher ein.")]
    [SerializeField] private float angleEaseExponent = 1.4f;

    private bool isActive = false;
    private bool spawnLoopActive = false;
    public bool IsActive => isActive;

    /// <summary>Gefeuert wenn eine anzahl-limitierte Special-Phase (vom PhaseManager gestartet) fertig
    /// abgespielt ist: Spawn-Limit erreicht UND keine VortexPoints mehr aktiv.</summary>
    public static event System.Action OnSpecialPhaseComplete;

    private float CurrentSpawnInterval() =>
        Mathf.Max(minSpawnInterval, baseSpawnInterval - (_intensity - 1f) * spawnIntervalIntensityFactor);

    private float CurrentTravelDuration()
    {
        float multiplier = 1f + (_intensity - 1f) * speedIntensityFactor;
        return Mathf.Max(minTravelDuration, baseTravelDuration / Mathf.Max(0.01f, multiplier));
    }

    private void Awake()
    {
        Instance = this;
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
        if (mode == SpecialMode.Vortex)
            Activate();
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

        NeonAnalytics.LogSpecialModeTriggered("vortex");
        AchievementManager.OnSpecialModeTriggered("vortex");
        MissionManager.OnSpecialModeTriggered();

        StartCoroutine(Co_VortexMode(maxSpawnCount, diamondBonusActive));
    }

    private IEnumerator Co_VortexMode(int maxSpawnCount = -1, bool diamondBonusActive = false)
    {
        Debug.Log("🌀 Vortex Mode START");

        isActive = true;
        spawnLoopActive = true;
        _spawnedCount = 0;

        spawner.PauseSpawning(true);
        spawner.ClearAllGameplayPoints();

        while (spawnLoopActive)
        {
            // Pro Tick EIN Element: Shocker / Fake / Diamant-Bonus / normal (nicht-überlappende Chancen).
            float r       = Random.value;
            float thunder = spawner != null ? spawner.thunderSpawnChance : 0f;
            float fake    = spawner != null ? spawner.fakeSpawnChance    : 0f;
            float diamond = diamondBonusActive && vortexDiamondPrefab != null ? diamondBonusChance : 0f;

            if (vortexShockerPrefab != null && r < thunder)
                SpawnVortexPoint(vortexShockerPrefab);
            else if (vortexFakePrefab != null && r < thunder + fake)
                SpawnVortexPoint(vortexFakePrefab);
            else if (diamond > 0f && r < thunder + fake + diamond)
                SpawnVortexPoint(vortexDiamondPrefab);
            else
                SpawnVortexPoint(vortexTapPrefab);

            _spawnedCount++;
            if (maxSpawnCount > 0 && _spawnedCount >= maxSpawnCount)
                spawnLoopActive = false;

            if (spawnLoopActive)
                yield return new WaitForSeconds(CurrentSpawnInterval());
        }

        // Spawn-Limit erreicht → auf Restelemente warten, dann Mode sauber beenden.
        if (maxSpawnCount > 0)
        {
            yield return new WaitUntil(() => FindObjectsByType<VortexPoint>(FindObjectsSortMode.None).Length == 0);
            StopMode();
            OnSpecialPhaseComplete?.Invoke();
        }
    }

    private void SpawnVortexPoint(GameObject prefab)
    {
        if (prefab == null || VortexCenter.Instance == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 centerPos = VortexCenter.Instance.transform.position;

        // Zufälliger Rand-Punkt knapp außerhalb des Bildschirms (analog GravityModeActivationPoint.OffScreenPos).
        int edge = Random.Range(0, 4);
        float u = Random.Range(0.1f, 0.9f);
        Vector2 vp = edge switch
        {
            0 => new Vector2(u,      1.15f),
            1 => new Vector2(1.15f,  u),
            2 => new Vector2(u,     -0.15f),
            _ => new Vector2(-0.15f, u),
        };
        float camZ = Mathf.Abs(cam.transform.position.z);
        Vector3 spawnPos = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, camZ));
        spawnPos.z = 0f;

        Vector2 offset = (Vector2)spawnPos - (Vector2)centerPos;
        float radius = offset.magnitude;
        float angle  = Mathf.Atan2(offset.y, offset.x);

        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        PortalElectrifier.Instance?.ElectrifyElement(obj);

        var vp2 = obj.GetComponent<VortexPoint>();
        if (vp2 != null)
        {
            vp2.Init(this, centerPos, radius, angle, totalSweepDegrees,
                     CurrentTravelDuration(), clockwise ? -1f : 1f, angleEaseExponent);
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
                    spawner.TriggerGameOverFromGravity();
            }
        }
    }

    public void ForceStop()
    {
        if (!isActive) return;
        StopAllCoroutines();
        isActive = false;
        spawnLoopActive = false;
        foreach (var vp in FindObjectsByType<VortexPoint>(FindObjectsSortMode.None))
            Destroy(vp.gameObject);
    }

    /// <summary>Phasenende, Schritt 1: nur den Spawn-Loop stoppen. Mode bleibt aktiv, Restelemente
    /// werden normal zu Ende gespielt.</summary>
    public void StopSpawning()
    {
        spawnLoopActive = false;
    }

    /// <summary>Beendet den Vortex Mode. Wird intern aufgerufen, sobald das Spawn-Limit erreicht ist
    /// UND keine VortexPoints mehr aktiv sind.</summary>
    public void StopMode()
    {
        if (!isActive) return;
        Debug.Log("🌀 Vortex Mode END (StopMode)");

        isActive = false;
        spawnLoopActive = false;
        StopAllCoroutines();
        SpecialModeManager.Instance.EndCurrentMode();
    }
}
