using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance;

    // lifePoint3 = erstes Herz das sich leert (health 3→2)
    // lifePoint2 = zweites                    (health 2→1)
    // lifePoint1 = letztes                    (health 1→0)
    [Header("Heart Images (lifePoint3 = first to empty)")]
    [SerializeField] private Image lifePoint1;
    [SerializeField] private Image lifePoint2;
    [SerializeField] private Image lifePoint3;

    [Header("Timeout VFX")]
    [SerializeField] private GameObject timeoutVFXPrefab;
    [SerializeField] private float vfxDuration = 0.8f;

    [Header("Pop Animation")]
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float popDuration = 0.15f;

    [Header("Damage per Miss")]
    [SerializeField] private float damagePerMiss = 0.25f;

    private float health = 3f;
    private const float maxHealth = 3f;

    public bool HasLivesLeft => health > 0f;
    public float TotalLoseDuration => vfxDuration + popDuration * 2f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ResetLives();
    }

    public void ResetLives()
    {
        health = maxHealth;
        UpdateHeartFills();
    }

    // Gibt true zurück wenn noch Leben übrig, false bei GameOver
    // damage = 0 → nutzt damagePerMiss aus Inspector; sonst direkt übergeben
    public bool LoseLife(Vector3 vfxPosition, float damage = 0f)
    {
        if (TutorialManager.IsTutorialActive) return true; // kein Schaden im Tutorial
        if (health <= 0f) return false;

        float actualDamage = damage > 0f ? damage : damagePerMiss;

        // Betroffenes Herz VOR dem Abziehen bestimmen
        Image affectedHeart;
        if (health > 2f)      affectedHeart = lifePoint3;
        else if (health > 1f) affectedHeart = lifePoint2;
        else                   affectedHeart = lifePoint1;

        health = Mathf.Max(0f, health - actualDamage);
        UpdateHeartFills();

        bool stillAlive = health > 0f;
        StartCoroutine(VFXThenPop(vfxPosition, affectedHeart));
        return stillAlive;
    }

    private void UpdateHeartFills()
    {
        if (lifePoint3 != null) lifePoint3.fillAmount = Mathf.Clamp01(health - 2f);
        if (lifePoint2 != null) lifePoint2.fillAmount = Mathf.Clamp01(health - 1f);
        if (lifePoint1 != null) lifePoint1.fillAmount = Mathf.Clamp01(health);
    }

    private IEnumerator VFXThenPop(Vector3 vfxPosition, Image heart)
    {
        if (timeoutVFXPrefab != null)
        {
            var vfx = Instantiate(timeoutVFXPrefab, vfxPosition, Quaternion.identity);
            Destroy(vfx, vfxDuration);
        }

        StartCoroutine(PopHeart(heart));
        yield break;
    }

    private IEnumerator PopHeart(Image img)
    {
        if (img == null) yield break;

        RectTransform rt = img.rectTransform;
        Vector3 originalScale = rt.localScale;
        Vector3 bigScale = originalScale * popScale;

        // Pop up
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(originalScale, bigScale, t / popDuration);
            yield return null;
        }

        // Pop down
        t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(bigScale, originalScale, t / popDuration);
            yield return null;
        }

        rt.localScale = originalScale;
    }
}
