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

    void OnEnable()  => PhaseManager.OnPhaseBanner += Show;
    void OnDisable() => PhaseManager.OnPhaseBanner -= Show;

    void Show(int phaseNumber, int totalPhases)
    {
        if (label != null) label.text = string.Format(textFormat, phaseNumber, totalPhases);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Co_Banner());
    }

    IEnumerator Co_Banner()
    {
        if (group == null) yield break;
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(holdDuration);
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
