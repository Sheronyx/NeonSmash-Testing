using UnityEngine;

// Vortex-Element: fliegt vom Bildschirmrand auf einer Spiralkurve (fester Schwenkwinkel, einheitliche
// Drehrichtung für alle Elemente) zum VortexCenter, wo es eingesaugt wird (Schrumpf+Fade wie bei Gravity).
public class VortexPoint : BasePoint
{
    [Header("Shocker / Fake / Diamant-Bonus")]
    [Tooltip("Shocker: NICHT tappen! Tappen = Strafe (Leben), Einsaugen = sicher.")]
    [SerializeField] private bool isShocker = false;
    public bool IsShocker => isShocker;
    [Tooltip("Fake: sieht echt aus, ist aber Köder. Tappen = nur verpuffen (kein Punkt, KEINE Strafe), Einsaugen = sicher.")]
    [SerializeField] private bool isFake = false;
    public bool IsFake => isFake;
    [Tooltip("Diamant-Bonus: verhält sich wie ein normales Element, gibt aber beim Tappen den zusätzlichen Diamant-Bonus-Multiplikator.")]
    [SerializeField] private bool isBonusDiamond = false;
    public bool IsBonusDiamond => isBonusDiamond;

    [Header("Einsaugen")]
    [Tooltip("Ab welchem Reise-Fortschritt (0-1) das Schrumpfen/Faden vorm Einsaugen beginnt.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float suckStartProgress = 0.75f;
    [SerializeField] private float minScaleBeforeDestroy = 0.15f;
    [SerializeField] private float rotationSpeed = -50f;

    private Vector3 initialScale;
    private bool isDestroyed = false;
    private SpriteRenderer sr;

    private VortexModeSystem system;
    private Vector3 center;
    private float initialRadius;
    private float initialAngle;
    private float sweepRadians;
    private float travelDuration;
    private float angleEaseExponent = 2f;
    private float t;

    /// <summary>rotationSign: -1 = im Uhrzeigersinn, +1 = gegen den Uhrzeigersinn (Standard-Mathe-Konvention, Y nach oben).
    /// angleEaseExponent: 1 = Winkel läuft linear (Kurve setzt sofort ein), &gt;1 = Kurve setzt später/kräftiger ein
    /// (2 = quadratisch), &lt;1 = Kurve setzt früher ein.</summary>
    public void Init(VortexModeSystem sys, Vector3 vortexCenter, float radius, float angle,
                      float sweepDegrees, float duration, float rotationSign, float angleEaseExp = 2f)
    {
        system = sys;
        center = vortexCenter;
        initialRadius = radius;
        initialAngle = angle;
        sweepRadians = sweepDegrees * Mathf.Deg2Rad * rotationSign;
        travelDuration = Mathf.Max(0.1f, duration);
        angleEaseExponent = Mathf.Max(0.1f, angleEaseExp);
        t = 0f;
    }

    void Start()
    {
        initialScale = transform.localScale;
        sr = GetComponentInChildren<SpriteRenderer>();
        ApplyProgress(0f);
    }

    void Update()
    {
        if (isDestroyed) return;

        t += Time.deltaTime / travelDuration;
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        if (t >= 1f)
        {
            Suck();
            return;
        }

        ApplyProgress(t);
    }

    private void ApplyProgress(float progress)
    {
        // Radius schrumpft STETIG (linear) — kein plötzlicher Einbruch erst spät.
        // Winkel schwenkt per Ease-In (Exponent einstellbar über VortexModeSystem) — die Kurve
        // passiert dadurch vor allem NICHT direkt am (kaum sichtbaren) Rand.
        float radius = initialRadius * (1f - progress);
        float angle  = initialAngle + sweepRadians * Mathf.Pow(progress, angleEaseExponent);

        Vector3 pos = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        pos.z = 0f;
        transform.position = pos;

        if (progress >= suckStartProgress)
        {
            float k = (progress - suckStartProgress) / (1f - suckStartProgress);
            transform.localScale = initialScale * Mathf.Lerp(1f, minScaleBeforeDestroy, k);
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, k);
                sr.color = c;
            }
        }
    }

    // Erreicht das Vortex-Zentrum ohne getappt worden zu sein.
    private void Suck()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (!isShocker && !isFake && system != null)
            system.OnPointDestroyed(false, transform.position);

        Destroy(gameObject);
    }

    public void TryTap()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (isFake)
        {
            SpawnExplosion();
            Destroy(gameObject);
            return;
        }
        if (isShocker)
        {
            ApplyShockerPenalty(transform.position);
            Destroy(gameObject);
            return;
        }

        if (PortalElectrifier.IsActive)
        {
            SpawnExplosion();
            ApplyShockerPenalty(transform.position);
            Destroy(gameObject);
            return;
        }

        SpawnExplosion();
        AudioManager.Instance?.PlayNormalPoint();

        if (system != null) system.OnPointDestroyed(true, transform.position, isBonusDiamond);

        Destroy(gameObject);
    }

    private void ApplyShockerPenalty(Vector3 pos)
    {
        if (LivesManager.Instance != null)
        {
            bool stillAlive = LivesManager.Instance.LoseLife(pos);
            if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
            if (!stillAlive)
            {
                var spawner = FindFirstObjectByType<MixedPointSpawner>();
                spawner?.TriggerGameOverFromGravity();
            }
        }
    }

    /// <summary>Phasenende: sicher verpuffen lassen (kein Schaden, kein Punkt).</summary>
    public void DissolveNoPenalty()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Destroy(gameObject);
    }
}
