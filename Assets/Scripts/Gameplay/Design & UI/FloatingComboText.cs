using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class FloatingComboText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    [Header("Animation")]
    [SerializeField] private float punchInDuration  = 0.15f;
    [SerializeField] private float punchInScale     = 1.35f;
    [SerializeField] private float holdDuration     = 1.50f;
    [SerializeField] private float punchOutDuration = 0.35f;
    [SerializeField] private float punchOutScale    = 1.15f;

    [Header("Materialien pro Farbe")]
    [SerializeField] private Material materialDefault;
    [FormerlySerializedAs("materialRed")]
    [SerializeField] private Material materialPink;
    [SerializeField] private Material materialGreen;
    [FormerlySerializedAs("materialPurple")]
    [SerializeField] private Material materialBlue;

    public void Play(string message, Color color, PointColor? pointColor = null)
    {
        if (label == null) { Destroy(gameObject); return; }
        label.text  = message;
        label.color = color;

        if (pointColor.HasValue)
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
        else if (materialDefault != null)
        {
            label.fontMaterial = materialDefault;
        }

        StartCoroutine(Co_Animate());
    }

    private IEnumerator Co_Animate()
    {
        // Einpoppen: 0 → punchInScale → 1
        float t = 0f;
        transform.localScale = Vector3.zero;
        while (t < punchInDuration)
        {
            t += Time.deltaTime;
            float k = t / punchInDuration;
            float s = k < 0.55f
                ? Mathf.Lerp(0f, punchInScale, k / 0.55f)
                : Mathf.Lerp(punchInScale, 1f, (k - 0.55f) / 0.45f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Stehen bleiben
        yield return new WaitForSeconds(holdDuration);

        // Auspoppen: 1 → punchOutScale → 0
        t = 0f;
        while (t < punchOutDuration)
        {
            t += Time.deltaTime;
            float k = t / punchOutDuration;
            float s = k < 0.35f
                ? Mathf.Lerp(1f, punchOutScale, k / 0.35f)
                : Mathf.Lerp(punchOutScale, 0f, (k - 0.35f) / 0.65f);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.zero;

        Destroy(gameObject);
    }
}
