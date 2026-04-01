using UnityEngine;
using System.Collections;

public class GoldModeSystem : MonoBehaviour
{
    public static GoldModeSystem Instance;

    [SerializeField] private float duration = 10f; // 👈 EINZIGE QUELLE

    private bool isActive = false;

    public bool IsActive => isActive;
    public float Duration => duration; // 👈 wichtig!

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

        yield return new WaitForSeconds(duration);

        isActive = false;
    }

    public int ModifyPoints(int basePoints)
    {
        return isActive ? basePoints * 2 : basePoints;
    }
}