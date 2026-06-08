using UnityEngine;

public class PointPulse : MonoBehaviour
{
    [SerializeField] private float pulseAmount = 0.15f;  // 15% größer/kleiner
    [SerializeField] private float pulseSpeed = 2.5f;    // wie schnell pulsiert

    private Vector3 baseScale;
    private bool isPulsing = false;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public void StartPulsing()
    {
        isPulsing = true;
    }

    public void StopPulsing()
    {
        isPulsing = false;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (!isPulsing) return;

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.PI * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
