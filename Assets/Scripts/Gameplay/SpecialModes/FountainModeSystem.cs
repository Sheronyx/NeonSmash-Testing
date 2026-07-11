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
    [Tooltip("Bewegtes Diamant-Bonus-Element (FountainPoint-Prefab mit isBonusDiamond=true). Nur in Phasen mit erreichtem Diamant-Bonus.")]
    [SerializeField] private GameObject fountainDiamondPrefab;
    [Range(0f, 1f)]
    [Tooltip("Spawn-Chance des Diamant-Bonus-Elements, wenn der Bonus für diese Special-Phase aktiv ist.")]
    [SerializeField] private float diamondBonusChance = 0.15f;
    [Tooltip("Score-Multiplikator eines Diamant-Bonus-Treffers (stapelt mit dem Special-Mode-Multiplikator).")]
    [SerializeField] private int diamondBonusMultiplier = 5;
    [Tooltip("Font-Material des Floating-Score-Texts bei Fountain-Treffern (wie materialPink/Green/Blue bei Normal-Mode-Treffern).")]
    [SerializeField] private Material scoreTextMaterial;
    [SerializeField] private Transform portal;

    /// <summary>Vom PhaseManager VOR dem Orb-Spawn gesetzt — vom nächsten Activate()-Aufruf konsumiert
    /// (egal ob per PhaseManager-Trigger oder automatisch über OnModeStarted).</summary>
    [HideInInspector] public int PendingMaxSpawnCount = -1;
    [HideInInspector] public bool PendingDiamondBonusActive = false;
    [HideInInspector] public float PendingIntensity = 1f;

    private int _spawnedCount = 0;
    private float _intensity = 1f;

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

    [Header("Intensität (Wert kommt pro Phase vom PhaseManager)")]
    [Tooltip("Spawn-Intervall (Sekunden) bei Intensität = 1.0.")]
    [SerializeField] private float baseSpawnInterval = 1.2f;
    [Tooltip("Wie stark das Spawn-Intervall pro Intensitäts-Einheit über 1.0 sinkt (höher = schneller bei steigender Intensität).")]
    [SerializeField] private float spawnIntervalIntensityFactor = 0.2f;
    [Tooltip("Untere Grenze fürs Spawn-Intervall, damit es bei hoher Intensität nicht unspielbar wird.")]
    [SerializeField] private float minSpawnInterval = 0.3f;
    [Tooltip("Wie stark die Schusskraft (Velocity) pro Intensitäts-Einheit über 1.0 steigt.")]
    [SerializeField] private float forceIntensityFactor = 1f;

    private bool isActive = false;
    private bool spawnLoopActive = false;
    public bool IsActive => isActive;

    /// <summary>Gefeuert wenn eine anzahl-limitierte Special-Phase (vom PhaseManager gestartet) fertig
    /// abgespielt ist: Spawn-Limit erreicht UND keine FountainPoints mehr aktiv.</summary>
    public static event System.Action OnSpecialPhaseComplete;

    private MixedPointSpawner spawner;

    public void Activate()
    {
        if (isActive) return;

        int maxSpawnCount       = PendingMaxSpawnCount;
        bool diamondBonusActive = PendingDiamondBonusActive;
        _intensity              = PendingIntensity;
        PendingMaxSpawnCount      = -1;
        PendingDiamondBonusActive = false;
        PendingIntensity          = 1f;

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
        StartCoroutine(SpawnRoutine(maxSpawnCount, diamondBonusActive));
    }

    // Anzahl-basiert (vom PhaseManager): spawnt bis maxSpawnCount erreicht ist (-1 = unlimitiert).
    private IEnumerator SpawnRoutine(int maxSpawnCount = -1, bool diamondBonusActive = false)
    {
        _spawnedCount = 0;

        while (spawnLoopActive)
        {
            // Pro Tick EIN Element: Shocker / Fake / Diamant-Bonus / normal (nicht-überlappende Chancen).
            float r       = Random.value;
            float thunder = spawner != null ? spawner.thunderSpawnChance : 0f;
            float fake    = spawner != null ? spawner.fakeSpawnChance    : 0f;
            float diamond = diamondBonusActive && fountainDiamondPrefab != null ? diamondBonusChance : 0f;

            bool triggeredThunder = false;
            if (fountainShockerPrefab != null && r < thunder)
            { SpawnPoint(fountainShockerPrefab); triggeredThunder = true; }
            else if (fountainFakePrefab != null && r < thunder + fake)
                SpawnPoint(fountainFakePrefab);
            else if (diamond > 0f && r < thunder + fake + diamond)
                SpawnPoint(fountainDiamondPrefab);
            else
                SpawnPoint(ActiveFountainPrefab);

            // Portal-Elektrifizierung: spawner.electricPortalChance ist im neuen Redesign fest auf 0
            // (PhaseManager.ApplyPhaseSettings) — dieser Zweig bleibt daher inaktiv.
            if (!triggeredThunder && spawner != null)
            {
                var pe = PortalElectrifier.Instance;
                if (pe != null && pe.CanActivate() && spawner.electricPortalChance > 0f
                    && Random.value < spawner.electricPortalChance)
                    pe.Activate();
            }

            _spawnedCount++;
            if (maxSpawnCount > 0 && _spawnedCount >= maxSpawnCount)
                spawnLoopActive = false;

            if (spawnLoopActive)
                yield return new WaitForSeconds(GetCurrentSpawnInterval());
        }

        if (maxSpawnCount > 0)
        {
            yield return new WaitUntil(() => FindObjectsByType<FountainPoint>(FindObjectsSortMode.None).Length == 0);
            StopMode();
            OnSpecialPhaseComplete?.Invoke();
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

        velocity *= 1f + (_intensity - 1f) * forceIntensityFactor; // Intensität der aktuellen Special-Phase (PhaseManager)

        var go = Instantiate(prefab, pos, Quaternion.identity);
        PortalElectrifier.Instance?.ElectrifyElement(go);
        var point = go.GetComponent<FountainPoint>();

        if (prefab == ActiveFountainPrefab && TutorialManager.Instance != null)
            TutorialManager.Instance.OnElementSpawnedShowOverlay(TutorialPointType.FountainPoint, pos);

        point.Init(this, velocity);
    }

    private float GetCurrentSpawnInterval() =>
        Mathf.Max(minSpawnInterval, baseSpawnInterval - (_intensity - 1f) * spawnIntervalIntensityFactor);

    public void OnPointFinished(bool hit, bool isBonusDiamond = false, Vector3 position = default)
    {
        if (hit)
            SpecialModeManager.RegisterSpecialHit(isBonusDiamond ? diamondBonusMultiplier : 1, position, scoreTextMaterial);
        else
            SpecialModeManager.RegisterSpecialMiss();
    }

    /// <summary>Beendet den Fountain Mode. Wird intern aufgerufen, sobald das Spawn-Limit erreicht ist
    /// UND keine FountainPoints mehr aktiv sind (kein manuelles Aufräumen nötig).</summary>
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
