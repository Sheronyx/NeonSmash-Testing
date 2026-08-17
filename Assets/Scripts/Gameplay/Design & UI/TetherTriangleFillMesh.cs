using System.Collections.Generic;
using UnityEngine;

// Füllt die Fläche zwischen drei Tether Lines mit einem Fächer-Mesh (Zentrum + alle Randpunkte) — nutzt
// dafür die BEREITS von TetherLineFX gezitterten Positionen direkt aus den drei LineRenderern (statt das
// Wackeln separat nachzubauen), damit die Fläche exakt synchron mit den Linien mitwellt, ohne aus dem
// Takt zu laufen. Reihenfolge der Kanten muss den Kreis schließen: A->B, B->C, C->A.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TetherTriangleFillMesh : MonoBehaviour
{
    [Header("Kanten (dieselben LineRenderer wie die Tether Lines, A->B->C->A)")]
    [SerializeField] private LineRenderer edgeAB;
    [SerializeField] private LineRenderer edgeBC;
    [SerializeField] private LineRenderer edgeCA;

    [Header("Farbe pro Ecke")]
    [SerializeField] private Color colorA = Color.green;
    [SerializeField] private Color colorB = Color.blue;
    [SerializeField] private Color colorC = Color.magenta;

    private Mesh _mesh;
    private readonly List<Vector3> _verts = new List<Vector3>();
    private readonly List<Color> _cols = new List<Color>();
    private readonly List<int> _tris = new List<int>();
    private Vector3[] _buffer;

    private void Awake()
    {
        _mesh = new Mesh { name = "TetherTriangleFill" };
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    private void LateUpdate()
    {
        if (edgeAB == null || edgeBC == null || edgeCA == null) return;

        _verts.Clear();
        _cols.Clear();
        _tris.Clear();

        // Randpunkte im Kreis A -> B -> C -> (zurück zu A) sammeln, mit Farbverlauf entlang jeder Kante.
        AppendEdge(edgeAB, colorA, colorB);
        AppendEdge(edgeBC, colorB, colorC);
        AppendEdge(edgeCA, colorC, colorA);

        int rimCount = _verts.Count;
        if (rimCount < 3) return;

        // Zentrum = Durchschnitt aller Randpunkte (bereits lokale Koordinaten), als Fan-Mittelpunkt.
        Vector3 centerLocal = Vector3.zero;
        for (int i = 0; i < rimCount; i++) centerLocal += _verts[i];
        centerLocal /= rimCount;

        Color centerColor = Color.Lerp(Color.Lerp(colorA, colorB, 0.5f), colorC, 1f / 3f);

        int centerIndex = rimCount;
        _verts.Add(centerLocal);
        _cols.Add(centerColor);

        // Fan-Triangulation: jedes Randsegment wird ein Dreieck zum Zentrum, beide Wickelrichtungen für
        // sicheres Rendering unabhängig von Backface Culling.
        for (int i = 0; i < rimCount; i++)
        {
            int next = (i + 1) % rimCount;
            _tris.Add(centerIndex); _tris.Add(i); _tris.Add(next);
            _tris.Add(centerIndex); _tris.Add(next); _tris.Add(i);
        }

        _mesh.Clear();
        _mesh.SetVertices(_verts);
        _mesh.SetColors(_cols);
        _mesh.SetTriangles(_tris, 0);
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    // Hängt alle Punkte einer Kante an (bis auf den letzten, der ja schon der erste Punkt der nächsten
    // Kante ist -- sonst gäbe es am Eckpunkt einen doppelten Vertex).
    private void AppendEdge(LineRenderer line, Color colorStart, Color colorEnd)
    {
        int count = line.positionCount;
        if (count < 2) return;
        if (_buffer == null || _buffer.Length < count) _buffer = new Vector3[count];
        line.GetPositions(_buffer);

        for (int i = 0; i < count - 1; i++)
        {
            float t = (float)i / (count - 1);
            _verts.Add(transform.InverseTransformPoint(_buffer[i]));
            _cols.Add(Color.Lerp(colorStart, colorEnd, t));
        }
    }
}
