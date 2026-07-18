using UnityEngine;
using UnityEngine.EventSystems;

// Lässt einen UI-Button beim Drücken sanft kleiner werden und beim Loslassen (oder Wegziehen des
// Fingers) wieder auf seine Ursprungsgröße zurückfedern — reines visuelles Feedback zusätzlich zum
// Button.Transition-System (Color Tint bleibt unberührt). Einfach aufs selbe GameObject wie den
// Button legen. Läuft auf Unscaled Time, damit es auch bei pausiertem Spiel/Time.timeScale=0 reagiert.
public class ButtonPressScale : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Skalierung beim Gedrückthalten, relativ zur Ursprungsgröße (0.9 = 10% kleiner).")]
    [SerializeField] private float pressedScale = 0.9f;
    [Tooltip("Wie schnell zwischen Normal- und Pressed-Skalierung gelerpt wird (höher = schneller).")]
    [SerializeField] private float lerpSpeed = 18f;

    private Vector3 _baseScale;
    private Vector3 _targetScale;

    private void Awake()
    {
        _baseScale   = transform.localScale;
        _targetScale = _baseScale;
    }

    private void Update()
    {
        if (transform.localScale == _targetScale) return;

        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * lerpSpeed);
        if (Vector3.Distance(transform.localScale, _targetScale) < 0.001f)
            transform.localScale = _targetScale;
    }

    public void OnPointerDown(PointerEventData eventData) => _targetScale = _baseScale * pressedScale;
    public void OnPointerUp(PointerEventData eventData)   => _targetScale = _baseScale;
    public void OnPointerExit(PointerEventData eventData) => _targetScale = _baseScale;
}
