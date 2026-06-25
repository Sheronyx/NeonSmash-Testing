using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entscheidungs-UI vor Special-Mode-Phasen (Archero-Stil): zwei Karten/Buttons (Gravity/Fountain).
/// Blendet smooth ein/aus, lauscht auf <see cref="PhaseManager"/>. Während der Entscheidung ist
/// Time.timeScale=0 → diese UI nutzt unscaled Time.
///
/// Setup: an ein Canvas-Objekt mit CanvasGroup hängen; die zwei Buttons zuweisen.
/// </summary>
public class DecisionUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Button gravityButton;
    [SerializeField] private Button fountainButton;
    [SerializeField] private float fadeDuration = 0.25f;

    [Tooltip("Objekt mit den Karten/VFX, das zusätzlich an-/ausgeschaltet wird. " +
             "Nötig, weil CanvasGroup-Alpha VFX/Partikel NICHT ausblendet (nur UI-Grafik). " +
             "Z.B. das 'Cards'-Objekt.")]
    [SerializeField] private GameObject content;

    Coroutine _fade;

    void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        SetVisible(false, 0f);
        if (content != null) content.SetActive(false);   // VFX am Start aus

        if (gravityButton  != null) gravityButton.onClick.AddListener(()  => Choose(SpecialMode.Gravity));
        if (fountainButton != null) fountainButton.onClick.AddListener(() => Choose(SpecialMode.Fountain));
    }

    void OnEnable()
    {
        PhaseManager.OnDecisionRequested += Show;
        PhaseManager.OnDecisionClosed    += Hide;
    }

    void OnDisable()
    {
        PhaseManager.OnDecisionRequested -= Show;
        PhaseManager.OnDecisionClosed    -= Hide;
    }

    void Show()
    {
        if (content != null) content.SetActive(true);   // VFX/Karten an, dann einblenden
        StartFade(1f, true);
    }

    void Hide() => StartFade(0f, false);   // content wird am Ende des Fades deaktiviert

    void Choose(SpecialMode mode)
    {
        PhaseManager.Instance?.ChooseMode(mode);
    }

    void StartFade(float targetAlpha, bool interactable)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Co_Fade(targetAlpha, interactable));
    }

    IEnumerator Co_Fade(float target, bool interactable)
    {
        if (group == null) yield break;

        // Beim Einblenden sofort klickbar; beim Ausblenden sofort sperren.
        group.blocksRaycasts = interactable;
        group.interactable   = interactable;

        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;   // funktioniert auch bei timeScale=0
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;

        // Nach dem Ausblenden den Inhalt (inkl. VFX) wirklich abschalten.
        if (target <= 0f && content != null) content.SetActive(false);

        _fade = null;
    }

    void SetVisible(bool visible, float alpha)
    {
        if (group == null) return;
        group.alpha          = alpha;
        group.blocksRaycasts = visible;
        group.interactable   = visible;
    }
}
