using UnityEngine;

// Laesst ein UI-Element (Screen Space Overlay Canvas) einem Weltraum-Objekt "folgen" —
// z.B. den Color-Progress-Balken unter der Spieler-Fee. Das UI-Objekt bleibt dabei ein
// ganz normales Canvas-Kind (RectTransform, Image Filled etc. funktionieren unveraendert),
// es wird nur jeden Frame per WorldToScreenPoint neu positioniert. Kein World Space Canvas
// noetig, kein zusaetzliches Setup pro Fee-Instanz.
[RequireComponent(typeof(RectTransform))]
public class UIWorldFollow : MonoBehaviour
{
    [Tooltip("Das Weltraum-Objekt, dem das UI-Element folgen soll (z.B. die Spieler-Fee).")]
    [SerializeField] private Transform target;

    [Tooltip("Kamera, die fuer die Weltraum-zu-Bildschirm-Umrechnung genutzt wird. Leer = Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("Versatz in Weltraum-Einheiten relativ zum Ziel, z.B. (0, -0.6, 0) fuer 'unterhalb der Fee'.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, -0.6f, 0f);

    [Tooltip("Zusaetzlicher Versatz in UI-Pixeln, nachdem die Weltposition auf den Bildschirm umgerechnet wurde.")]
    [SerializeField] private Vector2 screenOffset = Vector2.zero;

    [Tooltip("UI-Element ausblenden, wenn das Ziel gerade hinter der Kamera liegt (verhindert Sprung an die falsche Bildschirmseite).")]
    [SerializeField] private bool hideWhenBehindCamera = true;

    private RectTransform _rect;
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>Ziel zur Laufzeit setzen, z.B. sobald feststeht, welche Fee gerade aktiv ist.</summary>
    public void SetTarget(Transform newTarget) => target = newTarget;

    /// <summary>Weltraum-Versatz zur Laufzeit/per Editor-Skript setzen.</summary>
    public void SetWorldOffset(Vector3 newOffset) => worldOffset = newOffset;

    /// <summary>Nur zum Debuggen/Prüfen im Editor (z.B. per Skript).</summary>
    public Transform DebugTarget => target;
    public Camera DebugWorldCamera => worldCamera;

    private void LateUpdate()
    {
        if (target == null || _canvas == null) return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return;

        Vector3 worldPos = target.position + worldOffset;
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPos);

        bool behindCamera = screenPoint.z < 0f;
        if (hideWhenBehindCamera && _canvasGroup != null)
            _canvasGroup.alpha = behindCamera ? 0f : 1f;
        if (behindCamera) return;

        screenPoint.x += screenOffset.x;
        screenPoint.y += screenOffset.y;

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            _rect.position = screenPoint;
        }
        else
        {
            // Screen Space - Camera / World Space Canvas: ueber die Canvas-eigene Kamera umrechnen
            Camera canvasCam = _canvas.renderMode == RenderMode.ScreenSpaceCamera ? _canvas.worldCamera : cam;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)_canvas.transform, screenPoint, canvasCam, out var worldPoint))
            {
                _rect.position = worldPoint;
            }
        }
    }
}
