using UnityEngine;

// Scrollt die gesamte Blatt-Gruppe (Seitenblätter) endlos von oben nach unten
// (World/Local Space, SpriteRenderer — kein Canvas nötig) und wackelt dabei leicht
// im Wind: langsame Z-Sway-Schwingung + optionales schnelleres Flattern.
//
// Eine Kopie der Gruppe wird direkt darüber platziert → nahtloser Loop (Leapfrog).
// Wie aus dem Fly-High-Projekt. Flutter auf 0 = exakt das Original-Verhalten.
public class LeafSideLooper : MonoBehaviour
{
    [Header("Scroll")]
    [SerializeField] private float scrollSpeed = 3f;     // World Units/Sekunde nach unten
    [Tooltip("Höhe der Gruppe in World Units. 0 = automatisch aus Kinder-Sprites berechnen.")]
    [SerializeField] private float groupHeight = 0f;

    [Header("Wind-Schwingung (Z-Rotation)")]
    [SerializeField] private float swayAmplitude = 4f;
    [SerializeField] private float swayFrequency = 0.25f;
    [SerializeField] private float swayPhaseOffset = 0f;

    [Header("Flattern (schnelles Mini-Wackeln obendrauf)")]
    [Tooltip("Max. Ausschlag des Flatterns (Grad). 0 = aus (wie Fly High).")]
    [SerializeField] private float flutterAmplitude = 0f;
    [Tooltip("Zyklen pro Sekunde des Flatterns (deutlich höher als Sway).")]
    [SerializeField] private float flutterFrequency = 1.6f;

    private Transform _copy;
    private float _loopThresholdY;
    private float _startY;

    private void Start()
    {
        // groupHeight auto: echte Höhe der Kinder-Sprites messen
        if (groupHeight <= 0f)
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length > 0)
            {
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var r in renderers)
                {
                    minY = Mathf.Min(minY, r.bounds.min.y);
                    maxY = Mathf.Max(maxY, r.bounds.max.y);
                }
                groupHeight = maxY - minY;
            }
            else if (Camera.main != null)
                groupHeight = Camera.main.orthographicSize * 2f;
        }

        _startY = transform.localPosition.y;
        _loopThresholdY = _startY - groupHeight;

        // Kopie direkt über dem Original platzieren
        var copyGo = Instantiate(gameObject, transform.parent);
        copyGo.name = gameObject.name + "_Loop";

        var looperOnCopy = copyGo.GetComponent<LeafSideLooper>();
        if (looperOnCopy != null) looperOnCopy.enabled = false;

        _copy = copyGo.transform;
        _copy.localPosition = new Vector3(
            transform.localPosition.x,
            _startY + groupHeight,
            transform.localPosition.z);
        _copy.SetSiblingIndex(transform.GetSiblingIndex() + 1);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;

        float sway = Mathf.Sin(t * swayFrequency * Mathf.PI * 2f + swayPhaseOffset) * swayAmplitude;
        float flutter = flutterAmplitude != 0f
            ? Mathf.Sin(t * flutterFrequency * Mathf.PI * 2f + swayPhaseOffset) * flutterAmplitude
            : 0f;

        var swayRot = Quaternion.Euler(0f, 0f, sway + flutter);
        var move = Vector3.down * scrollSpeed * dt;

        transform.localPosition += move;
        transform.localRotation = swayRot;

        _copy.localPosition += move;
        _copy.localRotation = swayRot;

        // Loop: fällt ein Tile unter die Schwelle → über das andere springen
        if (transform.localPosition.y < _loopThresholdY)
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                _copy.localPosition.y + groupHeight,
                transform.localPosition.z);

        if (_copy.localPosition.y < _loopThresholdY)
            _copy.localPosition = new Vector3(
                _copy.localPosition.x,
                transform.localPosition.y + groupHeight,
                _copy.localPosition.z);
    }
}
