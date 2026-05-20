using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DailyRewardPopupController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private CanvasGroup dimOverlay;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI dayLabel;     // e.g. "Day 3"
    [SerializeField] private TextMeshProUGUI streakLabel;  // e.g. "3-Day Streak!"
    [SerializeField] private TextMeshProUGUI coinsLabel;   // e.g. "+100 Coins"

    [Header("Streak Day Icons (optional, 7 total)")]
    [Tooltip("One Image per streak day — active day gets full alpha, past days half, future days dim")]
    [SerializeField] private Image[] dayIcons;

    [Header("Coins")]
    [SerializeField] private RectTransform coinsRow;

    [Header("Claim Button")]
    [SerializeField] private Button claimButton;

    [Header("Animation")]
    [SerializeField] private float delayAfterFade   = 0.4f;
    [SerializeField] private float popInDuration    = 0.3f;
    [SerializeField] private float popOutDuration   = 0.25f;
    [SerializeField] private float dimTargetAlpha   = 0.6f;

    void Start()
    {
        if (panel != null) panel.gameObject.SetActive(false);
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

        if (DailyRewardManager.CanClaimToday)
            StartCoroutine(Co_Show());
    }

    void OnEnable()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaim);
    }

    void OnDisable()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(OnClaim);
    }

    void OnClaim()
    {
        int earned = DailyRewardManager.ClaimTodayReward();
        if (earned > 0)
        {
            Vector3 source = coinsRow != null ? coinsRow.position : panel.transform.position;
            CoinDisplayUI.Instance?.FlyCoinsFrom(earned, source);
            StartCoroutine(Co_Hide());
        }
    }

    IEnumerator Co_Show()
    {
        // Wait until scene fade is done
        if (SceneFader.Instance != null)
            yield return new WaitUntil(() => SceneFader.Instance == null || !SceneFader.Instance.IsFading);
        else
            yield return new WaitForSecondsRealtime(0.5f);

        yield return new WaitForSecondsRealtime(delayAfterFade);

        RefreshText();

        if (dimOverlay != null) { dimOverlay.gameObject.SetActive(true); dimOverlay.alpha = 0f; }
        if (panel      != null) { panel.gameObject.SetActive(true);      panel.alpha = 0f; }

        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.7f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel      != null) panel.alpha      = p;
            if (dimOverlay != null) dimOverlay.alpha  = p * dimTargetAlpha;
            if (rt         != null) rt.localScale     = Vector3.Lerp(Vector3.one * 0.7f, Vector3.one, p);
            yield return null;
        }

        if (panel      != null) panel.alpha = 1f;
        if (dimOverlay != null) dimOverlay.alpha = dimTargetAlpha;
        if (rt         != null) rt.localScale = Vector3.one;
    }

    IEnumerator Co_Hide()
    {
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel      != null) panel.alpha     = 1f - p;
            if (dimOverlay != null) dimOverlay.alpha = Mathf.Lerp(dimTargetAlpha, 0f, p);
            if (rt         != null) rt.localScale    = Vector3.Lerp(Vector3.one, Vector3.one * 0.7f, p);
            yield return null;
        }

        if (panel      != null) panel.gameObject.SetActive(false);
        if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
    }

    void RefreshText()
    {
        int streak  = DailyRewardManager.CurrentStreak + 1; // next claimed streak
        int reward  = DailyRewardManager.TodayRewardAmount;

        if (dayLabel    != null) dayLabel.text    = $"Day {streak}";
        if (streakLabel != null) streakLabel.text = streak > 1 ? $"{streak}-Day Streak!" : "Daily Reward";
        if (coinsLabel  != null) coinsLabel.text  = $"+{reward}";

        RefreshDayIcons(streak);
    }

    void RefreshDayIcons(int activeDay)
    {
        if (dayIcons == null) return;
        for (int i = 0; i < dayIcons.Length; i++)
        {
            if (dayIcons[i] == null) continue;
            int day = i + 1;
            float alpha = day < activeDay ? 0.5f : day == activeDay ? 1f : 0.25f;
            var c = dayIcons[i].color;
            dayIcons[i].color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
