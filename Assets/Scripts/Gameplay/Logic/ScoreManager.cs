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
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void OnEnable()
    {
        UpdateUI();
    }

    public void AddPoint()
    {
        score++;
        UpdateUI();
    }

    public void AddPoints(int amount)
    {
        score += amount;
        UpdateUI();
    }

    public void ResetScore()
    {
        score = 0;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
        PunchScore();
    }

    public void PunchScore()
    {
        if (scoreText == null)
            return;

        if (punchRoutine != null)
            StopCoroutine(punchRoutine);

        punchRoutine = StartCoroutine(Co_Punch());
    }

    private IEnumerator Co_Punch()
    {
        RectTransform rect = scoreText.GetComponent<RectTransform>();

        Vector3 start = Vector3.one;
        Vector3 target = start * 1.2f;

        float upDuration = 0.08f;
        float downDuration = 0.12f;

        float t = 0f;

        // 🔼 UP (ease out)
        while (t < upDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Sin((t / upDuration) * Mathf.PI * 0.5f);

            rect.localScale = Vector3.Lerp(start, target, p);
            yield return null;
        }

        t = 0f;

        // 🔽 DOWN (ease in)
        while (t < downDuration)
        {
            t += Time.deltaTime;
            float p = t / downDuration;

            rect.localScale = Vector3.Lerp(target, start, p);
            yield return null;
        }

        rect.localScale = start;
    }
}
