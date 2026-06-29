using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingScoreText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Header("Animation")]
    [SerializeField] private float punchDuration = 0.10f;
    [SerializeField] private float punchScale    = 1.35f;
    [SerializeField] private float riseDuration  = 0.55f;
    [SerializeField] private float riseDistance  = 1.4f;

    public void Play(int score, Color color)
    {
        label.text  = "+" + score;
        label.color = color;
        StartCoroutine(Co_Animate());
    }

    private IEnumerator Co_Animate()
    {
        // Punch: 0 → punchScale → 1
        float t = 0f;
        transform.localScale = Vector3.zero;
        while (t < punchDuration)
        {
            t += Time.deltaTime;
            float k = t / punchDuration;
            float s = k < 0.55f
                ? Mathf.Lerp(0f, punchScale, k / 0.55f)
                : Mathf.Lerp(punchScale, 1f, (k - 0.55f) / 0.45f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Float up + fade
        Vector3 startPos   = transform.position;
        Vector3 endPos     = startPos + Vector3.up * riseDistance;
        Color   startColor = label.color;
        Color   endColor   = new Color(startColor.r, startColor.g, startColor.b, 0f);

        t = 0f;
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float k = t / riseDuration;
            transform.position = Vector3.Lerp(startPos, endPos, k);
            label.color        = Color.Lerp(startColor, endColor, k * k); // verzögertes Fade
            yield return null;
        }

        Destroy(gameObject);
    }
}
