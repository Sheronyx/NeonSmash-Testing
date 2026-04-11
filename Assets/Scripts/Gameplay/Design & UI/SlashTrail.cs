using UnityEngine;

public class SlashTrail : MonoBehaviour
{


    [Header("Trail Prefab")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private GameObject goldTrailPrefab;
    [SerializeField] private GameObject fountainTrailPrefab;

    private bool isGold = false;
    private bool isFountain = false;

    private void OnEnable()
    {
        GoldModeSystem.OnGoldModeStarted += EnableGold;
        GoldModeSystem.OnGoldModeEnded += DisableGold;
        FountainModeSystem.OnFountainModeStarted += EnableFountain;
        FountainModeSystem.OnFountainModeEnded += DisableFountain;
    }

    private void OnDisable()
    {
        GoldModeSystem.OnGoldModeStarted -= EnableGold;
        GoldModeSystem.OnGoldModeEnded -= DisableGold;
        FountainModeSystem.OnFountainModeStarted -= EnableFountain;
        FountainModeSystem.OnFountainModeEnded -= DisableFountain;
    }

    private void EnableGold()    { isGold = true;     ResetActiveTrail(); }
    private void DisableGold()   { isGold = false;    ResetActiveTrail(); }
    private void EnableFountain(){ isFountain = true;  ResetActiveTrail(); }
    private void DisableFountain(){ isFountain = false; ResetActiveTrail(); }

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 2;

    [Header("Trail Einstellungen")]
    [Range(0.01f, 2f)]
    public float width = 0.06f;           // Dicke des Trails
    public float trailTime = 0.15f;       // Lebensdauer (Sekunden)

    private TrailRenderer activeTrail;
    private bool swiping;
    private Vector3 prevPos;

    /// <summary>Swipe beginnt – z.B. bei TouchBegan aufrufen.</summary>
    public void Begin(Vector3 worldPos)
    {
        swiping = true;
        prevPos = worldPos;

        if (activeTrail == null)
        {
            GameObject prefabToUse = isGold ? goldTrailPrefab : isFountain ? fountainTrailPrefab : trailPrefab;

            if (prefabToUse != null)
            {
                GameObject trailObj = Instantiate(prefabToUse);
                activeTrail = trailObj.GetComponent<TrailRenderer>();
            }

            activeTrail.sortingLayerName = sortingLayerName;
            activeTrail.sortingOrder = sortingOrder;
        }

        if (activeTrail != null)
        {
            activeTrail.emitting = false;
            activeTrail.transform.position = worldPos;
            activeTrail.Clear();

            activeTrail.widthMultiplier = width;
            activeTrail.time = trailTime;

            activeTrail.emitting = true;
        }
    }

    /// <summary>Swipe wird bewegt – z.B. bei TouchMove aufrufen.</summary>
    public void Move(Vector3 worldPos)
    {
        if (!swiping || activeTrail == null) return;

        activeTrail.transform.position = worldPos;
        prevPos = worldPos;
    }

    /// <summary>Swipe endet – z.B. bei TouchEnd aufrufen.</summary>
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

    public void SetGoldMode(bool active)
    {
        isGold = active;
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
