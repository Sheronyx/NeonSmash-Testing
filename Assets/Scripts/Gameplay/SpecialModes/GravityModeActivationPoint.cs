using UnityEngine;
using System.Collections;

public class GravityModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float delayBeforeGravityMode = 0.5f;

    [Header("VFX")]
    [SerializeField] private GameObject SlashVFXPrefab;

    private ArcanePortalFlash portal;
    private Transform portalTransform;

    private bool isDestroyed = false;
    private bool isFinishing = false;

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
        if (spawner != null)
        {
            spawner.PauseSpawning(true);
        }

        if (isDestroyed || isFinishing) return;

        isDestroyed = true;
        isFinishing = true;

        Debug.Log("🌪️ GRAVITY ORB GETRIGGERT!");

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
            float eased = progress * progress;

            transform.position = Vector3.Lerp(startPos, endPos, eased);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);

            yield return null;
        }

        transform.position = endPos;

        // 🔥 VFX
        if (SlashVFXPrefab != null)
        {
            Instantiate(SlashVFXPrefab, endPos, Quaternion.identity);
        }

        // 🔴 Portal Effekt (optional später anders färben)
        if (portal != null)
        {
            portal.FlashParticles();
        }

        DisableVisuals();

        yield return new WaitForSeconds(delayBeforeGravityMode);

        FinishCombo();
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
            if (SpecialModeManager.Instance != null &&
                !SpecialModeManager.Instance.IsModeActive)
            {
                SpecialModeManager.Instance.StartMode(SpecialMode.Gravity);
            }

            spawner.PauseSpawning(false);
            spawner.ForceClearCurrentPoint();
        }

        DestroySelf();
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    public void TryTap()
{
    OnTapped();
}
}