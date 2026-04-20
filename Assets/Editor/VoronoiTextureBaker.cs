using UnityEngine;
using UnityEditor;
using System.IO;

public class VoronoiTextureBaker : EditorWindow
{
    private int textureSize   = 512;
    private int cellCount     = 12;
    private int seed          = 42;
    private bool drawEdges    = true;
    private float edgeWidth   = 0.06f;
    private bool animate      = false; // false = statische Textur für Shader-Replacement
    private string savePath   = "Assets/Textures/VoronoiBaked.png";

    [MenuItem("Tools/Voronoi Texture Baker")]
    public static void ShowWindow()
    {
        GetWindow<VoronoiTextureBaker>("Voronoi Baker");
    }

    private void OnGUI()
    {
        GUILayout.Label("Voronoi Texture Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        textureSize = EditorGUILayout.IntField("Texture Size", textureSize);
        cellCount   = EditorGUILayout.IntField("Cell Count", cellCount);
        seed        = EditorGUILayout.IntField("Seed", seed);
        EditorGUILayout.Space();
        drawEdges   = EditorGUILayout.Toggle("Draw Edges (Linien)", drawEdges);
        if (drawEdges)
            edgeWidth = EditorGUILayout.Slider("Edge Width", edgeWidth, 0.01f, 0.3f);
        EditorGUILayout.Space();
        savePath    = EditorGUILayout.TextField("Save Path", savePath);

        EditorGUILayout.HelpBox(
            "Baked Texture ersetzt die Echtzeit-Voronoi-Berechnung im Shader.\n" +
            "R-Kanal = Zellabstand (für Fills)\n" +
            "G-Kanal = Kantenmaske (für Linien/Edges)",
            MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake & Save Texture", GUILayout.Height(32)))
            BakeAndSave();
    }

    private void BakeAndSave()
    {
        var tex = Bake();

        string dir = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllBytes(savePath, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.Refresh();

        var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        if (imported != null)
        {
            Debug.Log($"[VoronoiBaker] Gespeichert: {savePath}");
            Selection.activeObject = imported;
            EditorGUIUtility.PingObject(imported);
        }
    }

    private Texture2D Bake()
    {
        var tex = new Texture2D(textureSize, textureSize, TextureFormat.RG16, false);
        var rng = new System.Random(seed);

        // Voronoi-Punkte generieren
        var points = new Vector2[cellCount];
        for (int i = 0; i < cellCount; i++)
            points[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());

        var pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                var uv = new Vector2((float)x / textureSize, (float)y / textureSize);

                float minDist  = float.MaxValue;
                float minDist2 = float.MaxValue;

                foreach (var p in points)
                {
                    // Tiling: kürzeste Distanz über Kacheln hinweg
                    float dx = Mathf.Abs(uv.x - p.x);
                    float dy = Mathf.Abs(uv.y - p.y);
                    dx = Mathf.Min(dx, 1f - dx);
                    dy = Mathf.Min(dy, 1f - dy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    if (d < minDist)      { minDist2 = minDist; minDist = d; }
                    else if (d < minDist2) { minDist2 = d; }
                }

                // R: Zellabstand (normalisiert)
                float cellValue = Mathf.Clamp01(minDist * cellCount * 0.5f);

                // G: Kantenschärfe (Differenz der zwei nächsten Punkte)
                float edge = drawEdges
                    ? Mathf.Clamp01(1f - (minDist2 - minDist) / edgeWidth)
                    : 0f;

                int idx = y * textureSize + x;
                pixels[idx] = new Color(cellValue, edge, 0f, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
