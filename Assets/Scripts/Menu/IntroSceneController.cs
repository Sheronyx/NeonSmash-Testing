using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class IntroSceneController : MonoBehaviour
{
    [Header("Ladescreen")]
    [Tooltip("Root-Panel für die Progressbar (im Editor deaktiviert lassen)")]
    [SerializeField] private CanvasGroup loadingGroup;
    [Tooltip("Image: Fill Method = Horizontal, Fill Origin = Left, Fill Amount = 0")]
    [SerializeField] private Image progressBarFill;
    [SerializeField] private TextMeshProUGUI percentageText;
    [SerializeField] private float loadingFadeInDur  = 0.35f;
    [SerializeField] private float loadingFadeOutDur = 0.25f;
    [SerializeField] private float fillSpeed         = 0.32f;
    [SerializeField] private float holdAtFullDur     = 0.45f;

    [Header("Scene")]
    public string nextScene = "MainMenuScene";

    private void Start()
    {
        // SceneFader startet schwarz (alpha=1) — sofort clearen damit der Ladebildschirm sichtbar ist.
        // Am Ende von PlayIntro() nutzen wir ihn wieder für den Übergang zum MainMenu.
        SceneFader.Instance?.Clear();
        StartCoroutine(PlayIntro());
    }

    private IEnumerator PlayIntro()
    {
        // MainMenuScene additiv vorladen — IntroScene bleibt sichtbar
        AsyncOperation preload = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Additive);
        preload.allowSceneActivation = false;

        // Progressbar einblenden — Loading Background ist bereits sichtbar (alpha=1 im Inspector)
        if (loadingGroup != null)
        {
            loadingGroup.gameObject.SetActive(true);
            loadingGroup.alpha = 0f;
            float t = 0f;
            while (t < loadingFadeInDur)
            {
                t += Time.unscaledDeltaTime;
                loadingGroup.alpha = Mathf.Clamp01(t / loadingFadeInDur);
                yield return null;
            }
            loadingGroup.alpha = 1f;
        }

        // Auf GPGS / Game Center Auth warten, Progressbar animieren
        yield return AnimateLoadingPhase();

        // Progressbar ausblenden
        if (loadingGroup != null)
        {
            float t = 0f;
            while (t < loadingFadeOutDur)
            {
                t += Time.unscaledDeltaTime;
                loadingGroup.alpha = 1f - Mathf.Clamp01(t / loadingFadeOutDur);
                yield return null;
            }
            loadingGroup.gameObject.SetActive(false);
        }

        // Vorladen sicherstellen
        while (preload.progress < 0.9f) yield return null;

        // Zu Schwarz faden — alles (Titel, Hintergrund) verschwindet hinter dem SceneFader
        if (SceneFader.Instance != null)
            yield return SceneFader.Instance.FadeToBlack();

        // MainMenuScene aktivieren während Bildschirm schwarz ist
        preload.allowSceneActivation = true;
        yield return null;
        yield return null;

        // Aufblenden
        if (SceneFader.Instance != null)
            yield return SceneFader.Instance.FadeFromBlack();

        // IntroScene entladen
        var mainScene = SceneManager.GetSceneByName(nextScene);
        if (mainScene.IsValid()) SceneManager.SetActiveScene(mainScene);
        SceneManager.UnloadSceneAsync(gameObject.scene);
    }

    private IEnumerator AnimateLoadingPhase()
    {
        if (progressBarFill != null) progressBarFill.fillAmount = 0f;

        // Guard: warten bis UgsBootstrap.Begin() aufgerufen wurde.
        while (!UgsBootstrap.HasBegun) yield return null;

        float fill = 0f;
        var initTask     = UgsBootstrap.Initialization;
        var platformTask = UgsBootstrap.PlatformAuthReady;

        while (true)
        {
            bool initDone     = initTask.IsCompleted;
            bool platformDone = platformTask.IsCompleted;

            float target = platformDone ? 1.00f
                         : initDone     ? 0.90f
                                        : 0.42f;

            float speed = (platformDone && fill > 0.88f) ? fillSpeed * 6f : fillSpeed;
            fill = Mathf.MoveTowards(fill, target, speed * Time.unscaledDeltaTime);

            if (progressBarFill != null)
                progressBarFill.fillAmount = fill;

            if (percentageText != null)
                percentageText.text = "Loading... " +Mathf.FloorToInt(fill * 100f) + "%";

            if (platformDone && fill >= 1f) break;

            yield return null;
        }

        if (progressBarFill != null) progressBarFill.fillAmount = 1f;
        if (percentageText != null) percentageText.text = "100%";
        yield return new WaitForSecondsRealtime(holdAtFullDur);
    }
}
