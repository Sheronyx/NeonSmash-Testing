using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;
using System.Globalization;

public class LevelUp : MonoBehaviour
{
    public event Action<int> OnLevelChanged;
    public int CurrentLevel => currentLevel;

    [Header("Level Panel")]
    [SerializeField] private CanvasGroup levelPanel;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI levelPanelTMP;

    [Header("Getting Faster")]
    [SerializeField] private TextMeshProUGUI gettingFasterTMP;
    [SerializeField] private string gettingFasterText = "Faster!";
    [SerializeField] private Color gettingFasterColor = new Color(1f, 0.85f, 0.95f, 1f);

    [Header("Time Row")]
    [SerializeField] private TextMeshProUGUI timeLabelTMP;
    [SerializeField] private TextMeshProUGUI timeValueTMP;

    [Header("Dots Animation")]
    [SerializeField] private Image[] dotImages;
    [SerializeField] private float dotInterval = 0.35f;
    [SerializeField] private float dotStartScale = 1.4f;
    [SerializeField] private float dotPeakScale = 1.6f;
    [SerializeField] private float dotBounceDuration = 0.22f;
    [SerializeField] private Color dotOffColor = new Color(1,1,1,0.18f);
    [SerializeField] private Color dotOnColor = Color.white;

    [Header("Spawn Pulse")]
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private float startScale = 0.4f;
    [SerializeField] private float overshootScale = 1.08f;
    [SerializeField] private AnimationCurve easeIn = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] private AnimationCurve easeOut = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] private float fadeDuration = 0.14f;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutDuration = 0.18f;
    [SerializeField] private float exitOvershoot = 1.08f;

    [Header("Timing")]
    [SerializeField] private float panelPostDelay = 0.5f;

    private int currentLevel = 1;
    private bool showingPanel = false;

    public bool IsShowingPanel => showingPanel;

    void Awake()
    {
        if (levelPanel)
        {
            levelPanel.alpha = 0f;
            levelPanel.interactable = false;
            levelPanel.blocksRaycasts = false;
        }

        if (panelRoot)
            panelRoot.localScale = Vector3.one;
    }

    // ------------------------------------------------
    // 🔥 NEU: zentrale Pause-Abfrage (sauber & wiederverwendbar)
    // ------------------------------------------------
    private bool IsPaused()
    {
        return PauseMenuController.IsPaused;
    }

    // ------------------------------------------------
    // Level Logic
    // ------------------------------------------------

    public int GetLevelForScore(int score)
    {
        if (score >= 400) return 12;
        if (score >= 350) return 11;
        if (score >= 300) return 10;
        if (score >= 250) return 9;
        if (score >= 200) return 8;
        if (score >= 150) return 7;
        if (score >= 100) return 6;
        if (score >= 80) return 5;
        if (score >= 60) return 4;
        if (score >= 40) return 3;
        if (score >= 20) return 2;
        return 1;
    }

    public float GetReactionTimeForScore(int score, float defaultTime)
    {
        if (score >= 400) return 0.3f;
        if (score >= 350) return 0.4f;
        if (score >= 300) return 0.5f;
        if (score >= 250) return 0.6f;
        if (score >= 200) return 0.7f;
        if (score >= 150) return 0.8f;
        if (score >= 100) return 0.9f;
        if (score >= 80) return 1.0f;
        if (score >= 60) return 1.5f;
        if (score >= 40) return 2.0f;
        if (score >= 20) return 2.5f;

        return defaultTime;
    }

public bool TryTriggerLevelUp(int score)
{
    int level = GetLevelForScore(score);

    if (level > currentLevel)
    {
        currentLevel = level;

        OnLevelChanged?.Invoke(currentLevel);
        return true;
    }

    return false;
}

    // ------------------------------------------------
    // Panel Sequence
    // ------------------------------------------------

    public IEnumerator ShowLevelPanel(int levelNumber, float levelTimeSeconds)
    {
        showingPanel = true;

        levelPanel.interactable = true;
        levelPanel.blocksRaycasts = true;

        if (levelPanelTMP)
            levelPanelTMP.text = $"LEVEL {levelNumber}";

        if (gettingFasterTMP)
        {
            gettingFasterTMP.text = gettingFasterText;
            gettingFasterTMP.color = gettingFasterColor;
        }

        if (timeLabelTMP)
            timeLabelTMP.text = "Smash Time:";

        if (timeValueTMP)
        {
            string secs = levelTimeSeconds < 1f
                ? levelTimeSeconds.ToString("0.##", CultureInfo.InvariantCulture)
                : levelTimeSeconds.ToString("0.0#", CultureInfo.InvariantCulture);

            timeValueTMP.text = $"{secs}s";
        }

        ResetDots();

        yield return StartCoroutine(Co_PulseAndFadeIn());
        yield return PlayDots();
        yield return StartCoroutine(Co_PulseFadeOut());

        // ✅ Pause-respektierendes Delay
        float t = 0f;
        while (t < panelPostDelay)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            yield return null;
        }

        showingPanel = false;
    }

    // ------------------------------------------------
    // Spawn Pulse (Einblenden)
    // ------------------------------------------------

    private IEnumerator Co_PulseAndFadeIn()
    {
        float half = duration * 0.55f;
        float t = 0f;

        panelRoot.localScale = Vector3.one * startScale;
        levelPanel.alpha = 0f;

        while (t < half)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            float p = Mathf.Clamp01(t / half);

            float s = Mathf.Lerp(startScale, overshootScale, easeIn.Evaluate(p));
            panelRoot.localScale = Vector3.one * s;

            float fade = Mathf.Clamp01(t / fadeDuration);
            levelPanel.alpha = fade;

            yield return null;
        }

        float half2 = duration - half;
        t = 0f;

        while (t < half2)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            float p = Mathf.Clamp01(t / half2);

            float s = Mathf.Lerp(overshootScale, 1f, easeOut.Evaluate(p));
            panelRoot.localScale = Vector3.one * s;

            yield return null;
        }

        panelRoot.localScale = Vector3.one;
        levelPanel.alpha = 1f;
    }

    // ------------------------------------------------
    // Dots
    // ------------------------------------------------

    private void ResetDots()
    {
        if (dotImages == null) return;

        foreach (var img in dotImages)
        {
            if (!img) continue;

            img.color = dotOffColor;
            img.transform.localScale = Vector3.one * dotStartScale;
        }
    }

    private IEnumerator PlayDots()
    {
        for (int i = 0; i < dotImages.Length; i++)
        {
            var img = dotImages[i];

            img.color = dotOnColor;

            yield return DotBounce(img);

            float t = 0f;
            while (t < dotInterval)
            {
                if (!IsPaused())
                    t += Time.unscaledDeltaTime;

                yield return null;
            }
        }
    }

    private IEnumerator DotBounce(Image img)
    {
        float t = 0f;

        while (t < dotBounceDuration)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            float p = Mathf.Clamp01(t / dotBounceDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3f);

            float scale = p < 0.5f
                ? Mathf.Lerp(dotStartScale, dotPeakScale, ease)
                : Mathf.Lerp(dotPeakScale, dotStartScale, ease);

            img.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        img.transform.localScale = Vector3.one * dotStartScale;
    }

    // ------------------------------------------------
    // Fade Out
    // ------------------------------------------------

    private IEnumerator Co_PulseFadeOut()
    {
        float overshootTime = fadeOutDuration * 0.25f;
        float shrinkTime = fadeOutDuration - overshootTime;

        float t = 0f;

        while (t < overshootTime)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            float p = Mathf.Clamp01(t / overshootTime);
            float s = Mathf.Lerp(1f, exitOvershoot, p);

            panelRoot.localScale = Vector3.one * s;

            yield return null;
        }

        t = 0f;

        while (t < shrinkTime)
        {
            if (!IsPaused())
                t += Time.unscaledDeltaTime;

            float p = Mathf.Clamp01(t / shrinkTime);

            float scale = Mathf.Lerp(exitOvershoot, 0f, p);
            float alpha = Mathf.Lerp(1f, 0f, p);

            panelRoot.localScale = Vector3.one * scale;
            levelPanel.alpha = alpha;

            yield return null;
        }

        panelRoot.localScale = Vector3.zero;
        levelPanel.alpha = 0f;

        levelPanel.interactable = false;
        levelPanel.blocksRaycasts = false;
    }
}