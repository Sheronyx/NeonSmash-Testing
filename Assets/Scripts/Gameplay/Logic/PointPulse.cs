using UnityEngine;

public class PointPulse : MonoBehaviour
{
    [SerializeField] private float pulseAmount = 0.15f;  // 15% größer/kleiner
    [SerializeField] private float pulseSpeed = 2.5f;    // wie schnell pulsiert

    private Vector3 baseScale;
    private bool isPulsing = false;
    private float pulseTimer = 0f;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    // Manche Aufrufer (z.B. DiamondBonusIndicatorUI) setzen localScale kurz auf 0, während das
    // GameObject selbst noch deaktiviert ist — Awake() läuft dann erst NACHTRÄGLICH beim ersten
    // SetActive(true) und würde fälschlich baseScale=0 einfrieren. Explizites Setzen unmittelbar
    // vor StartPulsing() umgeht dieses fragile Awake-Timing.
    public void SetBaseScale(Vector3 scale) => baseScale = scale;

    public void StartPulsing()
    {
        // Lokale Phase statt Time.time: startet garantiert bei Sinus-Phase 0 (= exakt baseScale),
        // sonst würde die Skalierung beim Umschalten von Einflug/Pop-In auf Pulsieren auf eine
        // zufällige Phase springen — sichtbarer Ruckler im Übergang.
        pulseTimer = 0f;
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

        pulseTimer += Time.deltaTime;
        float pulse = 1f + Mathf.Sin(pulseTimer * Mathf.PI * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
