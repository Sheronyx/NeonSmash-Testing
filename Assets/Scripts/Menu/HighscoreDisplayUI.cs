using TMPro;
using UnityEngine;

// Zeigt den lokalen Highscore (Infinity Mode) im Startmenü an -- gleiche Quelle wie die "BEST:"-
// Anzeige im Spiel selbst (siehe ScoreManager.Start), damit beide immer übereinstimmen.
public class HighscoreDisplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;

    private void OnEnable()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (amountText == null) return;
        int best = HighscoreUploader.GetLocalBest(LeaderboardApi.InfinityId);
        // Gleiche Formatierung wie die Currency-Anzeigen im Startmenü (aktuelle System-Kultur,
        // z.B. "100.044" statt einer erzwungenen Kultur) -- optisch konsistent zur Currency Bar.
        amountText.text = best.ToString("N0");
    }
}
