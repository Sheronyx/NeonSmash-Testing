using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private Canvas topBarCanvas;
    [SerializeField] private GameObject pauseButton;

    [Header("Game Over")]
    [SerializeField] private CanvasGroup gameOverBanner;
    [SerializeField] private TextMeshProUGUI gameOverTextTMP;

    [Header("Result Panel")]
    [SerializeField] private CanvasGroup resultPanel;
    [SerializeField] private TextMeshProUGUI resultHeadlineTMP;
    [SerializeField] private TextMeshProUGUI resultScoreTMP;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button backToMenuButton;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(BackToMenu);

        if (MultiplayerManager.IsMultiplayerGame && pauseButton != null)
            pauseButton.SetActive(false);
    }

    // Bindet die UI der aktiven Skin-Variante (Game Over Banner + Result Panel
    // + Buttons). Wird vom GameOverSkinBinder beim Start aufgerufen, damit nach
    // einem Bundle-Swap die richtige (aktive) Variante gesteuert wird.
    public void BindResultUI(
        CanvasGroup banner, TextMeshProUGUI bannerText,
        CanvasGroup result, TextMeshProUGUI headline, TextMeshProUGUI scoreTMP,
        Button restart, Button back)
    {
        gameOverBanner    = banner;
        gameOverTextTMP   = bannerText;
        resultPanel       = result;
        resultHeadlineTMP = headline;
        resultScoreTMP    = scoreTMP;

        // Buttons neu verdrahten (alte Listener entfernen, neue setzen)
        if (restartButton != null)    restartButton.onClick.RemoveListener(RestartGame);
        if (backToMenuButton != null) backToMenuButton.onClick.RemoveListener(BackToMenu);

        restartButton    = restart;
        backToMenuButton = back;

        if (restartButton != null)    restartButton.onClick.AddListener(RestartGame);
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(BackToMenu);
    }

    public void ShowGameOver(int score, string bannerText = "GAME OVER")
    {
        StartCoroutine(Co_ShowGameOver(score, bannerText));
    }

    private IEnumerator Co_ShowGameOver(int score, string bannerText)
    {
        if (pauseButton != null)
            pauseButton.SetActive(false);

        string text = bannerText;

        if (gameOverBanner != null && gameOverTextTMP != null)
        {
            gameOverTextTMP.text = text;
            yield return Fade(gameOverBanner, 0, 1, 0.25f);
            yield return new WaitForSeconds(0.6f);
            yield return Fade(gameOverBanner, 1, 0, 0.25f);
        }

        // Interstitial (jedes 3. Game Over) im Gap zwischen Banner und Score-Panel.
        // Wartet, bis die Anzeige geschlossen ist, dann erscheint das Ergebnis.
        if (AdManager.Instance != null)
        {
            bool adClosed = false;
            AdManager.Instance.MaybeShowInterstitial(() => adClosed = true);
            yield return new WaitUntil(() => adClosed);
        }

        if (resultPanel != null)
        {
            resultHeadlineTMP.text = text;
            resultScoreTMP.text = ScoreManager.Format(score);

            // Buttons sofort klickbar — nicht erst nach der Fade-Animation
            resultPanel.interactable = true;
            resultPanel.blocksRaycasts = true;
            yield return FadeAlpha(resultPanel, 0, 1, 0.25f);
        }
    }

    private IEnumerator FadeAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;

        cg.alpha = from;

        // 👉 HIER FIX
        cg.interactable = false;
        cg.blocksRaycasts = false;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        cg.alpha = to;

        // 👉 UND HIER
        if (to > 0.9f)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    public void RestartGame()
    {
        NeonAnalytics.LogGameOverAction("restart");

        Time.timeScale = 1f;
        AudioListener.pause = false;

        MusicManager.ForceRestartGameMusicNextLoad = true;

        string current = SceneManager.GetActiveScene().name;
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadScene(current);
    }

    public void BackToMenu()
    {
        NeonAnalytics.LogGameOverAction("menu");

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneFader.Instance.LoadScene("MainMenuScene");
    }
}