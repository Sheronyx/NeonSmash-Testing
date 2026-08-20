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

    private void OnEnable()
    {
        PhaseManager.OnColorProgressChanged += HandleProgressChanged;

        // Falls dieses UI erst nach Rundenstart aktiviert wird: aktuellen Stand sofort nachziehen.
        if (PhaseManager.Instance != null)
        {
            int threshold = PhaseManager.Instance.ColorTriggerThreshold;
            UpdateFill(pinkFill,  pinkGlow,  PhaseManager.Instance.GetColorCount(PointColor.Pink),  threshold);
            UpdateFill(greenFill, greenGlow, PhaseManager.Instance.GetColorCount(PointColor.Green), threshold);
            UpdateFill(blueFill,  blueGlow,  PhaseManager.Instance.GetColorCount(PointColor.Blue),  threshold);
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
            case PointColor.Pink:  UpdateFill(pinkFill,  pinkGlow,  current, threshold); break;
            case PointColor.Green: UpdateFill(greenFill, greenGlow, current, threshold); break;
            case PointColor.Blue:  UpdateFill(blueFill,  blueGlow,  current, threshold); break;
        }
    }

    private void UpdateFill(Image fill, RectTransform glow, int current, int threshold)
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
    }
}
