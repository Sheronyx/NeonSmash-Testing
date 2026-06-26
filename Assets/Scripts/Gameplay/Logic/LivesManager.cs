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

    [Header("Life Lost Animation (Pause)")]
    [SerializeField] private GameObject lifeLostAnimationPrefab;
    [SerializeField] private float lifeLostAnimDuration = 1.0f;

    [Header("Pop Animation")]
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float popDuration = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioClip loseLifeClip;
    [SerializeField] private float loseLifeVolume = 3f;

    [Header("Damage per Miss")]
    [SerializeField] private float damagePerMiss = 0.25f;

    private float health = 3f;
    private const float maxHealth = 3f;

    public static bool IsLifeLostAnimating { get; private set; }

    public bool HasLivesLeft => health > 0f;
    public float TotalLoseDuration => vfxDuration + popDuration * 2f;
    public float TotalGameOverAnimDuration => vfxDuration + lifeLostAnimDuration;

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
        IsLifeLostAnimating = false;
        Time.timeScale = 1f;
        UpdateHeartFills();
    }

    public void BindHearts(Image l1, Image l2, Image l3)
    {
        if (l1 != null) lifePoint1 = l1;
        if (l2 != null) lifePoint2 = l2;
        if (l3 != null) lifePoint3 = l3;
        UpdateHeartFills();
    }

    // damage = 0 → nutzt damagePerMiss aus Inspector; sonst direkt übergeben
    public bool LoseLife(Vector3 vfxPosition, float damage = 0f)
    {
        if (TutorialManager.IsTutorialActive) return true;
        if (health <= 0f) return false;
        if (IsLifeLostAnimating) return health > 0f;

        float actualDamage = damage > 0f ? damage : damagePerMiss;

        Image affectedHeart;
        if (health > 2f)      affectedHeart = lifePoint3;
        else if (health > 1f) affectedHeart = lifePoint2;
        else                   affectedHeart = lifePoint1;

        health = Mathf.Max(0f, health - actualDamage);

        AudioManager.Instance?.PlaySfx(loseLifeClip, loseLifeVolume);

        bool stillAlive = health > 0f;
        IsLifeLostAnimating = true;
        Time.timeScale = 0f;
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
            yield return new WaitForSecondsRealtime(vfxDuration);
            Destroy(vfx);
        }

        if (lifeLostAnimationPrefab != null)
        {
            var instance = Instantiate(lifeLostAnimationPrefab, Vector3.zero, Quaternion.identity);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
            var rootPs = instance.GetComponent<ParticleSystem>();
            if (rootPs != null) rootPs.Play(true);
            yield return new WaitForSecondsRealtime(lifeLostAnimDuration);
            Destroy(instance);
        }

        Time.timeScale = 1f;
        IsLifeLostAnimating = false;
        UpdateHeartFills();
        StartCoroutine(PopHeart(heart));
    }

    private IEnumerator PopHeart(Image img)
    {
        if (img == null) yield break;

        RectTransform rt = img.rectTransform;
        Vector3 originalScale = rt.localScale;
        Vector3 bigScale = originalScale * popScale;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(originalScale, bigScale, t / popDuration);
            yield return null;
        }

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
