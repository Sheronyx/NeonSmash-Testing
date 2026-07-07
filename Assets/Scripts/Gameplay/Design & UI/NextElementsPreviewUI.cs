using System.Collections;
using UnityEngine;

/// <summary>
/// Zeigt die 3 nächsten Spawn-Farben aus _previewColors (MixedPointSpawner).
/// Icon 0/1/2 entsprechen Slot 0/1/2. Wenn Slot i spawnt, animiert nur Icon i.
/// </summary>
public class NextElementsPreviewUI : MonoBehaviour
{
    [Header("Prefabs pro Element-Typ")]
    [SerializeField] private GameObject prefabPink;
    [SerializeField] private GameObject prefabBlue;
    [SerializeField] private GameObject prefabGreen;
    [SerializeField] private GameObject prefabOrange;
    [SerializeField] private GameObject prefabDeath;

    [Header("Ankerposition — Preview-Box RectTransform hier reinziehen")]
    [SerializeField] private RectTransform anchorRect;
    [SerializeField] private float worldSpacing = 0.8f;
    [SerializeField] private float iconScale    = 0.5f;
    [SerializeField] private float worldZ       = 0f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 100;

    [Header("Animation")]
    [SerializeField] private float swapDuration = 0.2f;

    private GameObject[] _icons      = new GameObject[3];
    private Coroutine[]  _routines   = new Coroutine[3];

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (MixedPointSpawner.Instance != null)
        {
            MixedPointSpawner.Instance.OnSlotColorAssigned += HandleSlotSpawned;
            RefreshImmediate();
        }
    }

    private void OnDisable()
    {
        if (MixedPointSpawner.Instance != null)
            MixedPointSpawner.Instance.OnSlotColorAssigned -= HandleSlotSpawned;
    }

    private void Start()
    {
        if (MixedPointSpawner.Instance != null)
        {
            MixedPointSpawner.Instance.OnSlotColorAssigned -= HandleSlotSpawned;
            MixedPointSpawner.Instance.OnSlotColorAssigned += HandleSlotSpawned;
            RefreshImmediate();
        }
    }

    // ─── Setup ────────────────────────────────────────────────────────────────

    private void RefreshImmediate()
    {
        StopAllCoroutines();
        for (int i = 0; i < 3; i++) _routines[i] = null;

        if (MixedPointSpawner.Instance == null) return;

        var colors = MixedPointSpawner.Instance.PeekUpcomingColors(3);
        for (int i = 0; i < 3; i++)
        {
            if (_icons[i] != null) { Destroy(_icons[i]); _icons[i] = null; }
            if (i < colors.Length)
                _icons[i] = SpawnIcon(colors[i], SlotWorldPos(i));
        }
    }

    // ─── Slot-Swap ────────────────────────────────────────────────────────────

    // Feuert wenn Slot i spawnt — _previewColors[i] ist zu diesem Zeitpunkt bereits die NEUE Farbe
    private void HandleSlotSpawned(int slotIndex, PointColor _)
    {
        if (!gameObject.activeInHierarchy || slotIndex < 0 || slotIndex >= 3) return;

        PointColor newPreviewColor = MixedPointSpawner.Instance.PeekUpcomingColors(3)[slotIndex];

        if (_routines[slotIndex] != null) StopCoroutine(_routines[slotIndex]);
        _routines[slotIndex] = StartCoroutine(Co_Swap(slotIndex, newPreviewColor));
    }

    private IEnumerator Co_Swap(int slotIndex, PointColor newColor)
    {
        float half = swapDuration * 0.5f;
        Vector3 targetPos = SlotWorldPos(slotIndex);

        // Phase 1: altes Icon schrumpft raus
        GameObject old = _icons[slotIndex];
        if (old != null)
        {
            Vector3 startScale = old.transform.localScale;
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                old.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
                SetAlpha(old, 1f - k);
                yield return null;
            }
            Destroy(old);
            _icons[slotIndex] = null;
        }

        // Phase 2: neues Icon wächst rein
        GameObject neo = SpawnIcon(newColor, targetPos);
        _icons[slotIndex] = neo;
        if (neo != null)
        {
            neo.transform.localScale = Vector3.zero;
            SetAlpha(neo, 0f);

            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k  = Mathf.Clamp01(t / half);
                float ek = 1f - Mathf.Pow(1f - k, 2f);
                neo.transform.localScale = Vector3.one * iconScale * Mathf.Lerp(0f, 1.12f, ek);
                SetAlpha(neo, k);
                yield return null;
            }
            // Kleiner Settle-Bounce
            float bt = 0f;
            while (bt < 0.08f)
            {
                bt += Time.unscaledDeltaTime;
                neo.transform.localScale = Vector3.one * iconScale *
                    Mathf.Lerp(1.12f, 1f, bt / 0.08f);
                yield return null;
            }
            neo.transform.localScale = Vector3.one * iconScale;
            SetAlpha(neo, 1f);
        }

        _routines[slotIndex] = null;
    }

    // ─── Hilfsmethoden ────────────────────────────────────────────────────────

    private GameObject SpawnIcon(PointColor color, Vector3 worldPos)
    {
        GameObject prefab = GetPrefab(color);
        if (prefab == null) return null;

        var go = Instantiate(prefab, worldPos, Quaternion.identity);
        go.transform.localScale = Vector3.one * iconScale;

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder     = sortingOrder;
        }
        foreach (var col in go.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        return go;
    }

    private Vector3 SlotWorldPos(int slotIndex)
    {
        Vector3 center = anchorRect != null
            ? anchorRect.position
            : Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.1f,
                Camera.main != null ? -Camera.main.transform.position.z : 10f));
        center.z = worldZ;
        float startX = center.x - worldSpacing;
        return new Vector3(startX + slotIndex * worldSpacing, center.y, worldZ);
    }

    private void SetAlpha(GameObject go, float alpha)
    {
        if (go == null) return;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
        { var c = sr.color; c.a = alpha; sr.color = c; }
    }

    private GameObject GetPrefab(PointColor pc) => pc switch
    {
        PointColor.Pink   => prefabPink,
        PointColor.Blue   => prefabBlue,
        PointColor.Green  => prefabGreen,
        PointColor.Orange => prefabOrange,
        PointColor.Death  => prefabDeath,
        _                 => null
    };
}
