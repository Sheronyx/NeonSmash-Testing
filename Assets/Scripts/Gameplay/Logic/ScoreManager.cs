using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Live Score")]
    public TextMeshProUGUI scoreText;

    [Header("Personal Best (kleines Label unter dem Score)")]
    [SerializeField] private TextMeshProUGUI personalBestText;

    public TextMeshProUGUI endscoreText;

    private Coroutine punchRoutine;
    private int score      = 0;
    private int sessionBest = 0;   // Bestwert zu Spielbeginn (für In-Game-Anzeige)

    public int CurrentScore => score;

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

    // Bindet die UI-Felder der aktuell aktiven Top Bar (z.B. nach einem
    // Skin-Bar-Swap) und rendert den aktuellen Stand neu.
    public void BindUI(TextMeshProUGUI score, TextMeshProUGUI best)
    {
        if (score != null) scoreText        = score;
        if (best  != null) personalBestText = best;
        UpdateUI();
    }

    public void AddPoints(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public int AddPointsFromHit(int basePoints = 10, int extraMultiplier = 1)
    {
        bool special = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;
        // Special-Multiplikator: phasenbasiert (Infinity Mode) via PhaseManager, sonst Fallback 2x (Multiplayer/o. PhaseManager).
        int specialMult = special ? (PhaseManager.Instance != null ? PhaseManager.Instance.CurrentSpecialMultiplier : 2) : 1;
        int amount   = basePoints * specialMult * extraMultiplier;
        score += amount;
        UpdateUI();
        return amount;
    }

    public void ResetScore()
    {
        score = 0;
        sessionBest = HighscoreUploader.GetLocalBest(LeaderboardApi.InfinityId);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = Format(score);

        if (personalBestText != null)
            personalBestText.text = "BEST: " + Format(Mathf.Max(score, sessionBest));

        PunchScore();
    }

    public void PunchScore()
    {
        if (scoreText == null || this == null) return;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(Co_Punch());
    }

    private IEnumerator Co_Punch()
    {
        RectTransform rect = scoreText.GetComponent<RectTransform>();
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
