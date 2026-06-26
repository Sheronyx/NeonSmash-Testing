using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Zeigt beim Phasenwechsel kurz einen „Phase X"-Banner (Fade in/out).
/// An ein Canvas-Objekt mit CanvasGroup + TMP-Text hängen und die Felder zuweisen.
/// Lauscht auf <see cref="PhaseManager.OnPhaseBanner"/>.
/// </summary>
public class PhaseBannerUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup     group;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float           fadeDuration = 0.25f;
    [SerializeField] private float           holdDuration = 0.7f;

    [Tooltip("Format des Texts. {0}=Phasennummer, {1}=Gesamtanzahl.")]
    [SerializeField] private string textFormat = "Phase {0}";

    Coroutine _routine;

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;
    }

    void OnEnable()
    {
        PhaseManager.OnPhaseBanner     += Show;
        PhaseManager.OnPhaseTextBanner += ShowText;
    }
    void OnDisable()
    {
        PhaseManager.OnPhaseBanner     -= Show;
        PhaseManager.OnPhaseTextBanner -= ShowText;
    }

    void Show(int phaseNumber, int totalPhases)
    {
        if (label != null) label.text = string.Format(textFormat, phaseNumber, totalPhases);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Banner(holdDuration));
    }

    // Freitext-Banner: faded rein/raus innerhalb von visibleSeconds (für die Zwischenphase).
    void ShowText(string text, float visibleSeconds)
    {
        if (label != null) label.text = text;
        if (_routine != null) StopCoroutine(_routine);
        // Hold = Fenster minus Ein-/Ausblendzeit (mind. 0).
        float hold = Mathf.Max(0f, visibleSeconds - 2f * fadeDuration);
        _routine = StartCoroutine(Co_Banner(hold));
    }

    IEnumerator Co_Banner(float hold)
    {
        if (group == null) yield break;
        yield return Fade(0f, 1f);
        yield return new WaitForSecondsRealtime(hold);
        yield return Fade(1f, 0f);
        _routine = null;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
    }
}
