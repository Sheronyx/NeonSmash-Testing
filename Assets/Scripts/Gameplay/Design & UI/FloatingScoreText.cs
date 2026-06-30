using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingScoreText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Header("Materialien pro Farbe")]
    [SerializeField] private Material materialDefault;
    [SerializeField] private Material materialRed;
    [SerializeField] private Material materialGreen;
    [SerializeField] private Material materialPurple;

    [Header("Animation")]
    [SerializeField] private float punchDuration = 0.08f;
    [SerializeField] private float punchScale    = 1.3f;
    [SerializeField] private float fadeDuration  = 0.35f;

    public void Play(int score, Color color, PointColor? pointColor = null)
    {
        label.text  = "+" + score;
        label.color = color;

        if (pointColor.HasValue)
        {
            Material mat = pointColor.Value switch
            {
                PointColor.Red    => materialRed    ?? materialDefault,
                PointColor.Green  => materialGreen  ?? materialDefault,
                PointColor.Purple => materialPurple ?? materialDefault,
                _                 => materialDefault
            };
            if (mat != null) label.fontMaterial = mat;
        }

        StartCoroutine(Co_Animate());
    }

    private IEnumerator Co_Animate()
    {
        // Einpoppen: 0 → punchScale → 1
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

        // Sofort wegfaden
        Color startColor = label.color;
        Color endColor   = new Color(startColor.r, startColor.g, startColor.b, 0f);
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            label.color = Color.Lerp(startColor, endColor, t / fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
