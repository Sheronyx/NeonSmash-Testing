using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// One shared full-screen dim panel for the entire scene.
// Place once in the Canvas, above all content but below any popup panels.
// Popups call DimOverlay.Instance.Show() / Hide() instead of owning their own overlay.
[RequireComponent(typeof(CanvasGroup))]
public class DimOverlay : MonoBehaviour
{
    public static DimOverlay Instance { get; private set; }

    [SerializeField] private float targetAlpha  = 0.55f;
    [SerializeField] private float fadeDuration = 0.25f;

    CanvasGroup _cg;
    Coroutine   _fade;

    void Awake()
    {
        Instance = this;
        _cg = GetComponent<CanvasGroup>();
        _cg.alpha          = 0f;
        _cg.blocksRaycasts = false;
        gameObject.SetActive(true);
    }

    public void Show()
    {
        _cg.blocksRaycasts = true;
        StartFade(targetAlpha);
    }

    public void Hide()
    {
        _cg.blocksRaycasts = false;
        StartFade(0f);
    }

    void StartFade(float to)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Co_Fade(to));
    }

    IEnumerator Co_Fade(float to)
    {
        float from = _cg.alpha;
        float t    = 0f;
        while (t < fadeDuration)
        {
            t        += Time.unscaledDeltaTime;
            _cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration)));
            yield return null;
        }
        _cg.alpha = to;
    }
}
