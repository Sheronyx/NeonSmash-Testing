using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Auf JEDE Top-Bar-Variante legen (Default + Skin-Bars).
//
// Externe Manager (ScoreManager, LivesManager) zeigen mit ihren Referenzen in
// die Bar. Bei einem Bar-Swap (Enable/Disable) müssen sie auf die jetzt aktive
// Bar umgebogen werden. Diese Komponente schiebt beim Aktivieren der Bar deren
// eigene Felder in die Manager.
//
// Combo wird NICHT hier behandelt — die ComboDisplay-Komponente bringt jede Bar
// selbst mit (sie abonniert in OnEnable selbst den ComboManager).
public class TopBarBinding : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private TextMeshProUGUI tempScoreText;
    [SerializeField] private TextMeshProUGUI safeScoreText;
    [SerializeField] private TextMeshProUGUI personalBestText;

    [Header("Herzen (lifePoint3 = zuerst leer)")]
    [SerializeField] private Image lifePoint1;
    [SerializeField] private Image lifePoint2;
    [SerializeField] private Image lifePoint3;

    void OnEnable()
    {
        // Wellen/Korb-Modus (Infinity) hat weder Leben noch laufenden Score — nur das alte
        // Binding überspringen. Die Bar selbst NICHT deaktivieren: sie wird im Infinity-Modus
        // von WaveHUDBinder (Korbstand/Timer) auf demselben GameObject weiterverwendet.
        if (GlobalGameManager.Instance != null && GlobalGameManager.Instance.SelectedMode == GameMode.Infinity)
            return;

        ScoreManager.Instance?.BindUI(tempScoreText, personalBestText, safeScoreText);
        LivesManager.Instance?.BindHearts(lifePoint1, lifePoint2, lifePoint3);
    }
}
