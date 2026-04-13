using UnityEngine;

public class FountainPoint : BasePoint
{
    [Header("Movement")]
    [SerializeField] private float gravity = -15f;
    [SerializeField] private float lifetimeAfterLanding = 1f;

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
        Destroy(gameObject);
    }

    public void TryTap()
    {
        if (isDestroyed) return;

        isDestroyed = true;

        SpawnExplosion();
        AudioManager.Instance?.PlayNormalPoint();

        system?.OnPointFinished(true); // ✅ Punkt

        Destroy(gameObject);
    }
}