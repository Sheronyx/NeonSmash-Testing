using UnityEngine;

// Belebt ein gefaktes Lichtstrahl-Sprite (God Ray) sehr günstig: dezentes Pulsieren
// der Helligkeit (Alpha), leichtes seitliches Wiegen und sanftes „Atmen" der Breite.
// Kein Light-Objekt, kein Post-Processing — nur ein paar sin-Werte pro Frame.
//
// Setup: weiches, additives Strahlen-Sprite (SpriteRenderer) + dieses Skript.
[RequireComponent(typeof(SpriteRenderer))]
public class LightShaftShimmer : MonoBehaviour
{
    [Header("Helligkeit (Alpha-Puls)")]
    [Tooltip("Grund-Helligkeit.")]
    [Range(0f, 1f)] [SerializeField] private float baseAlpha = 0.6f;
    [Tooltip("Wie stark die Helligkeit pulsiert (±).")]
    [Range(0f, 1f)] [SerializeField] private float alphaAmplitude = 0.15f;
    [SerializeField] private float alphaFrequency = 0.25f;

    [Header("Seitliches Wiegen (Grad)")]
    [SerializeField] private float swayAngle = 1.5f;
    [SerializeField] private float swayFrequency = 0.13f;

    [Header("Breiten-Atmen (Scale X)")]
    [Tooltip("Wie stark die Breite atmet (±, relativ). 0 = aus.")]
    [SerializeField] private float widthAmplitude = 0.04f;
    [SerializeField] private float widthFrequency = 0.18f;

    private SpriteRenderer sr;
    private Quaternion baseRot;
    private Vector3 baseScale;
    private float phaseA, phaseS, phaseW;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseRot = transform.localRotation;
        baseScale = transform.localScale;

        // Zufalls-Phasen, falls mehrere Strahlen → nicht synchron.
        phaseA = Random.Range(0f, Mathf.PI * 2f);
        phaseS = Random.Range(0f, Mathf.PI * 2f);
        phaseW = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float t = Time.time;

        float a = baseAlpha + Mathf.Sin(t * alphaFrequency * Mathf.PI * 2f + phaseA) * alphaAmplitude;
        Color c = sr.color;
        c.a = Mathf.Clamp01(a);
        sr.color = c;

        if (swayAngle != 0f)
        {
            float ang = Mathf.Sin(t * swayFrequency * Mathf.PI * 2f + phaseS) * swayAngle;
            transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, ang);
        }

        if (widthAmplitude != 0f)
        {
            float w = 1f + Mathf.Sin(t * widthFrequency * Mathf.PI * 2f + phaseW) * widthAmplitude;
            transform.localScale = new Vector3(baseScale.x * w, baseScale.y, baseScale.z);
        }
    }
}
