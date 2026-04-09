using UnityEngine;
using System.Collections;

public class FountainModeActivationPoint : MonoBehaviour
{
    public MixedPointSpawner spawner;

    [Header("VFX")]
    [SerializeField] private GameObject slashVFXPrefab;
    [SerializeField] private float delayBeforeFountainMode = 1f;

    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float flySpeed = 10f;

    private ArcanePortalFlash portal;
    private bool isTriggered = false;

    void Start()
    {
        var p = FindFirstObjectByType<ArcanePortalFlash>();
        if (p != null)
            portal = p;

        StartCoroutine(AutoDestroy());
    }

    private IEnumerator AutoDestroy()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isTriggered) Destroy(gameObject);
    }

    public void TryTap()
    {
        if (isTriggered) return;

        isTriggered = true;

        spawner?.PauseSpawning(true);

        StartCoroutine(FlyToPortal());
    }

    private IEnumerator FlyToPortal()
{
    Vector3 start = transform.position;
    Vector3 end = portal.transform.position;

    float t = 0f;

    while (t < 1f)
    {
        t += Time.deltaTime * flySpeed;
        transform.position = Vector3.Lerp(start, end, t);
        transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
        yield return null;
    }

    transform.position = end;

    // 👉 SLASH VFX
    if (slashVFXPrefab != null)
    {
        var slash = Instantiate(slashVFXPrefab, end, Quaternion.identity);
        Destroy(slash, 2f);
    }

    // 👉 Portal wird blau + Flash
    if (portal != null)
    {
        var portalFlash = portal.GetComponent<ArcanePortalFlash>();
        portalFlash?.SetMode(SpecialMode.Fountain);
        portalFlash?.FlashParticles();
    }

    // ⏳ 👉 HIER DER WICHTIGE DELAY
    yield return new WaitForSeconds(delayBeforeFountainMode);

    // 👉 Jetzt erst Mode starten
    SpecialModeManager.Instance?.StartMode(SpecialMode.Fountain);

    Destroy(gameObject);
}
}