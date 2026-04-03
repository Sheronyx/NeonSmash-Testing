using UnityEngine;
using System.Collections;

public class GoldModeSystem : MonoBehaviour
{
    public static event System.Action OnGoldModeStarted;
    public static event System.Action OnGoldModeEnded;

    public static GoldModeSystem Instance;

    [SerializeField] private float duration = 10f;
    [SerializeField] private int goldMultiplier = 3;

    private bool isActive = false;

    public bool IsActive => isActive;
    public float Duration => duration;

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
        {
            StartCoroutine(Co_GoldMode());
        }
    }

    private IEnumerator Co_GoldMode()
    {
        if (isActive) yield break; // 🛡️ Safety

        isActive = true;
        OnGoldModeStarted?.Invoke();

        yield return new WaitForSeconds(duration);

        isActive = false;
        OnGoldModeEnded?.Invoke();

        SpecialModeManager.Instance.EndCurrentMode();
    }

    public int ModifyPoints(int basePoints)
    {
        return isActive ? basePoints * goldMultiplier : basePoints;
    }
}