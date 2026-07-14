using System.Collections;
using UnityEngine;

// Lässt den GlowColor-Wert des Fairy-Sprite-Materials kurz aufblitzen (Intensity springt z.B. von
// -10 auf 0 und wieder zurück), getriggert sobald eine Energiekugel bei dieser Fairy ankommt
// (FairyEnergyManager). Arbeitet direkt mit den rohen HDR-Farbwerten des Materials — die
// "Intensity" ist nur eine Editor-Anzeige, im Material selbst steckt sie bereits in den RGB-Werten.
public class FairyGlowFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string glowColorProperty = "_GlowColor";
    [Tooltip("Die helle GlowColor beim Aufblitzen (z.B. dieselbe Farbe mit HDR-Intensity 0 statt -10) " +
             "— im Material-Inspector per HDR-Farbwähler abgreifen und hier eintragen.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color brightGlowColor = Color.white;
    [SerializeField] private float flashUpDuration   = 0.06f;
    [SerializeField] private float flashHoldDuration = 0.04f;
    [SerializeField] private float flashDownDuration = 0.25f;

    private Material mat;
    private Color baseColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        mat = spriteRenderer.material; // Unity instanziert automatisch eine Kopie — beeinflusst keine
                                        // anderen Nutzer desselben Ausgangs-Materials.
        baseColor = mat.GetColor(glowColorProperty);
    }

    public void Flash()
    {
        if (mat == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Co_Flash());
    }

    private IEnumerator Co_Flash()
    {
        float t = 0f;
        while (t < flashUpDuration)
        {
            t += Time.deltaTime;
            mat.SetColor(glowColorProperty, Color.Lerp(baseColor, brightGlowColor, t / flashUpDuration));
            yield return null;
        }
        mat.SetColor(glowColorProperty, brightGlowColor);

        yield return new WaitForSeconds(flashHoldDuration);

        t = 0f;
        while (t < flashDownDuration)
        {
            t += Time.deltaTime;
            mat.SetColor(glowColorProperty, Color.Lerp(brightGlowColor, baseColor, t / flashDownDuration));
            yield return null;
        }
        mat.SetColor(glowColorProperty, baseColor);
        flashRoutine = null;
    }
}
