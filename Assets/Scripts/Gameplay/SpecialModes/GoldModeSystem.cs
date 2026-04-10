using UnityEngine;

public class GoldModeSystem : MonoBehaviour
{
    public static event System.Action OnGoldModeStarted;
    public static event System.Action OnGoldModeEnded;

    public static GoldModeSystem Instance;

    [SerializeField] private MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private int elementCount = 15;
    [SerializeField] private int goldMultiplier = 3;

    private bool isActive = false;
    private int remaining;

    public bool IsActive => isActive;

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
        if (mode == SpecialMode.Gold)
            Activate();
    }

    private void Activate()
    {
        if (isActive) return;

        isActive = true;
        remaining = elementCount;

        OnGoldModeStarted?.Invoke();
    }

    public void OnGoldPointHit()
    {
        if (!isActive) return;

        remaining--;

        if (remaining <= 0)
            EndMode();
    }

    private void EndMode()
    {
        isActive = false;
        OnGoldModeEnded?.Invoke();
        SpecialModeManager.Instance.EndCurrentMode();
    }

    public int ModifyPoints(int basePoints)
    {
        return isActive ? basePoints * goldMultiplier : basePoints;
    }
}
