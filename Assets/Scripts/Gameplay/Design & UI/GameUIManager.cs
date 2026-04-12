using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameUIManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private Canvas topBarCanvas;

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
    }

    public void ShowGameOver(int score, bool isInfinityMode)
    {
        StartCoroutine(Co_ShowGameOver(score, isInfinityMode));
    }

    private IEnumerator Co_ShowGameOver(int score, bool isInfinityMode)
    {
        if (topBarCanvas != null)
            topBarCanvas.enabled = false;

        string text = isInfinityMode ? "GAME OVER" : "FINISHED";

        if (gameOverBanner != null && gameOverTextTMP != null)
        {
            gameOverTextTMP.text = text;
            yield return Fade(gameOverBanner, 0, 1, 0.25f);
            yield return new WaitForSeconds(0.6f);
            yield return Fade(gameOverBanner, 1, 0, 0.25f);
        }

        if (resultPanel != null)
        {
            resultHeadlineTMP.text = text;
            resultScoreTMP.text = score.ToString();

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
        Time.timeScale = 1f;
        AudioListener.pause = false;

        MusicManager.ForceRestartGameMusicNextLoad = true;

        string current = SceneManager.GetActiveScene().name;

        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadScene(current);

    }


    public void BackToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneFader.Instance.LoadScene("MainMenuScene");


    }
}