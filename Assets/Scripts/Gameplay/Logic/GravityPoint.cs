using UnityEngine;

public class GravityPoint : BasePoint
{
    [Header("Movement")]
    [SerializeField] private float fallSpeed = 3f;
    [SerializeField] private float rotationSpeed = -50f;
    

    [Header("Suck Settings")]
    [SerializeField] private float portalYOffset = -1.0f;
    [SerializeField] private float suckStartDistance = 5f; // 👈 wann Sog startet
    [SerializeField] private float suckSpeed = 5f;
    [SerializeField] private float shrinkSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float minScaleBeforeDestroy = 0.15f;
[SerializeField] private float maxSuckForce = 10f;

private Vector3 initialScale;
    private bool isDestroyed = false;
    private bool isBeingSucked = false;

    private GravityModeSystem gravitySystem;
    private TrailRenderer trail;
    private SpriteRenderer sr;
    private Transform portalTransform;

    public void Init(GravityModeSystem system)
    {
        gravitySystem = system;
    }

void Start()
{
    trail = GetComponent<TrailRenderer>();
    sr = GetComponent<SpriteRenderer>();

    initialScale = transform.localScale; // 👈 WICHTIG

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

    // 💥 BEIDES kombinieren!
    Vector3 finalMove = (moveDown + moveSuction) * Time.deltaTime;

    transform.position += finalMove;

    // 👉 Scale (jetzt korrekt!)
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

    // 👉 Progress (0 → 1)
    float t = Mathf.Clamp01(1f - (distance / suckStartDistance));

    // 👉 Smooth easing (wichtig!)
    float eased = t * t; // weich starten

    // 👉 Richtung
Vector3 dir = (portalTarget - transform.position).normalized;

    // 👉 Geschwindigkeit wächst
    float speed = Mathf.Lerp(1f, maxSuckForce, eased);

    transform.position += dir * speed * Time.deltaTime;

    // 👉 Scale wird stärker zum Ende hin
    float scaleFactor = Mathf.Lerp(1f, minScaleBeforeDestroy, eased);
transform.localScale = initialScale * scaleFactor;
    // 👉 Fade auch progressiv
    if (sr != null)
    {
        Color c = sr.color;
        c.a = Mathf.Lerp(1f, 0f, eased);
        sr.color = c;
    }

    // 👉 Trail kürzer am Ende
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

        // 👉 entweder nah genug ODER unsichtbar
        if (distance < 0.15f || (sr != null && sr.color.a < 0.05f))
        {
            isDestroyed = true;

            // ❗ kein Explosion beim Portal → fühlt sich besser an
            gravitySystem?.OnPointDestroyed(false);

            Destroy(gameObject);
        }
    }

    public void TryTap()
    {
        if (isDestroyed) return;

        isDestroyed = true;

        // 💥 Explosion nur beim Tap
        SpawnExplosion();

        gravitySystem?.OnPointDestroyed(true);

        Destroy(gameObject);
    }
}