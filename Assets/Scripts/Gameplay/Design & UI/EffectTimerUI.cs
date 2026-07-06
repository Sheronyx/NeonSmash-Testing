using TMPro;
using UnityEngine;

public class EffectTimerUI : MonoBehaviour
{
    [Header("Pink Effekt")]
    [SerializeField] private GameObject      pinkRoot;
    [SerializeField] private TextMeshProUGUI pinkTimerText;
    [SerializeField] private TextMeshProUGUI pinkMultText;

    [Header("Blau Effekt")]
    [SerializeField] private GameObject      blueRoot;
    [SerializeField] private TextMeshProUGUI blueTimerText;
    [SerializeField] private TextMeshProUGUI blueBonusText;

    private void Update()
    {
        var cem = ColorEffectManager.Instance;
        if (cem == null) return;

        if (pinkRoot != null) pinkRoot.SetActive(cem.PinkActive);
        if (cem.PinkActive)
        {
            if (pinkTimerText != null)
                pinkTimerText.text = cem.PinkTimeRemaining.ToString("F1") + "s";
            if (pinkMultText != null)
                pinkMultText.text = "×" + cem.Multiplier.ToString("F2");
        }

        if (blueRoot != null) blueRoot.SetActive(cem.BlueActive);
        if (cem.BlueActive)
        {
            if (blueTimerText != null)
                blueTimerText.text = cem.BlueTimeRemaining.ToString("F1") + "s";
            if (blueBonusText != null)
                blueBonusText.text = "+" + cem.ReactionTimeBonus.ToString("F1") + "s";
        }
    }
}
