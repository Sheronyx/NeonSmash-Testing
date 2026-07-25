using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class FloatingScoreText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Header("Materialien pro Farbe")]
    [SerializeField] private Material materialDefault;
    [FormerlySerializedAs("materialRed")]
    [SerializeField] private Material materialPink;
    [SerializeField] private Material materialGreen;
    [FormerlySerializedAs("materialPurple")]
    [SerializeField] private Material materialBlue;

    [Header("Animation")]
    [SerializeField] private float punchDuration = 0.08f;
    [SerializeField] private float punchScale    = 1.3f;
    [SerializeField] private float fadeDuration  = 0.35f;

    private float _scale = 1f;

    public void Play(int score, Color color, PointColor? pointColor = null, float scale = 1f,
        Material explicitMaterial = null, bool showPlusPrefix = true, float holdDuration = 0f, string overrideText = null)
    {
        // Negative Werte (z.B. Zufallsbox-Minuspunkte-Effekt) tragen ihr Minuszeichen schon selbst über
        // ToString() — ein "+" davor würde ein falsches "+-10" ergeben.
        label.text  = overrideText ?? (showPlusPrefix && score >= 0 ? "+" + score : score.ToString());
        label.color = color;
        _scale      = scale;

        if (explicitMaterial != null)
        {
            // Direkt übergebenes Material (z.B. Special-Mode-Treffer) hat Vorrang vor der Farb-Auswahl.
            label.fontMaterial = explicitMaterial;
        }
        else if (pointColor.HasValue)
        {
            Material mat = pointColor.Value switch
            {
                PointColor.Pink   => materialPink   ?? materialDefault,
                PointColor.Green  => materialGreen  ?? materialDefault,
                PointColor.Blue   => materialBlue   ?? materialDefault,
                _                 => materialDefault
            };
            if (mat != null) label.fontMaterial = mat;
        }

        StartCoroutine(Co_Animate(holdDuration));
    }

    private IEnumerator Co_Animate(float holdDuration = 0f)
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

        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // Wegfaden
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
