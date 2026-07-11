using UnityEngine;

public class FountainPoint : BasePoint
{
    [Header("Movement")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float lifetimeAfterLanding = 1f;

    [Header("Shocker / Fake")]
    [Tooltip("Shocker: NICHT tappen! Tappen = Strafe (Leben), Durchlassen = sicher.")]
    [SerializeField] private bool isShocker = false;
    public bool IsShocker => isShocker;
    [Tooltip("Fake: sieht echt aus, ist aber Köder. Tappen = nur verpuffen (kein Punkt, KEINE Strafe), Durchlassen = sicher.")]
    [SerializeField] private bool isFake = false;
    public bool IsFake => isFake;
    [Tooltip("Diamant-Bonus: verhält sich wie ein normales Element, gibt aber beim Tappen den zusätzlichen Diamant-Bonus-Multiplikator.")]
    [SerializeField] private bool isBonusDiamond = false;
    public bool IsBonusDiamond => isBonusDiamond;

    private Vector3 velocity;
    private bool hasLanded = false;
    private bool isDestroyed = false;

    private FountainModeSystem system;

    public void Init(FountainModeSystem sys, Vector3 startVelocity)
    {
        system = sys;
        velocity = startVelocity;
    }

    void Update()
    {
        if (isDestroyed) return;

        velocity.y += gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        // Boden check (y <= -5 z.B. anpassen!)
        if (!hasLanded && transform.position.y <= -5f)
        {
            hasLanded = true;
            Invoke(nameof(DestroyAfterFall), lifetimeAfterLanding);
        }
    }

    private void DestroyAfterFall()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Shocker/Fake durchgelassen (gelandet) = richtig, kein Schaden.
        if (!isShocker && !isFake)
        {
            if (LivesManager.Instance != null)
            {
                bool stillAlive = LivesManager.Instance.LoseLife(transform.position);
                if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
                if (!stillAlive)
                {
                    var spawner = FindFirstObjectByType<MixedPointSpawner>();
                    if (spawner != null) spawner.TriggerGameOverFromGravity();
                }
            }
            if (system != null) system.OnPointFinished(false); // ❌ kein Punkt
        }

        Destroy(gameObject);
    }

    public void TryTap()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (isFake)
        {
            // Fake getappt = kein Punkt, KEINE Strafe (nur verpufft)
            SpawnExplosion();
            Destroy(gameObject);
            return;
        }
        if (isShocker)
        {
            // Shocker getappt = Fehler → Strafe
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

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnActionPerformed(TutorialPointType.FountainPoint);

        SpawnExplosion();
        AudioManager.Instance?.PlayNormalPoint();

        if (system != null) system.OnPointFinished(true, isBonusDiamond, transform.position); // ✅ Punkt

        Destroy(gameObject);
    }

    void ApplyShockerPenalty(Vector3 pos)
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

    /// <summary>Phasenende: Shocker sicher verpuffen lassen (kein Schaden, kein Punkt).</summary>
    public void DissolveNoPenalty()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Destroy(gameObject);
    }
}