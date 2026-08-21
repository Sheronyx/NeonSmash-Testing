using UnityEngine;
using UnityEngine.UI;

// Zeigt den Fortschritt der 3 Farb-Zähler als Füllbalken an (0 = leer, Schwellenwert = voll),
// die den jeweiligen Special Mode auslösen.
// Rein anzeigend — die eigentliche Logik/der Zählerstand liegt im PhaseManager.
public class ColorProgressUI : MonoBehaviour
{
    [Header("Fill Bars (Image Type: Filled, Fill Method: Horizontal)")]
    [SerializeField] private Image pinkFill;
    [SerializeField] private Image greenFill;
    [SerializeField] private Image blueFill;

    [Header("Optional: Glow-Marker am Füllstand-Ende")]
    [SerializeField] private RectTransform pinkGlow;
    [SerializeField] private RectTransform greenGlow;
    [SerializeField] private RectTransform blueGlow;

    // Reward-Preview (siehe docs/RESEARCH.md "UI-Pattern für Progress-/Meta-Progression-Bars"):
    // deutet knapp vor Balken-voll bereits an, dass gleich der Special Mode ausgelöst wird, statt
    // nur reinen Fortschritt zu zeigen. Optional/nullable, damit dieses Feature ohne Scene-Änderung
    // vorbereitet werden kann — Felder bleiben leer, bis in der Szene ein Image mit
    // "Reward Preview Sparkle.png" zugewiesen wird (siehe Assets/001 Fairy World/Elements/Fairy
    // Progress Icons/), Verhalten ist bis dahin identisch zu vorher (reine Glow-Balken).
    [Header("Optional: Reward-Preview (aktiviert kurz vor Balken-voll)")]
    [SerializeField, Range(0.5f, 0.99f)] private float rewardPreviewThreshold = 0.75f;
    [SerializeField] private GameObject pinkRewardPreview;
    [SerializeField] private GameObject greenRewardPreview;
    [SerializeField] private GameObject blueRewardPreview;

    private void OnEnable()
    {
        PhaseManager.OnColorProgressChanged += HandleProgressChanged;

        // Falls dieses UI erst nach Rundenstart aktiviert wird: aktuellen Stand sofort nachziehen.
        if (PhaseManager.Instance != null)
        {
            int threshold = PhaseManager.Instance.ColorTriggerThreshold;
            UpdateFill(pinkFill,  pinkGlow,  pinkRewardPreview,  PhaseManager.Instance.GetColorCount(PointColor.Pink),  threshold);
            UpdateFill(greenFill, greenGlow, greenRewardPreview, PhaseManager.Instance.GetColorCount(PointColor.Green), threshold);
            UpdateFill(blueFill,  blueGlow,  blueRewardPreview,  PhaseManager.Instance.GetColorCount(PointColor.Blue),  threshold);
        }
    }

    private void OnDisable()
    {
        PhaseManager.OnColorProgressChanged -= HandleProgressChanged;
    }

    private void HandleProgressChanged(PointColor color, int current, int threshold)
    {
        switch (color)
        {
            case PointColor.Pink:  UpdateFill(pinkFill,  pinkGlow,  pinkRewardPreview,  current, threshold); break;
            case PointColor.Green: UpdateFill(greenFill, greenGlow, greenRewardPreview, current, threshold); break;
            case PointColor.Blue:  UpdateFill(blueFill,  blueGlow,  blueRewardPreview,  current, threshold); break;
        }
    }

    private void UpdateFill(Image fill, RectTransform glow, GameObject rewardPreview, int current, int threshold)
    {
        if (fill == null) return;
        float amount = threshold > 0 ? Mathf.Clamp01((float)current / threshold) : 0f;
        fill.fillAmount = amount;

        if (glow != null)
        {
            // Glow-Marker entlang der Fülllänge des Balkens positionieren (links -> rechts, horizontaler Fill).
            float width = fill.rectTransform.rect.width;
            glow.anchoredPosition = new Vector2(-width * 0.5f + width * amount, glow.anchoredPosition.y);
            glow.gameObject.SetActive(current > 0);
        }

        if (rewardPreview != null)
            rewardPreview.SetActive(amount >= rewardPreviewThreshold);
    }
}
