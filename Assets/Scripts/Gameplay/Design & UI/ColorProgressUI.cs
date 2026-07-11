using TMPro;
using UnityEngine;

// Zeigt den Fortschritt der 3 Farb-Zähler an (z.B. "12/20"), die den jeweiligen Special Mode auslösen.
// Rein anzeigend — die eigentliche Logik/der Zählerstand liegt im PhaseManager.
public class ColorProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI pinkText;
    [SerializeField] private TextMeshProUGUI greenText;
    [SerializeField] private TextMeshProUGUI blueText;

    private void OnEnable()
    {
        PhaseManager.OnColorProgressChanged += HandleProgressChanged;

        // Falls dieses UI erst nach Rundenstart aktiviert wird: aktuellen Stand sofort nachziehen.
        if (PhaseManager.Instance != null)
        {
            int threshold = PhaseManager.Instance.ColorTriggerThreshold;
            UpdateText(pinkText,  PhaseManager.Instance.GetColorCount(PointColor.Pink),  threshold);
            UpdateText(greenText, PhaseManager.Instance.GetColorCount(PointColor.Green), threshold);
            UpdateText(blueText,  PhaseManager.Instance.GetColorCount(PointColor.Blue),  threshold);
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
            case PointColor.Pink:  UpdateText(pinkText,  current, threshold); break;
            case PointColor.Green: UpdateText(greenText, current, threshold); break;
            case PointColor.Blue:  UpdateText(blueText,  current, threshold); break;
        }
    }

    private void UpdateText(TextMeshProUGUI label, int current, int threshold)
    {
        if (label != null) label.text = $"{current}/{threshold}";
    }
}
