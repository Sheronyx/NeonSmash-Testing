using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Orchestrator für den neuen Wellen/Korb-Modus (Fruit-Ninja-Stil, ersetzt den
// alten MixedPointSpawner für GameMode.Infinity). Läuft in derselben Szene wie
// der Multiplayer-Modus, der weiterhin unverändert MixedPointSpawner nutzt.
public class WaveBasketController : MonoBehaviour
{
    public static WaveBasketController Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameUIManager uiManager;
    [SerializeField] private Transform basketAnchor;
    [SerializeField] private Camera mainCamera;

    [Header("Element-Prefabs (1 pro Typ)")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject specialPrefab;
    [SerializeField] private GameObject multiplierPrefab;
    [SerializeField] private GameObject shockerPrefab;
    [SerializeField] private GameObject bombPrefab;

    [Header("Match-Settings")]
    [SerializeField] private float matchDuration = 60f;
    [SerializeField] private int basketCap = 50;

    [Header("Wellen-Timing")]
    [SerializeField] private int minWaveSize = 4;
    [SerializeField] private int maxWaveSize = 10;
    [SerializeField] private float flyInDuration = 0.6f;
    [SerializeField] private float hoverDuration = 2f;

    [Header("Schwebe-Bereich (Viewport, 0..1)")]
    [Range(0f, 0.45f)] [SerializeField] private float hoverAreaLeft = 0.15f;
    [Range(0f, 0.45f)] [SerializeField] private float hoverAreaRight = 0.15f;
    [Range(0f, 0.45f)] [SerializeField] private float hoverAreaTop = 0.15f;
    [Range(0f, 0.45f)] [SerializeField] private float hoverAreaBottom = 0.25f;
    [Tooltip("Wie weit außerhalb des Bildschirms (Viewport-Einheiten) Elemente einfliegen.")]
    [SerializeField] private float edgeOffsetViewport = 0.12f;

    [Header("Abstand (kein Überlappen)")]
    [Tooltip("Mindestabstand (Welteinheiten) zwischen neuer Schwebeposition und allen noch nicht aufgelösten Elementen.")]
    [SerializeField] private float minElementSpacing = 1.2f;
    [Tooltip("Wie oft eine neue Zufallsposition versucht wird, bevor die am wenigsten überlappende genommen wird.")]
    [SerializeField] private int maxPlacementAttempts = 20;

    [Header("Wellen-Zusammensetzung (relative Gewichte)")]
    [Range(0.05f, 1f)] [SerializeField] private float normalSpawnWeight = 1.0f;
    [Range(0.05f, 1f)] [SerializeField] private float specialSpawnWeight = 0.35f;
    [Range(0.05f, 1f)] [SerializeField] private float multiplierSpawnWeight = 0.20f;
    [Range(0.01f, 1f)] [SerializeField] private float shockerSpawnWeight = 0.08f;
    [Range(0.01f, 1f)] [SerializeField] private float bombSpawnWeight = 0.04f;

    /// <summary>(currentCount, cap) — für die Korb-Füllstand-HUD.</summary>
    public System.Action<int, int> OnBasketChanged;
    /// <summary>Verbleibende Sekunden — für die Countdown-HUD.</summary>
    public System.Action<float> OnTimerChanged;
    /// <summary>Feuert einmal, wenn Begin() läuft — z.B. für den Start der Korb-Demolierungs-Sequenz.</summary>
    public System.Action OnMatchStarted;
    /// <summary>Feuert ganz am Anfang von EndGame() mit der cause ("timeout"/"bomb"/"basketFull"),
    /// noch vor Analytics/UI — z.B. damit der Korb bei Timeout/Bombe sofort auf "kaputt" springt,
    /// bei vollem Korb aber im aktuellen Zustand bleibt.</summary>
    public System.Action<string> OnGameEnding;

    public Vector3 BasketWorldPosition => basketAnchor != null ? basketAnchor.position : Vector3.zero;
    public float MatchDuration => matchDuration;

    /// <summary>Für die sanfte Abstoßung: jedes WaveElement schaut hier nach benachbarten
    /// Elementen, um sich bei zu großer Nähe während des Schwebens leicht wegzudrücken.</summary>
    public IReadOnlyList<WaveElement> ActiveElements => _active;

    private bool _running;
    private bool _gameOver;
    private int _basketCount;
    private int _normalCount, _specialCount, _multiplierCount;
    private float _remaining;
    private readonly List<WaveElement> _active = new List<WaveElement>();

    void Awake()
    {
        Instance = this;
        if (!mainCamera) mainCamera = Camera.main;
    }

    public void Begin()
    {
        if (_running) return;
        _running = true;
        _gameOver = false;
        _basketCount = 0;
        _normalCount = 0;
        _specialCount = 0;
        _multiplierCount = 0;
        _remaining = matchDuration;
        _active.Clear();

        MusicManager.Instance?.ResetGameMusicSpeed();

        OnBasketChanged?.Invoke(_basketCount, basketCap);
        OnTimerChanged?.Invoke(_remaining);
        OnMatchStarted?.Invoke();

        StartCoroutine(Co_WaveLoop());
        StartCoroutine(Co_Timer());
    }

    private IEnumerator Co_WaveLoop()
    {
        while (_running && !_gameOver)
        {
            SpawnWave();
            // Nächste Welle startet exakt wenn die aktuelle zu verpuffen beginnt.
            yield return new WaitForSeconds(flyInDuration + hoverDuration);
        }
    }

    private IEnumerator Co_Timer()
    {
        while (_running && !_gameOver && _remaining > 0f)
        {
            _remaining -= Time.deltaTime;
            OnTimerChanged?.Invoke(Mathf.Max(0f, _remaining));
            yield return null;
        }

        if (_running && !_gameOver)
            EndGame("timeout");
    }

    private void SpawnWave()
    {
        int count = Random.Range(minWaveSize, maxWaveSize + 1);
        var types = new WaveElementType[count];
        int badCount = 0;

        for (int i = 0; i < count; i++)
        {
            types[i] = PickWeightedType();
            if (IsObstacle(types[i])) badCount++;
        }

        // Hindernisse (Shocker/Bombe) dürfen in einer Welle nie mehr sein als positive
        // Elemente (Normal/Special/Multiplier) — überschüssige Hindernis-Picks werden
        // auf ein positives Element umgewürfelt.
        int maxBad = count / 2;
        for (int i = 0; i < count && badCount > maxBad; i++)
        {
            if (IsObstacle(types[i]))
            {
                types[i] = PickWeightedPositiveType();
                badCount--;
            }
        }

        for (int i = 0; i < count; i++)
            SpawnOne(types[i]);
    }

    private static bool IsObstacle(WaveElementType type) =>
        type == WaveElementType.Shocker || type == WaveElementType.Bomb;

    private WaveElementType PickWeightedType()
    {
        float total = normalSpawnWeight + specialSpawnWeight + multiplierSpawnWeight
                    + shockerSpawnWeight + bombSpawnWeight;
        float roll = Random.Range(0f, total);

        if ((roll -= normalSpawnWeight) < 0f) return WaveElementType.Normal;
        if ((roll -= specialSpawnWeight) < 0f) return WaveElementType.Special;
        if ((roll -= multiplierSpawnWeight) < 0f) return WaveElementType.Multiplier;
        if ((roll -= shockerSpawnWeight) < 0f) return WaveElementType.Shocker;
        return WaveElementType.Bomb;
    }

    // Wie PickWeightedType(), aber nur unter den positiven Typen — für die Korrektur,
    // wenn eine Welle zu viele Hindernisse gewürfelt hat.
    private WaveElementType PickWeightedPositiveType()
    {
        float total = normalSpawnWeight + specialSpawnWeight + multiplierSpawnWeight;
        float roll = Random.Range(0f, total);

        if ((roll -= normalSpawnWeight) < 0f) return WaveElementType.Normal;
        if ((roll -= specialSpawnWeight) < 0f) return WaveElementType.Special;
        return WaveElementType.Multiplier;
    }

    private void SpawnOne(WaveElementType type)
    {
        if (mainCamera == null) return;

        GameObject prefab = PrefabFor(type);
        if (prefab == null) return;

        Vector3 edgePos = RandomEdgeWorldPosition();
        Vector3 hoverPos = RandomHoverWorldPosition();

        var go = Instantiate(prefab, edgePos, Quaternion.identity);
        var element = go.GetComponent<WaveElement>();
        if (element == null)
        {
            Destroy(go);
            return;
        }

        _active.Add(element);
        element.Init(edgePos, hoverPos, flyInDuration, hoverDuration);
    }

    private GameObject PrefabFor(WaveElementType type) => type switch
    {
        WaveElementType.Normal => normalPrefab,
        WaveElementType.Special => specialPrefab,
        WaveElementType.Multiplier => multiplierPrefab,
        WaveElementType.Shocker => shockerPrefab,
        WaveElementType.Bomb => bombPrefab,
        _ => normalPrefab
    };

    private Vector3 RandomEdgeWorldPosition()
    {
        float camZ = Mathf.Abs(mainCamera.transform.position.z);
        int edge = Random.Range(0, 4); // 0=links, 1=rechts, 2=oben, 3=unten
        float vx, vy;
        switch (edge)
        {
            case 0: vx = -edgeOffsetViewport; vy = Random.Range(0.1f, 0.9f); break;
            case 1: vx = 1f + edgeOffsetViewport; vy = Random.Range(0.1f, 0.9f); break;
            case 2: vx = Random.Range(0.1f, 0.9f); vy = 1f + edgeOffsetViewport; break;
            default: vx = Random.Range(0.1f, 0.9f); vy = -edgeOffsetViewport; break;
        }

        var p = mainCamera.ViewportToWorldPoint(new Vector3(vx, vy, camZ));
        p.z = 0f;
        return p;
    }

    // Rejection Sampling: zufällige Kandidaten würfeln, bis einer weit genug von allen noch
    // NICHT aufgelösten Elementen entfernt ist. Elemente, die schon per Swipe getroffen wurden
    // oder gerade auslösen (IsResolved), werden ignoriert — sie verschwinden ohnehin sofort und
    // stören die neue Welle nicht. Kein Raster nötig, bleibt organisch verteilt.
    private Vector3 RandomHoverWorldPosition()
    {
        float camZ = Mathf.Abs(mainCamera.transform.position.z);

        Vector3 best = Vector3.zero;
        float bestMinDist = -1f;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float vx = Random.Range(hoverAreaLeft, 1f - hoverAreaRight);
            float vy = Random.Range(hoverAreaBottom, 1f - hoverAreaTop);
            Vector3 candidate = mainCamera.ViewportToWorldPoint(new Vector3(vx, vy, camZ));
            candidate.z = 0f;

            float minDistToAny = float.MaxValue;
            bool valid = true;

            foreach (var el in _active)
            {
                if (el == null || el.IsResolved) continue;
                float dist = Vector3.Distance(candidate, el.HoverPosition);
                minDistToAny = Mathf.Min(minDistToAny, dist);
                if (dist < minElementSpacing) { valid = false; break; }
            }

            if (valid) return candidate;
            if (minDistToAny > bestMinDist) { bestMinDist = minDistToAny; best = candidate; }
        }

        return best; // kein perfekter Kandidat gefunden -> bester verfügbarer
    }

    // Vom WaveElement bei erfolgreichem Swipe auf Normal/Special/Multiplier.
    public void OnElementCollected(WaveElementType type)
    {
        if (_gameOver) return;

        switch (type)
        {
            case WaveElementType.Normal: _normalCount++; break;
            case WaveElementType.Special: _specialCount++; break;
            case WaveElementType.Multiplier: _multiplierCount++; break;
        }

        _basketCount++;
        OnBasketChanged?.Invoke(_basketCount, basketCap);

        if (_basketCount >= basketCap)
            EndGame("basketFull");
    }

    // Vom WaveElement bei Bomben-Swipe.
    public void OnBombSliced()
    {
        if (_gameOver) return;
        EndGame("bomb");
    }

    private async void EndGame(string cause)
    {
        if (_gameOver) return;
        _gameOver = true;
        _running = false;

        OnGameEnding?.Invoke(cause);

        StopAllCoroutines();
        SweepActiveElements();

        if (cause != "basketFull" && ScreenShakeManager.Instance != null)
            ScreenShakeManager.Instance.Shake(0.3f, 0.2f);

        InAppReviewManager.Instance?.OnGameFinished();
        MusicManager.Instance?.ResetGameOnGameOver();
        SfxManager.Instance?.PlayInfinityGameOver();

        int score = WaveScoreCalculator.ComputeFinalScore(_normalCount, _specialCount, _multiplierCount);

        NeonAnalytics.LogGameOver(GameMode.Infinity, score, cause);
        AchievementManager.OnGameFinished(score, GameMode.Infinity);
        MissionManager.OnGameFinished(score);

        var breakdown = new WaveResultBreakdown
        {
            NormalCount = _normalCount,
            SpecialCount = _specialCount,
            MultiplierCount = _multiplierCount
        };
        uiManager?.ShowGameOver(score, cause == "basketFull" ? "FINISHED" : "GAME OVER", breakdown);

        try
        {
            bool uploaded = await HighscoreUploader.TrySubmitAsync(score, LeaderboardApi.InfinityId);
            if (uploaded)
                NeonAnalytics.LogHighscoreBeat(GameMode.Infinity, score);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[WaveBasketController] Upload fehlgeschlagen: {e.Message}");
        }
    }

    private void SweepActiveElements()
    {
        foreach (var el in _active)
            if (el != null) Destroy(el.gameObject);
        _active.Clear();
    }
}
