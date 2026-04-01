using System.Collections;
using TMPro;
using UnityEngine;

public class LevelHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private LevelUp levelSystem;

    private int currentLevel = -1;


    void Start()
    {
        if (levelSystem != null)
        {
            UpdateUI(levelSystem.CurrentLevel);
        }
    }

    private void OnEnable()
    {
        if (levelSystem != null)
            levelSystem.OnLevelChanged += HandleLevelChanged;
    }

    private void OnDisable()
    {
        if (levelSystem != null)
            levelSystem.OnLevelChanged -= HandleLevelChanged;
    }

    private void HandleLevelChanged(int level)
    {
        UpdateUI(level);
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