using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Combined Tasks popup: Daily Reward + Missions in one 80%-panel.
// Attach to a root GameObject in MainMenuScene. Wire up all SerializeFields in the Inspector.
// Open via TasksPopupController.Instance.Open() or MainMenuController.OpenTasks().
public class TasksPopupController : MonoBehaviour
{
    public static TasksPopupController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private CanvasGroup panel;

    [Header("Daily Reward")]
    [SerializeField] private TextMeshProUGUI dailyDayLabel;
    [SerializeField] private TextMeshProUGUI dailyCoinsLabel;
    [SerializeField] private Image[]         dailyStreakIcons;   // 7 icons, left to right = Day 1–7
    [SerializeField] private Button          claimButton;
    [SerializeField] private GameObject      claimedState;       // "Already claimed" label/icon
    [SerializeField] private RectTransform   dailyCoinsRow;      // source position for coin fly animation

    [Header("Mission Rows")]
    [SerializeField] private MissionRowUI[] missionRows;         // exactly 3 elements

    [Header("Animation")]
    [SerializeField] private float popInDuration  = 0.28f;
    [SerializeField] private float popOutDuration = 0.2f;

    bool _open;

    void Awake()
    {
        Instance = this;
        if (panel      != null) panel.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (claimButton != null) claimButton.onClick.AddListener(OnClaim);
        MissionManager.OnMissionCompleted += OnMissionCompleted;
    }

    void OnDisable()
    {
        if (claimButton != null) claimButton.onClick.RemoveListener(OnClaim);
        MissionManager.OnMissionCompleted -= OnMissionCompleted;
    }

    public void Open()
    {
        if (_open) return;
        _open = true;
        RefreshAll();
        DimOverlay.Instance?.Show();
        StartCoroutine(Co_Open());
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        DimOverlay.Instance?.Hide();
        StartCoroutine(Co_Close());
    }

    // ── Claim ───────────────────────────────────────────────────────────────

    void OnClaim()
    {
        int earned = DailyRewardManager.ClaimTodayReward();
        if (earned <= 0) return;
        Vector3 src = dailyCoinsRow != null ? dailyCoinsRow.position : panel.transform.position;
        CoinDisplayUI.Instance?.FlyCoinsFrom(earned, src);
        RefreshDaily();
    }

    void OnMissionCompleted(MissionData _, int __)
    {
        if (_open) RefreshMissions();
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    void RefreshAll()
    {
        RefreshDaily();
        RefreshMissions();
    }

    void RefreshDaily()
    {
        bool canClaim  = DailyRewardManager.CanClaimToday;
        int  streak    = DailyRewardManager.CurrentStreak;
        int  reward    = DailyRewardManager.TodayRewardAmount;

        // Active day index: if already claimed today, streak is the just-claimed day
        int activeDay  = canClaim ? Mathf.Clamp(streak + 1, 1, 7) : Mathf.Clamp(streak, 1, 7);

        if (dailyDayLabel   != null) dailyDayLabel.text   = $"Day {activeDay}";
        if (dailyCoinsLabel != null) dailyCoinsLabel.text  = $"+{reward}";
        if (claimButton     != null) claimButton.gameObject.SetActive(canClaim);
        if (claimedState    != null) claimedState.SetActive(!canClaim);

        if (dailyStreakIcons == null) return;
        for (int i = 0; i < dailyStreakIcons.Length; i++)
        {
            if (dailyStreakIcons[i] == null) continue;
            int   day   = i + 1;
            float alpha = day < activeDay ? 0.5f : day == activeDay ? 1f : 0.25f;
            var   c     = dailyStreakIcons[i].color;
            dailyStreakIcons[i].color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    void RefreshMissions()
    {
        var missions = MissionManager.GetTodaysMissions();
        for (int i = 0; i < missionRows.Length && i < missions.Length; i++)
            missionRows[i].Bind(missions[i]);
    }

    // ── Animation ───────────────────────────────────────────────────────────

    IEnumerator Co_Open()
    {
        if (panel != null) { panel.gameObject.SetActive(true); panel.alpha = 0f; }

        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel != null) panel.alpha   = p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, p);
            yield return null;
        }

        if (panel != null) panel.alpha   = 1f;
        if (rt    != null) rt.localScale = Vector3.one;
    }

    IEnumerator Co_Close()
    {
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel != null) panel.alpha   = 1f - p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, p);
            yield return null;
        }

        if (panel != null) panel.gameObject.SetActive(false);
    }
}
