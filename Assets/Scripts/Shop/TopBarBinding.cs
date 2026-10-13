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
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Der sichtbare Kasten der Punkteanzeige. Leer lassen = das Elternobjekt von " +
             "scoreText. Wird am Spielende ausgeblendet, weil der Punktestand dann im " +
             "Ergebnisfenster steht.")]
    [SerializeField] private GameObject scoreRoot;

    /// <summary>Die Punkteanzeige dieser Top-Bar-Variante, zum Aus- und Einblenden.</summary>
    public GameObject ScoreRoot
        => scoreRoot != null ? scoreRoot
         : scoreText != null && scoreText.transform.parent != null ? scoreText.transform.parent.gameObject
         : null;
    [SerializeField] private TextMeshProUGUI personalBestText;

    [Header("Herzen (lifePoint3 = zuerst leer)")]
    [SerializeField] private Image lifePoint1;
    [SerializeField] private Image lifePoint2;
    [SerializeField] private Image lifePoint3;

    void OnEnable()
    {
        ScoreManager.Instance?.BindUI(scoreText, personalBestText);
        LivesManager.Instance?.BindHearts(lifePoint1, lifePoint2, lifePoint3);
    }
}
