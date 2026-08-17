using UnityEngine;

// Füllt die Fläche zwischen drei Ankerpunkten (z.B. den drei Feen) mit einem einzigen Dreiecks-Mesh,
// live nachgezogen, falls sich die Anker bewegen (Feen schweben ja). Pro Ecke eine eigene Vertex-Farbe
// gesetzt, damit die Fläche denselben Grün/Blau/Pink-Farbverlauf zeigt wie die Tether Lines — das
// Material muss dafür Vertex-Farben auslesen (siehe Hinweis unten).
//
// Die Transparenz der Fläche folgt zusätzlich live der übrigen Reaktionszeit der aktuellen Elementreihe
// (PhaseManager.CurrentRowProgress01) — gleiches Prinzip/gleiche Richtung wie bei TetherReactionWidth:
// volles Alpha bei frisch gespawnter Reihe, Richtung 0 kurz vorm Ablaufen.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TriangleFillMesh : MonoBehaviour
{
    [Header("Ecken (3 Anker)")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private Transform pointC;

    [Header("Farbe pro Ecke (RGB + Basis-Alpha)")]
    [SerializeField] private Color colorA = Color.green;
    [SerializeField] private Color colorB = Color.blue;
    [SerializeField] private Color colorC = Color.magenta;

    [Header("Rand-zu-Mitte Verlauf (Alpha)")]
    [Tooltip("Alpha-Multiplikator an den ECKEN/Rändern des Dreiecks (niedrig = Rand transparent).")]
    [Range(0f, 1f)] [SerializeField] private float edgeAlpha = 0f;
    [Tooltip("Alpha-Multiplikator in der MITTE des Dreiecks (hoch = Mitte kräftig/deckend).")]
    [Range(0f, 1f)] [SerializeField] private float centerAlpha = 1f;

    [Header("Transparenz (Alpha-Multiplikator, wirkt auf Rand UND Mitte gleichermaßen)")]
    [Tooltip("Alpha-Multiplikator direkt nach dem Spawnen einer neuen Reihe (volle Reaktionszeit übrig) " +
             "— wird zusätzlich zu edgeAlpha/centerAlpha oben multipliziert.")]
    [Range(0f, 1f)] [SerializeField] private float alphaAtRowStart = 1f;
    [Tooltip("Alpha-Multiplikator kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [Range(0f, 1f)] [SerializeField] private float alphaAtRowTimeout = 0f;

    [Header("Glättung")]
    [Tooltip("Wie viele Sekunden das Alpha braucht, um komplett vom Start- zum Zielwert zu wechseln — " +
             "wichtig vor allem für den Reset nach einem Treffer, damit es nicht hart zurückspringt.")]
    [SerializeField] private float transitionDuration = 0.3f;

    [Header("Sorting")]
    [Tooltip("Der Standard-Inspector von MeshRenderer zeigt (anders als bei SpriteRenderer/LineRenderer) " +
             "keine Sorting-Layer/Order-Felder an, obwohl der Renderer sie intern hat — wird hier per " +
             "Script gesetzt, Name muss exakt einem existierenden Sorting Layer entsprechen.")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int orderInLayer = 0;

    private Mesh _mesh;
    private float _currentAlpha;

    private void Awake()
    {
        _mesh = new Mesh { name = "TriangleFill" };
        GetComponent<MeshFilter>().mesh = _mesh;

        var renderer = GetComponent<Renderer>();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = orderInLayer;
    }

    private void Start()
    {
        _currentAlpha = alphaAtRowStart;
    }

    private void LateUpdate()
    {
        if (pointA == null || pointB == null || pointC == null) return;

        if (PhaseManager.Instance != null)
        {
            float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
            float targetAlpha = Mathf.Lerp(alphaAtRowStart, alphaAtRowTimeout, progress);

            float dt = Mathf.Max(0.0001f, transitionDuration);
            float maxDelta = Mathf.Abs(alphaAtRowTimeout - alphaAtRowStart) * Time.deltaTime / dt;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, maxDelta);
        }

        // Lokale Koordinaten relativ zu diesem Transform, damit Position/Rotation/Scale des Objekts
        // selbst weiter normal funktionieren (z.B. für Sorting Layer/Order wie bei den Tether Lines).
        Vector3 a = transform.InverseTransformPoint(pointA.position);
        Vector3 b = transform.InverseTransformPoint(pointB.position);
        Vector3 c = transform.InverseTransformPoint(pointC.position);
        Vector3 center = (a + b + c) / 3f;

        // Ecken: niedriges Alpha (edgeAlpha) -> Rand transparent. Mitte: eigener Vertex mit hohem Alpha
        // (centerAlpha) und gemittelter Farbe -> die normale Vertex-Color-Interpolation zwischen Ecken
        // und Mitte erzeugt automatisch den weichen Rand-zu-Mitte-Verlauf, ganz ohne Shader-Textur.
        Color ca = colorA; ca.a *= edgeAlpha * _currentAlpha;
        Color cb = colorB; cb.a *= edgeAlpha * _currentAlpha;
        Color cc = colorC; cc.a *= edgeAlpha * _currentAlpha;

        Color centerColor = (colorA + colorB + colorC) / 3f;
        float centerBaseAlpha = (colorA.a + colorB.a + colorC.a) / 3f;
        centerColor.a = centerBaseAlpha * centerAlpha * _currentAlpha;

        _mesh.Clear();
        _mesh.vertices = new[] { a, b, c, center };
        // Fächer aus Zentrum (Index 3) zu jeder Kante, jeweils beide Wickelrichtungen -> von beiden
        // Seiten sichtbar, unabhängig von Backface Culling im Material.
        _mesh.triangles = new[]
        {
            3, 0, 1, 3, 1, 0,
            3, 1, 2, 3, 2, 1,
            3, 2, 0, 3, 0, 2,
        };
        _mesh.colors = new[] { ca, cb, cc, centerColor };
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }
}
