using System.Collections;
using UnityEngine;

// Finale "Endphase" statt endlosem Weiterlaufen (siehe PhaseManager.AdvanceToNextNormalPhase): ein
// großer Kristall erscheint mittig, darauf spawnt IMMER GENAU EIN Kristall-Element (Tap oder Swipe,
// zufällig) innerhalb des Kristall-Sprites — kein Bezug zu PointColor/Fairies, kein Shocker, kein
// Diamant. Bei Treffer: feste Punktzahl, sofort das nächste Element. Verpasst man eins (eigene
// Reaktionszeit) ODER läuft die feste Gesamtzeit der Phase ab: Game Over (normaler Flow, wie
// Timeout/Special-Mode — kein Sonderfall in der UI).
public class CrystalEndPhaseSystem : MonoBehaviour
{
    public static CrystalEndPhaseSystem Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject crystalPrefab;
    [SerializeField] private GameObject crystalTapElementPrefab;
    [SerializeField] private GameObject crystalSwipeElementPrefab;

    [Header("Spawn-Bereich auf dem Kristall")]
    [Tooltip("Manuell eingestellter Bereich statt der Sprite-Bounds (die haben oft viel transparenten Rand) — " +
             "Breite/Höhe in Welteinheiten, zentriert um die Kristall-Position + Offset. Im Scene-View als " +
             "Gizmo sichtbar, wenn dieses Objekt ausgewählt ist.")]
    [SerializeField] private Vector2 spawnAreaSize = new Vector2(2f, 2f);
    [Tooltip("Versatz des Spawn-Bereichs von der Kristall-Position, falls das sichtbare Motiv nicht exakt mittig im Sprite sitzt.")]
    [SerializeField] private Vector2 spawnAreaOffset = Vector2.zero;

    [Header("Timing")]
    [Tooltip("Gesamtdauer der Endphase — danach ist das Spiel vorbei, egal ob gerade ein Element aktiv ist.")]
    [SerializeField] private float phaseDuration = 10f;
    [Tooltip("Reaktionszeit pro Kristall-Element — verpasst, ist das Spiel sofort vorbei (auch vor Ablauf der Gesamtzeit).")]
    [SerializeField] private float elementReactionTime = 1.5f;

    [Header("Scoring")]
    [SerializeField] private int pointsPerHit = 50;
    public int PointsPerHit => pointsPerHit;

    [Range(0f, 1f)]
    [SerializeField] private float swipeChance = 0.33f;

    private MixedPointSpawner spawner;
    private GameObject crystalInstance;
    private Coroutine elementTimeoutRoutine;
    private Coroutine phaseTimerRoutine;
    private bool phaseActive = false;
    private bool ended = false;

    private void Awake() => Instance = this;

    /// <summary>Vom PhaseManager aufgerufen, sobald alle regulären Phasen durchlaufen sind (ersetzt
    /// das frühere endlose Wiederholen der letzten 2 Phasen).</summary>
    public void Begin(MixedPointSpawner spawnerRef)
    {
        if (phaseActive || spawnerRef == null) return;

        spawner = spawnerRef;
        phaseActive = true;
        ended = false;

        spawner.PauseSpawning(true);
        spawner.ClearAllGameplayPoints();
        spawner.SetCrystalPhaseActive(true);

        if (crystalPrefab != null)
        {
            Vector3 centerPos = GetScreenCenterWorldPos();
            crystalInstance = Instantiate(crystalPrefab, centerPos, Quaternion.identity);
        }

        phaseTimerRoutine = StartCoroutine(Co_PhaseTimer());
        SpawnNextElement();
    }

    private Vector3 GetScreenCenterWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;
        float camZ = Mathf.Abs(cam.transform.position.z);
        Vector3 pos = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, camZ));
        pos.z = 0f;
        return pos;
    }

    private IEnumerator Co_PhaseTimer()
    {
        yield return new WaitForSeconds(phaseDuration);
        EndWithGameOver();
    }

    private void SpawnNextElement()
    {
        if (!phaseActive || ended) return;

        bool isSwipe = Random.value < swipeChance;
        GameObject prefab = isSwipe ? crystalSwipeElementPrefab : crystalTapElementPrefab;
        if (prefab == null) prefab = crystalTapElementPrefab != null ? crystalTapElementPrefab : crystalSwipeElementPrefab;
        if (prefab == null) return; // weder Tap- noch Swipe-Prefab zugewiesen — nichts spawnbar

        Vector3 pos = GetRandomPositionOnCrystal();
        spawner.CreateCrystalPoint(prefab, pos);

        if (elementTimeoutRoutine != null) StopCoroutine(elementTimeoutRoutine);
        elementTimeoutRoutine = StartCoroutine(Co_ElementTimeout());
    }

    private Vector3 GetSpawnAreaCenter()
    {
        Vector3 basePos = crystalInstance != null ? crystalInstance.transform.position : GetScreenCenterWorldPos();
        return basePos + new Vector3(spawnAreaOffset.x, spawnAreaOffset.y, 0f);
    }

    private Vector3 GetRandomPositionOnCrystal()
    {
        Vector3 center = GetSpawnAreaCenter();
        float x = center.x + Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f);
        float y = center.y + Random.Range(-spawnAreaSize.y * 0.5f, spawnAreaSize.y * 0.5f);
        return new Vector3(x, y, 0f);
    }

    // Nur Editor-Hilfe: zeigt den eingestellten Spawn-Bereich als Rechteck im Scene-View, damit er
    // sich ohne Play-Testing gegen das Kristall-Sprite kalibrieren lässt.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = GetSpawnAreaCenter();
        Gizmos.DrawWireCube(center, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));
    }

    private IEnumerator Co_ElementTimeout()
    {
        yield return new WaitForSeconds(elementReactionTime);
        EndWithGameOver();
    }

    /// <summary>Von MixedPointSpawner.HandleCrystalPointHit aufgerufen, sobald das aktuelle
    /// Kristall-Element getroffen wurde.</summary>
    public void OnElementHit()
    {
        if (!phaseActive || ended) return;

        if (elementTimeoutRoutine != null) { StopCoroutine(elementTimeoutRoutine); elementTimeoutRoutine = null; }
        SpawnNextElement();
    }

    private void EndWithGameOver()
    {
        if (!phaseActive || ended) return;
        ended = true;

        if (elementTimeoutRoutine != null) { StopCoroutine(elementTimeoutRoutine); elementTimeoutRoutine = null; }
        if (phaseTimerRoutine != null) { StopCoroutine(phaseTimerRoutine); phaseTimerRoutine = null; }

        spawner?.TriggerGameOverFromCrystalPhase();
    }

    /// <summary>Räumt auf, falls das Spiel über einen ANDEREN Weg endet, während die Kristallphase
    /// noch läuft (siehe MixedPointSpawner.EndGame) — verhindert eine hängende Coroutine/Kristall-Leiche.</summary>
    public void ForceStop()
    {
        if (!phaseActive) return;

        phaseActive = false;
        ended = true;

        if (elementTimeoutRoutine != null) { StopCoroutine(elementTimeoutRoutine); elementTimeoutRoutine = null; }
        if (phaseTimerRoutine != null) { StopCoroutine(phaseTimerRoutine); phaseTimerRoutine = null; }

        if (crystalInstance != null) Destroy(crystalInstance);
        crystalInstance = null;

        spawner?.SetCrystalPhaseActive(false);
    }
}
