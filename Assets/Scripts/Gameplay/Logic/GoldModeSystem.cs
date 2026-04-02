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

    public void Activate()
    {
        if (isActive) return;

        StartCoroutine(Co_GoldMode());
    }

    private IEnumerator Co_GoldMode()
    {
        isActive = true;

        OnGoldModeStarted?.Invoke(); // 🟡 START

        yield return new WaitForSeconds(duration);

        isActive = false;

        OnGoldModeEnded?.Invoke(); // 🔵 END
    }

    public int ModifyPoints(int basePoints)
    {
        return isActive ? basePoints * goldMultiplier : basePoints;
    }
}