using UnityEngine;

public class SlashTrail : MonoBehaviour
{
    [Header("Trail Prefab")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private GameObject fountainTrailPrefab;
    [SerializeField] private GameObject electricTrailPrefab;

    private bool isFountain = false;

    private void OnEnable()
    {
        FountainModeSystem.OnFountainModeStarted += EnableFountain;
        FountainModeSystem.OnFountainModeEnded   += DisableFountain;
    }

    private void OnDisable()
    {
        FountainModeSystem.OnFountainModeStarted -= EnableFountain;
        FountainModeSystem.OnFountainModeEnded   -= DisableFountain;
    }

    private void EnableFountain()  { isFountain = true;  ResetActiveTrail(); }
    private void DisableFountain() { isFountain = false; ResetActiveTrail(); }

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 2;

    [Header("Trail Einstellungen")]
    [Range(0.01f, 2f)]
    public float width = 0.06f;
    public float trailTime = 0.15f;

    [Header("Electric Trail Einstellungen (Combo >= 10)")]
    [Range(0.01f, 2f)]
    public float electricWidth = 1.0f;
    public float electricTrailTime = 1.0f;

    private GameObject activeTrailObject;
    private TrailRenderer activeTrail;
    private TrailRenderer[] activeTrails;
    private bool swiping;
    private bool isElectricTrail;
    private Vector3 prevPos;

    public void Begin(Vector3 worldPos)
    {
        swiping = true;
        prevPos = worldPos;

        if (activeTrail == null)
        {
            GameObject prefabToUse = null;
            isElectricTrail = false;

            if (isFountain)
            {
                prefabToUse = fountainTrailPrefab;
            }
            else if (ComboManager.Instance != null && ComboManager.Instance.Multiplier >= 10f && electricTrailPrefab != null)
            {
                prefabToUse = electricTrailPrefab;
                isElectricTrail = true;
                Debug.Log("[SlashTrail] ELECTRIC TRAIL aktiviert! Combo=" + ComboManager.Instance.Multiplier);
            }
            else
            {
                prefabToUse = SkinManager.Instance?.ActiveTheme?.slashTrailPrefab ?? trailPrefab;
            }

            if (prefabToUse != null)
            {
                activeTrailObject = Instantiate(prefabToUse);
                activeTrailObject.transform.position = worldPos;
                Debug.Log($"[SlashTrail] Trail spawned: {activeTrailObject.name}, Pos={worldPos}, IsElectric={isElectricTrail}");

                activeTrail = activeTrailObject.GetComponent<TrailRenderer>();
                if (activeTrail == null)
                {
                    activeTrail = activeTrailObject.GetComponentInChildren<TrailRenderer>();
                }

                activeTrails = activeTrailObject.GetComponentsInChildren<TrailRenderer>();
                Debug.Log($"[SlashTrail] {activeTrails.Length} TrailRenderer gefunden");
            }

            if (activeTrail != null)
            {
                activeTrail.sortingLayerName = sortingLayerName;
                activeTrail.sortingOrder     = sortingOrder;
            }

            if (activeTrails != null)
            {
                foreach (TrailRenderer tr in activeTrails)
                {
                    tr.sortingLayerName = sortingLayerName;
                    tr.sortingOrder     = sortingOrder;
                }
            }
        }

        if (activeTrail != null)
        {
            activeTrail.emitting = false;
            activeTrail.Clear();

            // Nutze Electric-Werte wenn Electric Trail, sonst normale Werte
            if (isElectricTrail)
            {
                activeTrail.widthMultiplier = electricWidth;
                activeTrail.time = electricTrailTime;
            }
            else
            {
                activeTrail.widthMultiplier = width;
                activeTrail.time = trailTime;
            }

            activeTrail.emitting = true;
        }

        if (activeTrails != null)
        {
            foreach (TrailRenderer tr in activeTrails)
            {
                tr.emitting = false;
                tr.Clear();

                if (isElectricTrail)
                {
                    tr.widthMultiplier = electricWidth;
                    tr.time = electricTrailTime;
                }
                else
                {
                    tr.widthMultiplier = width;
                    tr.time = trailTime;
                }

                tr.emitting = true;
            }
        }
    }

    public void Move(Vector3 worldPos)
    {
        if (!swiping || activeTrailObject == null) return;
        activeTrailObject.transform.position = worldPos;
        prevPos = worldPos;
    }

    public void End()
    {
        if (!swiping) return;
        swiping = false;
        if (activeTrail != null)
        {
            activeTrail.emitting = false;
            Destroy(activeTrailObject, activeTrail.time + 0.1f);
            activeTrail = null;
            activeTrails = null;
            activeTrailObject = null;
        }
    }

    private void ResetActiveTrail()
    {
        if (activeTrailObject != null)
        {
            Destroy(activeTrailObject);
            activeTrail = null;
            activeTrails = null;
            activeTrailObject = null;
        }
    }
}
