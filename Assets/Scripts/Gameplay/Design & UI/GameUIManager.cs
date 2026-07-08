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

    [Header("Element-Breakdown (optional, nur Wave/Basket-Ergebnis — leer lassen für Skins ohne Reihen)")]
    [SerializeField] private TextMeshProUGUI normalCountTMP;
    [SerializeField] private TextMeshProUGUI specialCountTMP;
    [SerializeField] private TextMeshProUGUI multiplierTMP;

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
        Button restart, Button back,
        TextMeshProUGUI normalCount = null, TextMeshProUGUI specialCount = null, TextMeshProUGUI multiplier = null)
    {
        gameOverBanner    = banner;
        gameOverTextTMP   = bannerText;
        resultPanel       = result;
        resultHeadlineTMP = headline;
        resultScoreTMP    = scoreTMP;
        normalCountTMP    = normalCount;
        specialCountTMP   = specialCount;
        multiplierTMP     = multiplier;

        // Buttons neu verdrahten (alte Listener entfernen, neue setzen)
        if (restartButton != null)    restartButton.onClick.RemoveListener(RestartGame);
        if (backToMenuButton != null) backToMenuButton.onClick.RemoveListener(BackToMenu);

        restartButton    = restart;
        backToMenuButton = back;

        if (restartButton != null)    restartButton.onClick.AddListener(RestartGame);
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(BackToMenu);
    }

    public void ShowGameOver(int score, string bannerText = "GAME OVER", WaveResultBreakdown? breakdown = null)
    {
        StartCoroutine(Co_ShowGameOver(score, bannerText, breakdown));
    }

    private IEnumerator Co_ShowGameOver(int score, string bannerText, WaveResultBreakdown? breakdown)
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

            // Buttons sofort klickbar — nicht erst nach der Fade-Animation
            resultPanel.interactable = true;
            resultPanel.blocksRaycasts = true;
            yield return FadeAlpha(resultPanel, 0, 1, 0.25f);

            // Element-Breakdown (nur Wave/Basket, nur wenn diese Skin-Variante die Reihen hat):
            // erst die 3 Reihen nacheinander hochzählen, danach die Gesamt-Score-Zeile — wie ein
            // Kassenbon, statt alles gleichzeitig instant zu setzen.
            if (breakdown.HasValue && normalCountTMP != null && specialCountTMP != null && multiplierTMP != null)
            {
                yield return Co_RollNumber(normalCountTMP, breakdown.Value.NormalCount, 0.45f, v => $"{Mathf.RoundToInt(v)}");
                yield return Co_RollNumber(specialCountTMP, breakdown.Value.SpecialCount, 0.45f, v => $"{Mathf.RoundToInt(v)}");
                yield return Co_RollNumber(multiplierTMP, breakdown.Value.MultiplierCount, 0.45f, v => $"{Mathf.RoundToInt(v)}");
            }

            // Gesamt-Score: kein Hochzählen mehr — poppt groß auf, nachdem die kleinen
            // Breakdown-Zahlen fertig sind.
            resultScoreTMP.text = ScoreManager.Format(score);
            yield return Co_PunchText(resultScoreTMP.rectTransform, 1.6f, 0.15f, 0.25f);
        }
    }

    // Zählt eine TMP-Zahl von 0 auf target hoch (EaseOutQuad, unscaled — passend zu Fade/FadeAlpha
    // und zum bestehenden ScoreManager.Co_RollTempScore-Stil). format bekommt den aktuellen
    // Zwischenwert und liefert den fertigen Anzeigetext.
    private IEnumerator Co_RollNumber(TextMeshProUGUI text, float target, float duration, System.Func<float, string> format)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 2f);
            text.text = format(Mathf.Lerp(0f, target, k));
            yield return null;
        }
        text.text = format(target);
    }

    // Skaliert rect von 1 auf peakScale (Sin-Ease-In) und wieder zurück auf 1 (linear) — gleiche
    // "Punch"-Konvention wie ScoreManager.Co_PunchText, hier für das große Score-Aufpoppen genutzt.
    private IEnumerator Co_PunchText(RectTransform rect, float peakScale, float inDuration, float outDuration)
    {
        Vector3 start = Vector3.one;
        Vector3 target = Vector3.one * peakScale;

        float t = 0f;
        while (t < inDuration)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(start, target, Mathf.Sin((t / inDuration) * Mathf.PI * 0.5f));
            yield return null;
        }
        t = 0f;
        while (t < outDuration)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(target, start, t / outDuration);
            yield return null;
        }
        rect.localScale = start;
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