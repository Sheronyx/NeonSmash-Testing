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

    private Vector3 initialScale;
    private bool isDestroyed = false;
    private bool isBeingSucked = false;

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
        if (portalTransform == null) return;

        // 🔄 Rotation
        float currentRotation = isBeingSucked ? rotationSpeed * 2.5f : rotationSpeed;
        transform.Rotate(Vector3.forward * currentRotation * Time.deltaTime);

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
        return portalTransform.position + Vector3.up * portalYOffset;
    }

    private void HandleSuckMovement()
    {
        if (portalTransform == null) return;

        Vector3 portalTarget = GetPortalTarget();
        float distance = Vector3.Distance(transform.position, portalTarget);

        float t = Mathf.Clamp01(1f - (distance / suckStartDistance));
        float eased = t * t;

        Vector3 dir = (portalTarget - transform.position).normalized;
        float speed = Mathf.Lerp(1f, maxSuckForce, eased);

        transform.position += dir * speed * Time.deltaTime;

        float scaleFactor = Mathf.Lerp(1f, minScaleBeforeDestroy, eased);
        transform.localScale = initialScale * scaleFactor;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, eased);
            sr.color = c;
        }

        if (trail != null)
        {
            trail.time = Mathf.Lerp(0.6f, 0.1f, eased);
        }
    }

    private void CheckDestroy()
    {
        if (isDestroyed || portalTransform == null) return;

        Vector3 portalTarget = GetPortalTarget();
        float distance = Vector3.Distance(transform.position, portalTarget);

        if (distance < 0.15f || (sr != null && sr.color.a < 0.05f))
        {
            isDestroyed = true;
            gravitySystem?.OnPointDestroyed(false);
            Destroy(gameObject);
        }
    }

    public void TryTap()
    {
        if (isDestroyed) return;

        isDestroyed = true;

        SpawnExplosion();
        AudioManager.Instance?.PlayNormalPoint();

        gravitySystem?.OnPointDestroyed(true);

        Destroy(gameObject);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        fallSpeed *= multiplier;
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