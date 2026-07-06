using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Live Score — Temp (gefährdet) / Safe (gesichert)")]
    public TextMeshProUGUI tempScoreText;
    public TextMeshProUGUI safeScoreText;

    [Header("Personal Best")]
    [SerializeField] private TextMeshProUGUI personalBestText;

    public TextMeshProUGUI endscoreText;

    [Header("Roll-Animation (Passives Scoring)")]
    [SerializeField] private float rollDuration = 0.35f;

    [Header("Bank-Animation (Orange)")]
    [Tooltip("Prefab: einfaches oranges Kreis-Image (RectTransform + Image). Wird per Code animiert.")]
    [SerializeField] private RectTransform bankOrbPrefab;
    [SerializeField] private float bankOrbDuration  = 0.55f;
    [SerializeField] private float bankOrbArcHeight = 200f;

    private int   _tempScore = 0;
    private int   _safeScore = 0;
    private int   sessionBest = 0;

    // Rolling Counter
    private float     _displayedTempScore;
    private Coroutine _rollRoutine;

    // Safe Score Punch
    private Coroutine _punchTempRoutine;
    private Coroutine _punchSafeRoutine;

    /// <summary>TempScore + SafeScore — für Difficulty-Kurve (InfinityRunManager).</summary>
    public int CurrentScore => _tempScore + _safeScore;
    public int TempScore    => _tempScore;
    public int SafeScore    => _safeScore;
    public int FinalScore   => _safeScore;

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
        _displayedTempScore = _tempScore;
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

    // ─── Scoring ──────────────────────────────────────────────────────────────

    /// <summary>Passives Scoring — Pink-Multiplikator wird angewandt, startet Rolling-Animation.</summary>
    public int AddPointsPassive(int basePoints = 10)
    {
        float mult   = ColorEffectManager.Instance != null ? ColorEffectManager.Instance.Multiplier : 1f;
        bool special = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;
        int amount   = Mathf.RoundToInt(basePoints * mult * (special ? 2 : 1));
        _tempScore  += amount;

        StartRoll();
        UpdateBestText();
        return amount;
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

    /// <summary>TempScore → SafeScore. Startet Orb-Animation. Gibt gebankten Betrag zurück.</summary>
    public int BankTempScore()
    {
        // Roll abbrechen — TempScore springt sofort auf 0
        if (_rollRoutine != null) { StopCoroutine(_rollRoutine); _rollRoutine = null; }

        int banked  = _tempScore;
        _safeScore += banked;
        _tempScore  = 0;
        _displayedTempScore = 0f;

        if (tempScoreText    != null) tempScoreText.text    = Format(0);
        UpdateBestText();
        // SafeScore-Text wird NACH der Orb-Animation aktualisiert
        if (banked > 0)
            StartCoroutine(Co_BankAnimation());
        else if (safeScoreText != null)
            safeScoreText.text = Format(_safeScore);

        return banked;
    }

    public void ResetScore()
    {
        if (_rollRoutine != null) { StopCoroutine(_rollRoutine); _rollRoutine = null; }
        _tempScore          = 0;
        _safeScore          = 0;
        _displayedTempScore = 0f;
        sessionBest         = HighscoreUploader.GetLocalBest(LeaderboardApi.InfinityId);
        UpdateUI();
    }

    // ─── UI ───────────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (_rollRoutine == null)
        {
            _displayedTempScore = _tempScore;
            if (tempScoreText != null) tempScoreText.text = Format(_tempScore);
        }
        if (safeScoreText    != null) safeScoreText.text    = Format(_safeScore);
        UpdateBestText();
        PunchScore();
    }

    private void UpdateBestText()
    {
        if (personalBestText != null)
            personalBestText.text = "BEST: " + Format(Mathf.Max(_safeScore, sessionBest));
    }

    public void PunchScore()
    {
        if (tempScoreText == null || this == null) return;
        if (_punchTempRoutine != null) StopCoroutine(_punchTempRoutine);
        _punchTempRoutine = StartCoroutine(Co_PunchText(tempScoreText.rectTransform, 1.2f, 0.08f, 0.12f));
    }

    // ─── Rolling Counter ──────────────────────────────────────────────────────

    private void StartRoll()
    {
        if (_rollRoutine != null) StopCoroutine(_rollRoutine);
        _rollRoutine = StartCoroutine(Co_RollTempScore(_tempScore));
    }

    private IEnumerator Co_RollTempScore(int target)
    {
        float start = _displayedTempScore;
        float t     = 0f;
        while (t < rollDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / rollDuration), 2f); // EaseOut
            _displayedTempScore = Mathf.Lerp(start, target, k);
            if (tempScoreText != null)
                tempScoreText.text = Format(Mathf.RoundToInt(_displayedTempScore));
            yield return null;
        }
        _displayedTempScore = target;
        if (tempScoreText != null) tempScoreText.text = Format(target);
        _rollRoutine = null;
    }

    // ─── Bank Animation ───────────────────────────────────────────────────────

    private IEnumerator Co_BankAnimation()
    {
        if (bankOrbPrefab != null && tempScoreText != null && safeScoreText != null)
        {
            Canvas canvas = tempScoreText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var orb = Instantiate(bankOrbPrefab, canvas.transform);
                orb.position = tempScoreText.transform.position;

                Vector3 startPos = tempScoreText.transform.position;
                Vector3 endPos   = safeScoreText.transform.position;
                Vector3 midPos   = Vector3.Lerp(startPos, endPos, 0.5f)
                                 + Vector3.up * bankOrbArcHeight;

                float t = 0f;
                while (t < bankOrbDuration)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / bankOrbDuration);
                    // EaseInOut für weichere Bewegung
                    float ek = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) / 2f;
                    orb.position = QuadBezier(startPos, midPos, endPos, ek);
                    yield return null;
                }
                Destroy(orb.gameObject);
            }
            else
            {
                yield return new WaitForSecondsRealtime(bankOrbDuration);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(bankOrbDuration * 0.5f);
        }

        // SafeScore-Text aktualisieren wenn Orb ankommt
        if (safeScoreText != null) safeScoreText.text = Format(_safeScore);
        PunchSafeScore();
    }

    private static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        => Vector3.Lerp(Vector3.Lerp(a, b, t), Vector3.Lerp(b, c, t), t);

    private void PunchSafeScore()
    {
        if (safeScoreText == null) return;
        if (_punchSafeRoutine != null) StopCoroutine(_punchSafeRoutine);
        _punchSafeRoutine = StartCoroutine(Co_PunchText(safeScoreText.rectTransform, 1.35f, 0.1f, 0.18f));
    }

    // ─── Generische Punch-Animation ───────────────────────────────────────────

    private IEnumerator Co_PunchText(RectTransform rect, float peakScale, float inDur, float outDur)
    {
        Vector3 start  = Vector3.one;
        Vector3 target = Vector3.one * peakScale;

        float t = 0f;
        while (t < inDur)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(start, target, Mathf.Sin((t / inDur) * Mathf.PI * 0.5f));
            yield return null;
        }
        t = 0f;
        while (t < outDur)
        {
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(target, start, t / outDur);
            yield return null;
        }
        rect.localScale = start;
    }
}
