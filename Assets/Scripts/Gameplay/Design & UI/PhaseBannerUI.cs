using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Zeigt beim Phasenwechsel einen Textbanner (fliegt von rechts rein mit Gummieffekt,
/// nach links wieder raus). UISlideAnimator muss auf dem gleichen oder einem Kind-GameObject sitzen.
/// </summary>
public class PhaseBannerUI : MonoBehaviour
{
    [SerializeField] private UISlideAnimator  slideAnimator;
    [SerializeField] private TextMeshProUGUI  label;
    [SerializeField] private float            holdDuration = 0.7f;

    [Tooltip("Format des Texts. {0}=Phasennummer, {1}=Gesamtanzahl.")]
    [SerializeField] private string textFormat = "Phase {0}";

    private Coroutine _routine;

    private void OnEnable()
    {
        PhaseManager.OnPhaseBanner     += Show;
        PhaseManager.OnPhaseTextBanner += ShowText;
    }

    private void OnDisable()
    {
        PhaseManager.OnPhaseBanner     -= Show;
        PhaseManager.OnPhaseTextBanner -= ShowText;
    }

    private void Show(int phaseNumber, int totalPhases)
    {
        if (label != null) label.text = string.Format(textFormat, phaseNumber, totalPhases);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Banner(holdDuration));
    }

    private void ShowText(string text, float visibleSeconds)
    {
        if (label != null) label.text = text;
        if (_routine != null) StopCoroutine(_routine);

        float slideIn  = slideAnimator != null ? slideAnimator.slideInDuration  : 0f;
        float slideOut = slideAnimator != null ? slideAnimator.slideOutDuration : 0f;
        float hold     = Mathf.Max(0f, visibleSeconds - slideIn - slideOut);
        _routine = StartCoroutine(Co_Banner(hold));
    }

    private IEnumerator Co_Banner(float hold)
    {
        if (slideAnimator == null) yield break;

        slideAnimator.SlideInFromRight();
        yield return new WaitForSecondsRealtime(slideAnimator.slideInDuration + hold);
        slideAnimator.SlideOutToLeft();
        _routine = null;
    }
}
