using System.Collections;
using TMPro;
using UnityEngine;

public class LevelHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private LevelUp levelSystem;

    private int currentLevel = -1;

    void Update()
    {
        if (levelSystem == null) return;

        int level = levelSystem.GetLevelForScore(
            ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0
        );

        if (level != currentLevel)
        {
            currentLevel = level;
            UpdateUI(level);
        }
    }

    private IEnumerator Pulse()
    {
        Vector3 original = levelText.rectTransform.localScale;
        Vector3 target = original * 1.2f;

        float t = 0f;
        float duration = 0.2f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Sin((t / duration) * Mathf.PI);
            levelText.rectTransform.localScale = Vector3.Lerp(original, target, p);
            yield return null;
        }

        levelText.rectTransform.localScale = original;
    }

    private void UpdateUI(int level)
    {
        levelText.text = $"{level}";
        StartCoroutine(Pulse());
    }
}