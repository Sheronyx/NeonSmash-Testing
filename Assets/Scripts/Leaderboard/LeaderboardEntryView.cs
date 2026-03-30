using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntryView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image background;

    [Header("Style Settings")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color highlightTextColor = Color.cyan;
    [SerializeField] private Color normalBackgroundColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color highlightBackgroundColor = new Color(0.5f, 1f, 1f, 0.25f);
    [SerializeField] private FontStyles normalFontStyle = FontStyles.Normal;
    [SerializeField] private FontStyles highlightFontStyle = FontStyles.Normal;

    public bool IsPlayer { get; private set; }

    public void Bind(int rank, string playerName, double score, bool isMe)
    {

        IsPlayer = isMe;
        // Werte setzen
        rankText.text = rank.ToString();
        nameText.text = playerName;
        scoreText.text = ((long)score).ToString(); // Ganzzahlige Anzeige

        // Styles anwenden
        var textColor = isMe ? highlightTextColor : normalTextColor;
        var fontStyle = isMe ? highlightFontStyle : normalFontStyle;
        var bgColor   = isMe ? highlightBackgroundColor : normalBackgroundColor;

        rankText.color = textColor;
        nameText.color = textColor;
        scoreText.color = textColor;

        rankText.fontStyle = fontStyle;
        nameText.fontStyle = fontStyle;
        scoreText.fontStyle = fontStyle;

        if (background)
            background.color = bgColor;
    }
}
