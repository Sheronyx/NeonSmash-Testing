using UnityEngine;

public class SlashTrail : MonoBehaviour
{
    [Header("Trail Prefab")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private GameObject fountainTrailPrefab;

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

    private TrailRenderer activeTrail;
    private bool swiping;
    private Vector3 prevPos;

    public void Begin(Vector3 worldPos)
    {
        swiping = true;
        prevPos = worldPos;

        if (activeTrail == null)
        {
            GameObject prefabToUse = isFountain ? fountainTrailPrefab : trailPrefab;

            if (prefabToUse != null)
            {
                GameObject trailObj = Instantiate(prefabToUse);
                activeTrail = trailObj.GetComponent<TrailRenderer>();
            }

            activeTrail.sortingLayerName = sortingLayerName;
            activeTrail.sortingOrder     = sortingOrder;
        }

        if (activeTrail != null)
        {
            activeTrail.emitting = false;
            activeTrail.transform.position = worldPos;
            activeTrail.Clear();
            activeTrail.widthMultiplier = width;
            activeTrail.time            = trailTime;
            activeTrail.emitting        = true;
        }
    }

    public void Move(Vector3 worldPos)
    {
        if (!swiping || activeTrail == null) return;
        activeTrail.transform.position = worldPos;
        prevPos = worldPos;
    }

    public void End()
    {
        if (!swiping) return;
        swiping = false;
        if (activeTrail != null)
        {
            activeTrail.emitting = false;
            Destroy(activeTrail.gameObject, activeTrail.time + 0.1f);
            activeTrail = null;
        }
    }

    private void ResetActiveTrail()
    {
        if (activeTrail != null)
        {
            Destroy(activeTrail.gameObject);
            activeTrail = null;
        }
    }
}
