using System.Collections;
using UnityEngine;

// Lässt die GlowColor ALLER zugewiesenen Fairy-Materialien (Core + jeder Flügel, jeweils eigenes
// Material) kurz aufblitzen UND die ganze Fairy synchron kurz aufpulsieren (Scale-Punch), getriggert
// sobald eine Energiekugel bei dieser Fairy ankommt (FairyEnergyManager). Braucht KEINE fest
// eingetragene Zielfarbe: jedes Material behält seinen eigenen Farbton, nur die Intensität wird
// pro Material relativ zu dessen AKTUELLEM Wert hochskaliert (glowBoostMultiplier).
public class FairyGlowFlash : MonoBehaviour
{
    [Tooltip("Core + alle Flügel-SpriteRenderer. Leer lassen = automatisch alle SpriteRenderer in den Kindern.")]
    [SerializeField] private SpriteRenderer[] glowRenderers;
    [SerializeField] private string glowColorProperty = "_GlowColor";
    [Tooltip("Wie stark die AKTUELLE GlowColor jedes Materials beim Aufblitzen hochskaliert wird — " +
             "per Auge auf \"wirkt wie Intensity 3\" eintunen, kein fester Unity-Intensity-Wert.")]
    [SerializeField] private float glowBoostMultiplier = 1000f;
    [SerializeField] private float flashUpDuration   = 0.06f;
    [SerializeField] private float flashHoldDuration = 0.04f;
    [SerializeField] private float flashDownDuration = 0.25f;

    [Header("Scale-Pulse (synchron zum Glow)")]
    [Tooltip("Um wie viel die Fairy beim Aufblitzen kurz größer wird (1.2 = 20% größer).")]
    [SerializeField] private float pulseScaleMultiplier = 1.2f;

    private Material[] mats;
    private Color[] baseColors;
    private Vector3 baseScale;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (glowRenderers == null || glowRenderers.Length == 0)
            glowRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        mats       = new Material[glowRenderers.Length];
        baseColors = new Color[glowRenderers.Length];
        for (int i = 0; i < glowRenderers.Length; i++)
        {
            if (glowRenderers[i] == null) continue;
            var candidate = glowRenderers[i].material; // Unity instanziert automatisch eine Kopie

            // Nicht jedes Kind-Sprite hat zwingend ein Glow-Material (z.B. Standard-Sprite-Shader
            // ohne _GlowColor) — HasProperty verhindert die wiederholten Unity-Fehlermeldungen
            // "doesn't have a color property", die sonst bei jedem Flash()-Aufruf geloggt würden.
            if (!candidate.HasProperty(glowColorProperty)) continue;

            mats[i]       = candidate;
            baseColors[i] = mats[i].GetColor(glowColorProperty);
        }
    }

    public void Flash()
    {
        // Erst HIER (statt in Awake()) erfassen: die Fee wird beim Spielstart erst von
        // FairyArrivalSequence auf ihre eigentliche Ruheposition/-größe gebracht — ein in Awake()
        // gecachter Wert würde noch die (kleine/unsichtbare) Startgröße vor dieser Sequenz erwischen,
        // und Co_Flash() würde die Fee später fälschlich darauf zurücksetzen ("verschwindet").
        // Energiekugeln treffen ohnehin erst lange nach Abschluss der Ankunfts-Sequenz ein, daher ist
        // die aktuelle Skalierung zu diesem Zeitpunkt garantiert die richtige.
        baseScale = transform.localScale;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Co_Flash());
    }

    private IEnumerator Co_Flash()
    {
        Vector3 peakScale = baseScale * pulseScaleMultiplier;

        float t = 0f;
        while (t < flashUpDuration)
        {
            t += Time.deltaTime;
            float k = t / flashUpDuration;
            ApplyGlow(k);
            transform.localScale = Vector3.Lerp(baseScale, peakScale, k);
            yield return null;
        }
        ApplyGlow(1f);
        transform.localScale = peakScale;

        yield return new WaitForSeconds(flashHoldDuration);

        t = 0f;
        while (t < flashDownDuration)
        {
            t += Time.deltaTime;
            float k = t / flashDownDuration;
            ApplyGlow(1f - k);
            transform.localScale = Vector3.Lerp(peakScale, baseScale, k);
            yield return null;
        }
        ApplyGlow(0f);
        transform.localScale = baseScale;
        flashRoutine = null;
    }

    // k=0 → Basis-Glow, k=1 → hochskalierter Boost-Glow (pro Material relativ zu dessen eigenem
    // Ausgangswert, kein gemeinsamer fixer Farbwert).
    private void ApplyGlow(float k)
    {
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            Color boosted = baseColors[i] * glowBoostMultiplier;
            mats[i].SetColor(glowColorProperty, Color.LerpUnclamped(baseColors[i], boosted, k));
        }
    }
}
