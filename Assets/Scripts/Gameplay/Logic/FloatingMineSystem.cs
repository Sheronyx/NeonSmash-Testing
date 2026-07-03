using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingMineSystem : MonoBehaviour
{
    public static FloatingMineSystem Instance { get; private set; }

    [SerializeField] private GameObject minePrefab;
    [SerializeField] private int        mineCount      = 2;

    [Header("Spawn-Bereich (Viewport 0-1)")]
    [SerializeField] private float spawnYMin = 0.30f;
    [SerializeField] private float spawnYMax = 0.72f;
    [SerializeField] private float spawnXVariance = 0.10f;  // Zufallsversatz pro Mine in X

    private readonly List<FloatingMine> _mines = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void SpawnMines()
    {
        ForceRemove();

        if (minePrefab == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        for (int i = 0; i < mineCount; i++)
        {
            // Bildschirm gleichmäßig in Spalten aufteilen, kleine Zufallsverschiebung in X
            float xRatio = (i + 0.5f) / mineCount + Random.Range(-spawnXVariance, spawnXVariance);
            float yRatio = Random.Range(spawnYMin, spawnYMax);

            Vector3 worldPos = cam.ViewportToWorldPoint(
                new Vector3(xRatio, yRatio, Mathf.Abs(cam.transform.position.z)));
            worldPos.z = 0f;

            var go   = Instantiate(minePrefab, worldPos, Quaternion.identity);
            var mine = go.GetComponent<FloatingMine>();
            if (mine == null) continue;

            mine.Enter(worldPos);
            _mines.Add(mine);
        }
    }

    // Startet Exit-Animationen; await in einer Coroutine um auf Abschluss zu warten.
    public IEnumerator Co_RemoveMines()
    {
        if (_mines.Count == 0) yield break;

        int pending = 0;
        foreach (var mine in _mines)
        {
            if (mine == null) continue;
            pending++;
            mine.Exit(() => pending--);
        }
        _mines.Clear();

        yield return new WaitUntil(() => pending <= 0);
    }

    public void ForceRemove()
    {
        foreach (var m in _mines)
            if (m != null) Destroy(m.gameObject);
        _mines.Clear();
    }
}
