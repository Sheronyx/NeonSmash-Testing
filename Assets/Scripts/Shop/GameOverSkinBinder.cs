using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Wählt je nach aktivem Skin die passende Game-Over-/Result-Variante.
//
// Die GETEILTEN Container (gameOverBanner, CanvasScoreMenu) bleiben IMMER aktiv
// und werden von GameUIManager per Alpha eingeblendet. Pro Variante werden nur
// die KIND-Objekte (Game-Over-Text + Score-Panel) ein-/ausgeschaltet und die
// passenden Refs in GameUIManager gebunden.
//
// Auf ein immer aktives Objekt legen (z.B. das GameManager-Objekt).
public class GameOverSkinBinder : MonoBehaviour
{
    [System.Serializable]
    public class Variant
    {
        [Tooltip("Null = Default-Variante (wenn kein Skin passt).")]
        public SkinTheme theme;

        [Header("Toggle (Kind-Objekte)")]
        [Tooltip("Das Game-Over-Text-Kind (z.B. gameOverTextTMP Default/Heavenly).")]
        public GameObject gameOverTextObject;
        [Tooltip("Das Score-Panel-Kind (z.B. ScoreMenuPanel Default/Heavenly).")]
        public GameObject scorePanelObject;

        [Header("Bind (Refs in GameUIManager)")]
        public TextMeshProUGUI gameOverText;
        public TextMeshProUGUI resultHeadline;
        public TextMeshProUGUI resultScore;
        public Button          restartButton;
        public Button          backButton;
    }

    [SerializeField] private GameUIManager gameUI;

    [Header("Geteilte Container (immer aktiv, von GameUIManager gefadet)")]
    [SerializeField] private CanvasGroup gameOverBanner;   // gameOverBanner
    [SerializeField] private CanvasGroup resultPanel;       // CanvasScoreMenu

    [SerializeField] private Variant defaultVariant;
    [SerializeField] private Variant[] skinVariants;

    void Start()
    {
        if (gameUI == null) gameUI = FindFirstObjectByType<GameUIManager>();
        if (gameUI == null) return;

        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;

        Variant chosen = defaultVariant;
        foreach (var v in skinVariants)
        {
            bool match = active != null && v.theme == active;
            Toggle(v, match);
            if (match) chosen = v;
        }
        Toggle(defaultVariant, chosen == defaultVariant);

        gameUI.BindResultUI(
            gameOverBanner, chosen.gameOverText,
            resultPanel, chosen.resultHeadline, chosen.resultScore,
            chosen.restartButton, chosen.backButton);
    }

    private void Toggle(Variant v, bool on)
    {
        if (v == null) return;
        if (v.gameOverTextObject != null) v.gameOverTextObject.SetActive(on);
        if (v.scorePanelObject  != null) v.scorePanelObject.SetActive(on);
    }
}
