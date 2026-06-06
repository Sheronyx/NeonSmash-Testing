using UnityEngine;
using System.Collections;

public class FountainModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Animation")]
    [SerializeField] private float pulseFreq        = 1.5f;
    [SerializeField] private float flyInDuration    = 0.70f;
    [SerializeField] private float bloomDuration    = 1.10f;
    [SerializeField] private float flyOutDuration   = 0.60f;
    [SerializeField] private float flyCurveStrength = 2.5f;

    [Header("Portal")]
    [SerializeField] private float delayBeforeFountainMode = 1.6f;
    [SerializeField] private float slashClipVolume         = 2.6f;

    [Header("VFX")]
    [SerializeField] private GameObject slashVFXPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip orbFlyClip;
    [SerializeField] private AudioClip slashClip;
    private AudioSource orbAudioSource;

    private ArcanePortalFlash portal;
    private Transform portalTransform;

    void Start()
    {
        var p = FindFirstObjectByType<ArcanePortalFlash>();
        if (p != null) { portal = p; portalTransform = p.transform; }

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(OrbSequence());
    }

    private IEnumerator OrbSequence()
    {
        Camera cam  = Camera.main;
        float  camZ = Mathf.Abs(cam.transform.position.z);

        Vector3 center    = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, camZ));
        center.z          = 0f;
        Vector3 startPos  = OffScreenPos(cam, camZ);
        Vector3 ctrl      = CurveControl(startPos, center);
        Vector3 baseScale = transform.localScale;

        transform.position   = startPos;
        transform.localScale = Vector3.zero;

        float totalTime = 0f;

        // ── Phase 1: Einflug ──────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < flyInDuration)
        {
            elapsed   += Time.deltaTime;
            totalTime += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / flyInDuration);
            float ts = Mathf.SmoothStep(0f, 1f, t);
            float u  = 1f - ts;

            transform.position = u * u * startPos + 2f * u * ts * ctrl + ts * ts * center;

            float sin01 = (Mathf.Sin(totalTime * pulseFreq * Mathf.PI * 2f) + 1f) * 0.5f;
            float s     = Mathf.Lerp(0f, 1f, ts) * (1f + sin01 * 0.3f * ts);
            transform.localScale = baseScale * Mathf.Max(0f, s);
            yield return null;
        }
        transform.position = center;

        TutorialManager.Instance?.OnElementSpawnedShowOverlay(
            TutorialPointType.FountainOrb, center, gameObject);

        // ── Phase 2: Pulsierend auf 200% anwachsen ───────────────────────────
        elapsed = 0f;
        while (elapsed < bloomDuration)
        {
            elapsed   += Time.deltaTime;
            totalTime += Time.deltaTime;
            float bt   = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / bloomDuration));

            float minS  = Mathf.Lerp(0.85f, 1.70f, bt);
            float maxS  = Mathf.Lerp(1.30f, 2.30f, bt);
            float sin01 = (Mathf.Sin(totalTime * pulseFreq * Mathf.PI * 2f) + 1f) * 0.5f;
            transform.localScale = baseScale * Mathf.Lerp(minS, maxS, sin01);
            yield return null;
        }

        Vector3 flyOutStartScale = transform.localScale;

        TutorialManager.Instance?.OnActionPerformed(TutorialPointType.FountainOrb);
        spawner?.PauseSpawning(true);

        if (orbFlyClip != null)
        {
            orbAudioSource             = gameObject.AddComponent<AudioSource>();
            orbAudioSource.clip        = orbFlyClip;
            orbAudioSource.loop        = false;
            orbAudioSource.spatialBlend = 0f;
            orbAudioSource.Play();
        }

        // ── Phase 3: Smooth Abflug ────────────────────────────────────────────
        Vector3 flyTo = portalTransform != null ? portalTransform.position : center;
        elapsed = 0f;
        while (elapsed < flyOutDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flyOutDuration));
            transform.position   = Vector3.Lerp(center, flyTo, t);
            transform.localScale = Vector3.Lerp(flyOutStartScale, Vector3.zero, t);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        var sr  = GetComponent<SpriteRenderer>(); if (sr  != null) sr.enabled  = false;
        var col = GetComponent<Collider2D>();      if (col != null) col.enabled = false;

        if (portal != null) { portal.SetMode(SpecialMode.Fountain); portal.FlashParticles(); }
        AudioManager.Instance?.PlaySfx(slashClip, slashClipVolume);

        float slashDuration = delayBeforeFountainMode > 0f ? delayBeforeFountainMode : 1.5f;
        if (slashVFXPrefab != null && portalTransform != null)
        {
            var slash = Instantiate(slashVFXPrefab, portalTransform.position, Quaternion.identity);
            var ps    = slash.GetComponentInChildren<ParticleSystem>();
            if (ps != null) slashDuration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(slash, slashDuration);
        }
        yield return new WaitForSeconds(slashDuration);

        FinishCombo();
    }

    private Vector3 OffScreenPos(Camera cam, float z)
    {
        int   edge = Random.Range(0, 4);
        float u    = Random.Range(0.1f, 0.9f);
        Vector2 vp = edge switch
        {
            0 => new Vector2(u,     1.3f),
            1 => new Vector2(1.3f,  u),
            2 => new Vector2(u,    -0.3f),
            _ => new Vector2(-0.3f, u),
        };
        var p = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, z));
        p.z = 0f;
        return p;
    }

    private Vector3 CurveControl(Vector3 from, Vector3 to)
    {
        Vector3 mid  = (from + to) * 0.5f;
        Vector3 dir  = (to - from).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        return mid + perp * flyCurveStrength * (Random.value > 0.5f ? 1f : -1f);
    }

    private void FinishCombo()
    {
        if (orbAudioSource != null) orbAudioSource.Stop();
        SpecialModeManager.Instance?.StartMode(SpecialMode.Fountain);
        spawner?.ClearActivationPoint();
        Destroy(gameObject);
    }
}
