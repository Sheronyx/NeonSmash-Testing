using UnityEngine;
using System.Collections;

public class GoldModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float delayBeforeGoldMode = 0.5f;

    [Header("Point Destroy Timing")]
    [Tooltip("Delay bei Point ganz unten (viewport Y=0)")]
    [SerializeField] private float destroyDelayAtBottom = 0.1f;
    [Tooltip("Delay bei Point ganz oben (viewport Y=1)")]
    [SerializeField] private float destroyDelayAtTop = 0.6f;

    [Header("VFX")]
    [SerializeField] private GameObject SlashVFXPrefab;

    private ArcanePortalFlash portal;
    private Transform portalTransform;

    private bool isDestroyed = false;
    private bool isFinishing = false;
    private float cachedPointViewportY = 0.5f;

    void Start()
    {
        portal = FindFirstObjectByType<ArcanePortalFlash>();

        if (portal != null)
            portalTransform = portal.transform;

        StartCoroutine(AutoDestroy());
    }

    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(lifetime);

        if (!isDestroyed && !isFinishing)
        {
            DestroySelf();
        }
    }

public void OnTapped()
{
    // 🛑 BLOCK während LevelUp
    if (spawner != null && spawner.IsLevelUpActive())
        return;

    if (spawner != null)
    {
        spawner.PauseSpawning(true);
    }

    if (isDestroyed || isFinishing) return;

    isDestroyed = true;
    isFinishing = true;

    // Viewport-Y des normalen Points cachen bevor er weg ist
    if (spawner != null && spawner.CurrentPointPosition.HasValue && Camera.main != null)
        cachedPointViewportY = Camera.main.WorldToViewportPoint(spawner.CurrentPointPosition.Value).y;

    Debug.Log("COMBO GETRIGGERT!");

    StartCoroutine(CoFlyToPortal());
}

    private IEnumerator CoFlyToPortal()
    {
        if (portalTransform == null)
        {
            Debug.LogWarning("Portal nicht gefunden!");
            FinishCombo();
            yield break;
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = portalTransform.position;

        float t = 0f;

        Vector3 startScale = transform.localScale;

        while (t < 1f)
        {
            t += Time.deltaTime * flySpeed;

            float progress = Mathf.Clamp01(t);
            float eased = progress * progress; // 👉 Ease-In

            transform.position = Vector3.Lerp(startPos, endPos, eased);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);

            yield return null;
        }

        transform.position = endPos;

        // 👉 Portal sofort auf Gold färben beim Einfliegen
        if (portal != null)
            portal.SetMode(SpecialMode.Gold);

        // 👉 VFX am Portal
        float slashDuration = delayBeforeGoldMode > 0f ? delayBeforeGoldMode : 1.5f;
        if (SlashVFXPrefab != null)
        {
            var slash = Instantiate(SlashVFXPrefab, endPos, Quaternion.identity);
            var ps = slash.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
                slashDuration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(slash, slashDuration);
        }

        // 👉 Portal flash
        if (portal != null)
        {
            portal.FlashParticles();
        }

        // 👉 Point zur richtigen Zeit zerstören basierend auf Y-Position
        float pointDestroyDelay = Mathf.Lerp(destroyDelayAtBottom, destroyDelayAtTop, cachedPointViewportY);
        StartCoroutine(DestroyPointAfterDelay(pointDestroyDelay));

        // 👉 Orb "unsichtbar" machen (KEIN SetActive false!)
        DisableVisuals();

        // 👉 2 Sekunden nach Beginn der Slash Animation
        yield return new WaitForSeconds(1.6f);

        FinishCombo();
    }

    private IEnumerator DestroyPointAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        spawner.ForceClearCurrentPoint();
    }

    private void DisableVisuals()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    private void FinishCombo()
    {
        if (spawner != null)
        {
            if (!SpecialModeManager.Instance.IsModeActive)
            {
                SpecialModeManager.Instance.StartMode(SpecialMode.Gold);
            }

            spawner.PauseSpawning(false);
            spawner.SpawnNextPoint();
        }

        DestroySelf();
    }

    private void DestroySelf()
    {
        if (spawner != null)
        {
            spawner.OnGoldModePointDestroyed();
            spawner.ClearActivationPoint(); 
        }

        Destroy(gameObject);
    }
}