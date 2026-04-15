using UnityEngine;
using System.Collections;

public class GravityModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float delayBeforeGravityMode = 1.6f;

    [Header("VFX")]
    [SerializeField] private GameObject SlashVFXPrefab;

    private ArcanePortalFlash portal;
    private Transform portalTransform;

    private bool isDestroyed = false;
    private bool isFinishing = false;

    private bool orbArrived = false;
    private bool pointArrived = false;

    void Start()
    {
        portal = FindFirstObjectByType<ArcanePortalFlash>();
        if (portal != null)
            portalTransform = portal.transform;

        TutorialManager.Instance?.OnElementSpawnedShowOverlay(
            TutorialPointType.GravityOrb, transform.position, gameObject);

        StartCoroutine(AutoDestroy());
    }

    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isDestroyed && !isFinishing)
            DestroySelf();
    }

    public void TryTap()
    {
        if (spawner != null && spawner.IsLevelUpActive()) return;
        OnTapped();
    }

    public void OnTapped()
    {
        if (isDestroyed || isFinishing) return;

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnActionPerformed(TutorialPointType.GravityOrb);

        isDestroyed = true;
        isFinishing = true;

        spawner?.PauseSpawning(true);

        GameObject stolenPoint = spawner != null ? spawner.StealCurrentPoint() : null;

        StartCoroutine(CoFlyBothToPortal(stolenPoint));
    }

    private IEnumerator CoFlyBothToPortal(GameObject stolenPoint)
    {
        if (portalTransform == null)
        {
            FinishCombo();
            yield break;
        }

        orbArrived = false;
        pointArrived = stolenPoint == null;

        StartCoroutine(CoFlyOrb());
        if (stolenPoint != null)
            StartCoroutine(CoFlyNeonPoint(stolenPoint));

        yield return new WaitUntil(() => orbArrived && pointArrived);

        // Beide angekommen → Portal färben + Slash
        if (portal != null)
            portal.FlashParticles();

        float slashDuration = delayBeforeGravityMode > 0f ? delayBeforeGravityMode : 1.5f;
        if (SlashVFXPrefab != null)
        {
            var slash = Instantiate(SlashVFXPrefab, portalTransform.position, Quaternion.identity);
            var ps = slash.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
                slashDuration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(slash, slashDuration);
        }

        yield return new WaitForSeconds(slashDuration);

        FinishCombo();
    }

    private IEnumerator CoFlyOrb()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = portalTransform.position;
        Vector3 startScale = transform.localScale;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * flySpeed;
            float eased = Mathf.Clamp01(t) * Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(startPos, endPos, eased);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        orbArrived = true;
    }

    private IEnumerator CoFlyNeonPoint(GameObject point)
    {
        if (point == null) { pointArrived = true; yield break; }

        Vector3 startPos = point.transform.position;
        Vector3 endPos = portalTransform.position;
        Vector3 startScale = point.transform.localScale;
        float t = 0f;

        while (t < 1f)
        {
            if (point == null) break;
            t += Time.unscaledDeltaTime * flySpeed;
            float eased = Mathf.Clamp01(t) * Mathf.Clamp01(t);
            point.transform.position = Vector3.Lerp(startPos, endPos, eased);
            point.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        if (point != null) Destroy(point);
        pointArrived = true;
    }

    private void FinishCombo()
    {
        if (spawner != null)
        {
            if (SpecialModeManager.Instance != null && !SpecialModeManager.Instance.IsModeActive)
                SpecialModeManager.Instance.StartMode(SpecialMode.Gravity);

            spawner.ForceClearCurrentPoint();
        }

        DestroySelf();
    }

    private void DestroySelf()
    {
        if (spawner != null)
            spawner.ClearActivationPoint();

        Destroy(gameObject);
    }
}
