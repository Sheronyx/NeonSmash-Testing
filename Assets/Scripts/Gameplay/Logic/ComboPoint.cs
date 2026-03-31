using UnityEngine;
using System.Collections;

public class ComboPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("Settings")]
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;
    [SerializeField] private float delayBeforeGoldMode = 5f;

    [Header("VFX")]
    [SerializeField] private GameObject absorbVFXPrefab;

    private ArcanePortalFlash portal;
    private Transform portalTransform;

    private bool isDestroyed = false;
    private bool isFinishing = false;

    void Start()
    {
        // 👉 Portal automatisch finden (kein Inspector nötig)
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
        if (isDestroyed || isFinishing) return;

        isDestroyed = true;
        isFinishing = true;

        if (spawner != null)
        {
            spawner.StopSpawning();
        }

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
        if (absorbVFXPrefab != null)
        {
            Instantiate(absorbVFXPrefab, endPos, Quaternion.identity);
        }

        // 👉 Portal sofort visuell gold + flash
        if (portal != null)
        {
            portal.SetGoldMode(true);

            if (spawner != null)
            {
                spawner.SetGoldVisualState(true);
                spawner.ConvertAllPointsToGoldAndDestroy();
            }
            portal.FlashParticles();
        }

        // 👉 Orb "unsichtbar" machen (KEIN SetActive false!)
        DisableVisuals();

        // 👉 Delay bevor echter GoldMode startet
        yield return new WaitForSeconds(delayBeforeGoldMode);



        // 👉 VISUAL RESET
        if (portal != null)
        {
            portal.SetGoldMode(false);
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
            spawner.ActivateGoldMode();
            spawner.Begin();
        }

        DestroySelf();
    }

    private void DestroySelf()
    {
        if (spawner != null)
        {
            spawner.OnComboDestroyed();
        }

        Destroy(gameObject);
    }
}