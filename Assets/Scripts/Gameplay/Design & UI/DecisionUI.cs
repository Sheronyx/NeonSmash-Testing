using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entscheidungs-UI vor Special-Mode-Phasen: fliegt von rechts rein (Gummieffekt),
/// nach links wieder raus. Nutzt UISlideAnimator für die Bewegung.
/// CanvasGroup bleibt für Button-Interaktivität.
/// </summary>
public class DecisionUI : MonoBehaviour
{
    [SerializeField] private UISlideAnimator slideAnimator;
    [SerializeField] private CanvasGroup     group;
    [SerializeField] private Button          gravityButton;
    [SerializeField] private Button          fountainButton;

    [Tooltip("Objekt mit Karten/VFX — wird per SetActive gesteuert, da CanvasGroup-Alpha " +
             "VFX/Partikel nicht ausblendet.")]
    [SerializeField] private GameObject content;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        SetInteractable(false);
        if (content != null) content.SetActive(false);

        if (gravityButton  != null) gravityButton.onClick.AddListener(()  => Choose(SpecialMode.Gravity));
        if (fountainButton != null) fountainButton.onClick.AddListener(() => Choose(SpecialMode.Fountain));
    }

    private void OnEnable()  { }
    private void OnDisable() { }

    private void Show()
    {
        if (content != null) content.SetActive(true);
        SetInteractable(true);
        slideAnimator?.SlideInFromRight();
    }

    private void Hide()
    {
        SetInteractable(false);
        slideAnimator?.SlideOutToLeft(() =>
        {
            if (content != null) content.SetActive(false);
        });
    }

    private void Choose(SpecialMode mode) { }

    private void SetInteractable(bool value)
    {
        if (group == null) return;
        group.blocksRaycasts = value;
        group.interactable   = value;
    }
}
