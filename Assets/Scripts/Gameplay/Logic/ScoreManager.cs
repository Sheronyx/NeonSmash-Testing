using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Live Score — Temp (gefährdet) / Safe (gesichert)")]
    public TextMeshProUGUI tempScoreText;
    public TextMeshProUGUI safeScoreText;

    [Header("Personal Best")]
    [SerializeField] private TextMeshProUGUI personalBestText;

    public TextMeshProUGUI endscoreText;

    private Coroutine punchRoutine;
    private int _tempScore = 0;
    private int _safeScore = 0;
    private int sessionBest = 0;

    /// <summary>TempScore + SafeScore — für Difficulty-Kurve (InfinityRunManager).</summary>
    public int CurrentScore => _tempScore + _safeScore;

    /// <summary>Punkte die noch nicht gesichert sind — bei Game Over verloren.</summary>
    public int TempScore => _tempScore;

    /// <summary>Gesicherte Punkte (via Orange gebanktes TempScore).</summary>
    public int SafeScore => _safeScore;

    /// <summary>Score der ans Leaderboard gemeldet wird.</summary>
    public int FinalScore => _safeScore;

    private static readonly CultureInfo ScoreCulture = new CultureInfo("en-US");
    public static string Format(int value) => value.ToString("N0", ScoreCulture);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        sessionBest = HighscoreUploader.GetLocalBest(LeaderboardApi.InfinityId);
        UpdateUI();
    }

    private void OnEnable() => UpdateUI();

    public void BindUI(TextMeshProUGUI temp, TextMeshProUGUI best, TextMeshProUGUI safe = null)
    {
        if (temp != null) tempScoreText    = temp;
        if (best != null) personalBestText = best;
        if (safe != null) safeScoreText    = safe;
        UpdateUI();
    }

    /// <summary>Fügt amount direkt zum TempScore hinzu (kein Multiplikator).</summary>
    public void AddPoints(int amount)
    {
        _tempScore += amount;
        UpdateUI();
    }

    /// <summary>10 Punkte × Pink-Multiplikator → TempScore. Gibt tatsächlich addierten Betrag zurück.</summary>
    public int AddPointsFromHit(int basePoints = 10)
    {
        float mult   = ColorEffectManager.Instance != null ? ColorEffectManager.Instance.Multiplier : 1f;
        bool special = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;
        int amount   = Mathf.RoundToInt(basePoints * mult * (special ? 2 : 1));
        _tempScore  += amount;
        UpdateUI();
        return amount;
    }

    /// <summary>Schiebt den gesamten TempScore in den SafeScore. Gibt den gebankten Betrag zurück.</summary>
    public int BankTempScore()
    {
        int banked  = _tempScore;
        _safeScore += banked;
        _tempScore  = 0;
        UpdateUI();
        return banked;
    }

    public void ResetScore()
    {
        _tempScore  = 0;
        _safeScore  = 0;
        sessionBest = HighscoreUploader.GetLocalBest(LeaderboardApi.InfinityId);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (tempScoreText != null)
            tempScoreText.text = Format(_tempScore);

        if (safeScoreText != null)
            safeScoreText.text = Format(_safeScore);

        if (personalBestText != null)
            personalBestText.text = "BEST: " + Format(Mathf.Max(_safeScore, sessionBest));

        PunchScore();
    }

    public void PunchScore()
    {
        if (tempScoreText == null || this == null) return;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(Co_Punch());
    }

    private IEnumerator Co_Punch()
    {
        RectTransform rect = tempScoreText.GetComponent<RectTransform>();
        Vector3 start  = Vector3.one;
        Vector3 target = start * 1.2f;

        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(start, target, Mathf.Sin((t / 0.08f) * Mathf.PI * 0.5f));
            yield return null;
        }
        t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(target, start, t / 0.12f);
            yield return null;
        }
        rect.localScale = start;
    }
}
