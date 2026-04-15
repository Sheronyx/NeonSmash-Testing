using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Spielt alle VFX-Graphen gleichzeitig für wenige Frames ab damit ihre
/// Compute-Shader kompiliert sind bevor das erste Gameplay startet.
/// Ein CoverPanel (schwarzes Canvas) verdeckt die Effekte während des Warmups.
/// </summary>
public class VFXWarmup : MonoBehaviour
{
    [Tooltip("Alle VFX-Graph-Prefabs die vorgewärmt werden sollen")]
    [SerializeField] private GameObject[] vfxPrefabs;

    [Tooltip("Frames die jeder Effekt aktiv bleibt (mind. 3, mehr = sicherer)")]
    [SerializeField] private int warmupFrames = 6;

    [Tooltip("Schwarzes Cover-Canvas das während des Warmups alles verdeckt")]
    [SerializeField] private GameObject coverPanel;

    public bool IsComplete { get; private set; } = false;

    private void Start()
    {
        if (coverPanel != null)
            coverPanel.SetActive(true);

        StartCoroutine(Co_Warmup());
    }

    private IEnumerator Co_Warmup()
    {
        Camera cam = Camera.main;
        Vector3 spawnPos = cam != null
            ? cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 1f))
            : Vector3.zero;
        spawnPos.z = 0f;

        var spawned = new System.Collections.Generic.List<GameObject>();
        foreach (var prefab in vfxPrefabs)
        {
            if (prefab == null) continue;
            var go = Instantiate(prefab, spawnPos, Quaternion.identity);
            go.transform.localScale = Vector3.one * 0.0001f;
            var vfx = go.GetComponent<VisualEffect>();
            if (vfx != null) vfx.Play();
            spawned.Add(go);
        }

        for (int i = 0; i < warmupFrames; i++)
            yield return null;

        foreach (var go in spawned)
            if (go != null) Destroy(go);

        // Cover erst deaktivieren wenn Warmup fertig – IntroController übernimmt dann
        if (coverPanel != null)
            coverPanel.SetActive(false);

        IsComplete = true;
    }
}
