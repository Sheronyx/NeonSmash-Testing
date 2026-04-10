using UnityEngine;
using System.Collections;

public class GoldModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float delayBeforeGoldMode = 0.5f;

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

        // 👉 VFX am Portal
        if (SlashVFXPrefab != null)
        {
            var slash = Instantiate(SlashVFXPrefab, endPos, Quaternion.identity);
            var ps = slash.GetComponent<ParticleSystem>();
            float lifetime = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : 3f;
            Destroy(slash, lifetime);
        }

        // 👉 Portal sofort visuell gold + flash
        if (portal != null)
        {
            portal.SetMode(SpecialMode.Gold);

            portal.FlashParticles();
        }

        // 👉 Orb "unsichtbar" machen (KEIN SetActive false!)
        DisableVisuals();

        // 👉 Delay bevor echter GoldMode startet
        yield return new WaitForSeconds(delayBeforeGoldMode);



        // 👉 VISUAL RESET
        if (portal != null)
        {
            portal.SetMode(SpecialMode.None);
        }

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
            if (!SpecialModeManager.Instance.IsModeActive)
            {
                SpecialModeManager.Instance.StartMode(SpecialMode.Gold);
            }

            spawner.PauseSpawning(false);
            if (!spawner.ForceClearCurrentPoint())
                spawner.SpawnNextPoint(); // Safety: currentPoint war bereits null (Spieler hat zwischendrin getippt)
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