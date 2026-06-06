using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI endscoreText;
    private Coroutine punchRoutine;
    private int score = 0;

    public int CurrentScore => score;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable() => UpdateUI();

    public void AddPoints(int amount)
    {
        score += amount;
        UpdateUI();
    }

    // basePoints = 10 per hit; multiplied by combo and special mode
    public void AddPointsFromHit(int basePoints = 10)
    {
        int combo   = ComboManager.Instance != null ? ComboManager.Instance.Multiplier : 1;
        bool special = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;
        score += basePoints * combo * (special ? 2 : 1);
        UpdateUI();
    }

    public void ResetScore()
    {
        score = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = score.ToString();
        PunchScore();
    }

    public void PunchScore()
    {
        if (scoreText == null) return;
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
