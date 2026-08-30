using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Kleine wiederverwendbare Icon-Darstellung für eine einzelne RewardDefinition.
// - Währung (Splitter/Kristalle): zeigt immer Icon (aus RewardDefinition.icon) + Menge als Zahl,
//   ohne "+"-Zeichen — Icon und Zahl gemeinsam, nicht alternativ.
// - Titel: zeigt den echten Titelnamen als Text (kein generisches "T"-Kürzel mehr).
// - Sticker/Booster/Welt (ohne zugewiesenes Icon): Kurztext-Fallback wie zuvor.
public class RewardIconUI : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI fallbackLabel;

    // Anker für die zwei Layouts: Währung zeigt Icon rechts + Zahl links davon (beide nur einen
    // Teil der Zelle einnehmend), alle anderen Belohnungsarten nutzen das Icon wie zuvor als
    // volles, zellenfüllendes Quadrat.
    static readonly Vector2 CurrencyIconAnchorMin = new Vector2(0.53f, 0.29f);
    static readonly Vector2 CurrencyIconAnchorMax = new Vector2(0.96f, 0.71f);
    static readonly Vector2 CurrencyLabelAnchorMin = new Vector2(0f, 0f);
    static readonly Vector2 CurrencyLabelAnchorMax = new Vector2(0.5f, 1f);

    public void Bind(RewardDefinition reward)
    {
        if (reward == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        bool isCurrency = reward.kind == RewardKind.DiamondSplinters || reward.kind == RewardKind.Diamonds;

        if (reward.icon != null)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = reward.icon;
                iconImage.color = Color.white;
                ApplyIconRect(isCurrency);
            }
        }
        else if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }

        if (isCurrency)
        {
            if (fallbackLabel != null)
            {
                fallbackLabel.gameObject.SetActive(true);
                fallbackLabel.enableAutoSizing = true; // falls von einem vorherigen Bind (Titel/Welt) noch fest gesetzt
                fallbackLabel.text = reward.amount.ToString();
                fallbackLabel.alignment = TextAlignmentOptions.Right;
                fallbackLabel.fontSizeMax = 60;
                var rt = fallbackLabel.rectTransform;
                rt.anchorMin = CurrencyLabelAnchorMin;
                rt.anchorMax = CurrencyLabelAnchorMax;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            return;
        }

        if (reward.kind == RewardKind.Title)
        {
            if (fallbackLabel != null)
            {
                fallbackLabel.gameObject.SetActive(true);
                fallbackLabel.text = reward.displayName + " (TITLE)";
                ResetLabelRect();
                // Feste Größe statt Auto-Size, damit Titel- und Welt-Label (siehe unten,
                // RewardKind.WorldUnlock) immer gleich groß wirken statt je nach Textlänge zu variieren.
                fallbackLabel.enableAutoSizing = false;
                fallbackLabel.fontSize = NameSuffixFontSize;
            }
            return;
        }

        if (reward.icon != null)
        {
            if (fallbackLabel != null) fallbackLabel.gameObject.SetActive(false);
            return;
        }

        if (fallbackLabel != null)
        {
            fallbackLabel.gameObject.SetActive(true);
            ResetLabelRect();
            bool fixedSize = reward.kind == RewardKind.WorldUnlock || reward.kind == RewardKind.Booster;
            fallbackLabel.enableAutoSizing = !fixedSize;
            fallbackLabel.text = reward.kind switch
            {
                RewardKind.Booster     => Mathf.Max(1, reward.amount) + "x " + reward.displayName + " (Booster)",
                RewardKind.Sticker     => "? Sticker",
                RewardKind.WorldUnlock => Mathf.Max(1, reward.freePlaysGranted) + "x " + reward.displayName + " (WORLD)",
                _                       => "",
            };

            // Welt- und Booster-Label bekommen dieselbe feste Größe wie das Titel-Label oben, damit
            // alle Namens-Belohnungen im Level-Reward-Fenster optisch gleich groß wirken.
            if (fixedSize)
                fallbackLabel.fontSize = NameSuffixFontSize;
        }
    }

    const float NameSuffixFontSize = 34f;

    void ApplyIconRect(bool isCurrency)
    {
        var rt = iconImage.rectTransform;
        if (isCurrency)
        {
            rt.anchorMin = CurrencyIconAnchorMin;
            rt.anchorMax = CurrencyIconAnchorMax;
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void ResetLabelRect()
    {
        fallbackLabel.alignment = TextAlignmentOptions.Center;
        var rt = fallbackLabel.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
