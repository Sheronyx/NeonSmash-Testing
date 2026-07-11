using UnityEngine;

public class GravityPoint : BasePoint
{
    [Header("Movement")]
    [SerializeField] private float fallSpeed = 3f;
    [SerializeField] private float rotationSpeed = -50f;

    [Header("Suck Settings")]
    [SerializeField] private float portalYOffset = -1.0f;
    [SerializeField] private float suckStartDistance = 5f;
    [SerializeField] private float suckSpeed = 5f;
    [SerializeField] private float shrinkSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float minScaleBeforeDestroy = 0.15f;
    [SerializeField] private float maxSuckForce = 10f;

    [Header("Hitbox Offset")]
    [SerializeField] private float baseColliderOffset = 0.3f;  // Offset bei langsamer Geschwindigkeit
    [SerializeField] private float maxColliderOffset  = 1.2f;  // Offset bei hoher Geschwindigkeit
    [SerializeField] private float maxOffsetSpeed     = 15f;   // ab welcher Speed der Max-Offset gilt

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

    private Vector3 initialScale;
    private bool isDestroyed = false;

    // Velocity tracking
    private Vector3 previousPosition;
    private Vector3 actualVelocity;

    private GravityModeSystem gravitySystem;
    private TrailRenderer trail;
    private SpriteRenderer sr;
    private Collider2D col;
    private Transform portalTransform;

    public void Init(GravityModeSystem system)
    {
        gravitySystem = system;
    }

    void Start()
    {
        trail = GetComponent<TrailRenderer>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        initialScale = transform.localScale;
        previousPosition = transform.position;

        var portal = FindFirstObjectByType<ArcanePortalFlash>();
        if (portal != null)
            portalTransform = portal.transform;
    }

    void Update()
    {
        if (portalTransform == null)
        {
            var portal = FindFirstObjectByType<ArcanePortalFlash>();
            if (portal != null)
            {
                portalTransform = portal.transform;
            }
            else
            {
                CheckDestroy();
                return;
            }
        }

        // 🔄 Rotation
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        // 👉 Distanz berechnen
        Vector3 portalTarget = GetPortalTarget();
        float distance = Vector3.Distance(transform.position, portalTarget);

        // 👉 Progress (0 = weit weg, 1 = nah)
        float t = Mathf.Clamp01(1f - (distance / suckStartDistance));

        // 👉 Smooth Übergang
        float eased = Mathf.SmoothStep(0f, 1f, t);

        // ⬇️ normale Bewegung
        Vector3 moveDown = Vector3.down * fallSpeed;

        // 🌀 Richtung Portal
        Vector3 dirToPortal = (portalTarget - transform.position).normalized;

        // 👉 Sogkraft wächst stark zum Ende
        float suctionForce = Mathf.Lerp(0f, maxSuckForce, eased);

        Vector3 moveSuction = dirToPortal * suctionForce;

        // 💥 Bewegung anwenden
        Vector3 finalMove = (moveDown + moveSuction) * Time.deltaTime;
        transform.position += finalMove;

        // ✅ Velocity NACH der Bewegung messen
        if (Time.deltaTime > 0f)
            actualVelocity = (transform.position - previousPosition) / Time.deltaTime;

        previousPosition = transform.position;

        // ✅ Collider-Offset dynamisch nach Geschwindigkeit anpassen
        if (col != null)
        {
            float speed = actualVelocity.magnitude;
            float dynamicOffset = Mathf.Lerp(baseColliderOffset, maxColliderOffset, speed / maxOffsetSpeed);
            col.offset = new Vector2(0f, dynamicOffset);
        }

        // 👉 Scale
        float scaleFactor = Mathf.Lerp(1f, minScaleBeforeDestroy, eased);
        transform.localScale = initialScale * scaleFactor;

        // 👉 Fade
        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, eased);
            sr.color = c;
        }

        // 👉 Trail verkürzen
        if (trail != null)
        {
            trail.time = Mathf.Lerp(0.6f, 0.1f, eased);
        }

        CheckDestroy();
    }

    private Vector3 GetPortalTarget()
    {
        // Z auf die Ebene des Punktes setzen, sonst verfälscht der Z-Abstand
        // (Portal steht bei Z=100) die Distanz und der Sog aktiviert nie.
        Vector3 target = portalTransform.position + Vector3.up * portalYOffset;
        target.z = transform.position.z;
        return target;
    }

private void CheckDestroy()
    {
        if (isDestroyed) return;

        // Punkt ist vom Bildschirm gefallen ohne vom Portal gesaugt zu werden
        Camera cam = Camera.main;
        if (cam != null && cam.WorldToViewportPoint(transform.position).y < -0.1f)
        {
            isDestroyed = true;
            // Shocker durchgelassen = richtig (kein Schaden); normaler Punkt = Miss
            if (!isShocker && !isFake && gravitySystem != null) gravitySystem.OnPointDestroyed(false, transform.position);
            Destroy(gameObject);
            return;
        }

        if (portalTransform == null) return;

        Vector3 portalTarget = GetPortalTarget();
        float distance = Vector3.Distance(transform.position, portalTarget);

        if (distance < 0.15f || (sr != null && sr.color.a < 0.05f))
        {
            isDestroyed = true;
            if (!isShocker && !isFake && gravitySystem != null) gravitySystem.OnPointDestroyed(false, transform.position);
            Destroy(gameObject);
        }
    }

    public void TryTap()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (isFake)
        {
            // Fake getappt = kein Punkt, KEINE Strafe (nur verpufft / Zeitverlust)
            SpawnExplosion();
            Destroy(gameObject);
            return;
        }
        if (isShocker)
        {
            // Shocker getappt = Fehler → Strafe (wie der normale Shocker)
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
            TutorialManager.Instance.OnActionPerformed(TutorialPointType.GravityPoint);

        SpawnExplosion();
        AudioManager.Instance?.PlayNormalPoint();

        if (gravitySystem != null) gravitySystem.OnPointDestroyed(true, transform.position, isBonusDiamond);

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

    /// <summary>Vom GravityModeSystem: setzt die Fallgeschwindigkeit zentral (baseFallSpeed × Intensität),
    /// überschreibt damit den Fallback-Wert auf dem Prefab. Sogkraft bleibt relativ zum Prefab-Wert.</summary>
    public void SetSpeed(float baseFallSpeed, float multiplier)
    {
        fallSpeed = baseFallSpeed * multiplier;
        maxSuckForce *= multiplier;
    }

    public Vector3 GetVelocity()
    {
        return actualVelocity;
    }

    public float GetSpeed()
    {
        return actualVelocity.magnitude;
    }
}