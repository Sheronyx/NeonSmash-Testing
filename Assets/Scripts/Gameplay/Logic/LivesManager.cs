using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance;

    [Header("Heart Images (in order: 1=first lost, 3=last lost)")]
    [SerializeField] private Image lifePoint1;
    [SerializeField] private Image lifePoint2;
    [SerializeField] private Image lifePoint3;

    [Header("Timeout VFX")]
    [SerializeField] private GameObject timeoutVFXPrefab;
    [SerializeField] private float vfxDuration = 0.8f;

    [Header("Pop Animation")]
    [SerializeField] private float popScale = 1.4f;
    [SerializeField] private float popDuration = 0.15f;

    private int remainingLives = 3;

    public bool HasLivesLeft => remainingLives > 0;
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
        remainingLives = 3;
        SetHeartAlpha(lifePoint1, 1f);
        SetHeartAlpha(lifePoint2, 1f);
        SetHeartAlpha(lifePoint3, 1f);
    }

    // Gibt true zurück wenn noch Leben übrig, false bei GameOver
    public bool LoseLife(Vector3 vfxPosition)
    {
        if (remainingLives <= 0) return false;

        remainingLives--;
        bool stillAlive = remainingLives > 0;

        Image lostHeart = remainingLives switch
        {
            2 => lifePoint3,
            1 => lifePoint2,
            _ => lifePoint1
        };

        StartCoroutine(VFXThenPop(vfxPosition, lostHeart));

        return stillAlive;
    }

    private IEnumerator VFXThenPop(Vector3 vfxPosition, Image heart)
    {
        if (timeoutVFXPrefab != null)
        {
            var vfx = Instantiate(timeoutVFXPrefab, vfxPosition, Quaternion.identity);
            Destroy(vfx, vfxDuration);
            yield return new WaitForSeconds(vfxDuration);
        }

        StartCoroutine(PopAndFade(heart));
    }

    private IEnumerator PopAndFade(Image img)
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

        // Pop down + fade out gleichzeitig
        t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / popDuration;
            rt.localScale = Vector3.Lerp(bigScale, originalScale, p);
            var c = img.color;
            c.a = Mathf.Lerp(1f, 0f, p);
            img.color = c;
            yield return null;
        }

        rt.localScale = originalScale;
        SetHeartAlpha(img, 0f);
    }

    private void SetHeartAlpha(Image img, float alpha)
    {
        if (img == null) return;
        var c = img.color;
        c.a = alpha;
        img.color = c;
    }
}
