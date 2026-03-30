using UnityEngine;
using UnityEngine.EventSystems;

public class UIFloatJitter : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Bewegung")]
    [Tooltip("Maximale Auslenkung in Pixeln (ein paar Millimeter ~ 4–10px).")]
    public float amplitude = 10f;

    [Tooltip("Geschwindigkeit der Drift.")]
    public float speed = 0.35f;

    [Tooltip("Option: Intensiver beim Hovern")]
    public float hoverAmplitudeMultiplier = 1.5f;

    [Tooltip("Zeitskala ignorieren (gut für pausierte Menüs).")]
    public bool useUnscaledTime = true;

    RectTransform rt;
    Vector2 basePos;
    float seedX, seedY;
    bool isHover;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        basePos = rt.anchoredPosition;

        // Unterschiedliche Startphasen je Instanz
        seedX = Random.Range(0f, 1000f);
        seedY = Random.Range(0f, 1000f);
    }

    void OnEnable()
    {
        // Position sauber zurücksetzen
        if (rt != null) rt.anchoredPosition = basePos;
    }

    void Update()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float a = isHover ? amplitude * hoverAmplitudeMultiplier : amplitude;

        // Sanfte, pseudo-zufällige Offsets
        float offX = (Mathf.PerlinNoise(seedX, t * speed) - 0.5f) * 2f * a;
        float offY = (Mathf.PerlinNoise(seedY, t * speed) - 0.5f) * 2f * a;

        // Optional: leichte Phasenmodulation, damit es „lebendiger“ wirkt
        float wobble = Mathf.PerlinNoise(seedX + 77.7f, t * (speed * 0.4f));
        offX *= Mathf.Lerp(0.7f, 1.0f, wobble);
        offY *= Mathf.Lerp(0.7f, 1.0f, 1f - wobble);

        rt.anchoredPosition = basePos + new Vector2(offX, offY);
    }

    // Hover-Boost (für Maus)
    public void OnPointerEnter(PointerEventData eventData) => isHover = true;
    public void OnPointerExit(PointerEventData eventData)  => isHover = false;

    // Falls du den Wrapper zur Laufzeit umpositionierst:
    public void RecalibrateBase() => basePos = rt.anchoredPosition;
}
