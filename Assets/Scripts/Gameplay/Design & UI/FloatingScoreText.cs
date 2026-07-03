using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingScoreText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Header("Materialien pro Farbe")]
    [SerializeField] private Material materialDefault;
    [SerializeField] private Material materialPink;
    [SerializeField] private Material materialBlue;
    [SerializeField] private Material materialGreen;
    [SerializeField] private Material materialOrange;

    [Header("Animation")]
    [SerializeField] private float punchDuration = 0.08f;
    [SerializeField] private float punchScale    = 1.3f;
    [SerializeField] private float fadeDuration  = 0.35f;

    private float _scale = 1f;

    public void Play(int score, Color color, PointColor? pointColor = null, float scale = 1f)
    {
        label.text  = "+" + score;
        label.color = color;
        _scale      = scale;

        if (pointColor.HasValue)
        {
            Material mat = pointColor.Value switch
            {
                PointColor.Pink   => materialPink   ?? materialDefault,
                PointColor.Blue   => materialBlue   ?? materialDefault,
                PointColor.Green  => materialGreen  ?? materialDefault,
                PointColor.Orange => materialOrange ?? materialDefault,
                _                 => materialDefault
            };
            if (mat != null) label.fontMaterial = mat;
        }

        StartCoroutine(Co_Animate());
    }

    private IEnumerator Co_Animate()
    {
        // Einpoppen: 0 → punchScale*_scale → _scale
        float t = 0f;
        transform.localScale = Vector3.zero;
        while (t < punchDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / punchDuration;
            float s = k < 0.55f
                ? Mathf.Lerp(0f, punchScale * _scale, k / 0.55f)
                : Mathf.Lerp(punchScale * _scale, _scale, (k - 0.55f) / 0.45f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one * _scale;

        // Sofort wegfaden
        Color startColor = label.color;
        Color endColor   = new Color(startColor.r, startColor.g, startColor.b, 0f);
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            label.color = Color.Lerp(startColor, endColor, t / fadeDuration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
