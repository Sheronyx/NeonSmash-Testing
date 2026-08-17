using UnityEngine;

// Verbindet die 3 Feen als Kette (Fee1-Fee2, Fee2-Fee3) über zwei LineRenderer, deren Breite live die
// verbleibende Reaktionszeit der aktuellen Elementreihe anzeigt. Nutzt dieselbe Datenquelle wie
// EyeRaysIntensity (PhaseManager.CurrentRowProgress01), aber umgekehrte Richtung: DICK = viel Zeit
// übrig, DÜNN = Zeit läuft ab — Positionen folgen den Feen live, falls die selbst schweben/wackeln.
//
// "Plasma"-Look (Textur/additives Material) ist Material-Sache auf den LineRenderern selbst (Editor)
// — dieses Script kümmert sich um Position, Breite UND das fließende Scrollen der Textur-UVs.
public class FairyPlasmaLink : MonoBehaviour
{
    [Header("Feen (Kette: 1-2, 2-3)")]
    [SerializeField] private Transform fairy1;
    [SerializeField] private Transform fairy2;
    [SerializeField] private Transform fairy3;

    [Header("Linien")]
    [SerializeField] private LineRenderer line12;
    [SerializeField] private LineRenderer line23;

    [Header("Trails entlang der Linie (optional)")]
    [Tooltip("Particle Systems mit Shape=Edge, Radius=0.5, Scaling Mode=Local/Hierarchy — werden jeden " +
             "Frame auf Position/Winkel/Länge der jeweiligen Linie gestreckt, Partikel fließen dann " +
             "sichtbar von einer Fee zur anderen. Leer lassen, falls (noch) nicht gewünscht.")]
    [SerializeField] private ParticleSystem trail12;
    [SerializeField] private ParticleSystem trail23;

    [Header("Breite")]
    [Tooltip("Linienbreite direkt nach dem Spawnen einer neuen Reihe (volle Reaktionszeit übrig).")]
    [SerializeField] private float widthAtRowStart = 0.25f;
    [Tooltip("Linienbreite kurz bevor die Reaktionszeit dieser Reihe abläuft.")]
    [SerializeField] private float widthAtRowTimeout = 0.05f;

    [Header("Glättung")]
    [Tooltip("Wie viele Sekunden die Breite braucht, um komplett vom Start- zum Zielwert zu wechseln " +
             "(gleiches Prinzip wie bei EyeRaysIntensity — wichtig vor allem für den Reset nach einem Treffer).")]
    [SerializeField] private float transitionDuration = 0.3f;

    [Header("Plasma-Fluss (UV-Scroll)")]
    [Tooltip("Wie schnell die Textur über die Linie 'fließt' (UV-Einheiten pro Sekunde). Braucht ein " +
             "Material mit Textur auf beiden LineRenderern, sonst ohne sichtbaren Effekt.")]
    [SerializeField] private float scrollSpeed = 1f;

    private float _currentWidth;
    private Vector3 _trail12BaseScale;
    private Vector3 _trail23BaseScale;

    private void Start()
    {
        _currentWidth = widthAtRowStart;

        // Y/Z der ursprünglich im Editor gesetzten Skalierung erhalten (z.B. Partikelbreite/-tiefe) —
        // nur X wird pro Frame auf die aktuelle Linienlänge gesetzt.
        if (trail12 != null) _trail12BaseScale = trail12.transform.localScale;
        if (trail23 != null) _trail23BaseScale = trail23.transform.localScale;
    }

    private void Update()
    {
        if (PhaseManager.Instance == null) return;

        float progress = PhaseManager.Instance.CurrentRowProgress01; // 0 = frisch gespawnt, 1 = abgelaufen
        float targetWidth = Mathf.Lerp(widthAtRowStart, widthAtRowTimeout, progress);

        float dt = Mathf.Max(0.0001f, transitionDuration);
        float maxDelta = Mathf.Abs(widthAtRowTimeout - widthAtRowStart) * Time.deltaTime / dt;
        _currentWidth = Mathf.MoveTowards(_currentWidth, targetWidth, maxDelta);

        ApplyLine(line12, fairy1, fairy2);
        ApplyLine(line23, fairy2, fairy3);

        float scrollOffset = Time.time * scrollSpeed;
        ScrollTexture(line12, scrollOffset);
        ScrollTexture(line23, scrollOffset);

        PositionTrail(trail12, fairy1, fairy2, _trail12BaseScale);
        PositionTrail(trail23, fairy2, fairy3, _trail23BaseScale);
    }

    private void ApplyLine(LineRenderer line, Transform a, Transform b)
    {
        if (line == null || a == null || b == null) return;

        line.positionCount = 2;
        line.SetPosition(0, a.position);
        line.SetPosition(1, b.position);
        line.startWidth = _currentWidth;
        line.endWidth   = _currentWidth;
    }

    // .material (statt .sharedMaterial) legt beim ersten Zugriff automatisch eine Instanz-Kopie an —
    // wir verändern also nur die Laufzeit-Kopie dieses LineRenderers, nicht das geteilte Material-Asset.
    private void ScrollTexture(LineRenderer line, float offsetX)
    {
        if (line == null || line.material == null) return;

        Vector2 texOffset = line.material.mainTextureOffset;
        texOffset.x = offsetX;
        line.material.mainTextureOffset = texOffset;
    }

    // Streckt/dreht/positioniert das ganze Particle-System-Transform so, dass seine lokale X-Achse
    // exakt von a nach b zeigt — bei Shape=Edge, Radius=0.5, Scaling Mode=Local/Hierarchy ergibt
    // localScale.x = Abstand(a,b) eine Emissions-Kante in genau dieser Länge. Y/Z bleiben wie im Editor
    // gesetzt (z.B. für die Partikelgröße relevant), nur X wird pro Frame überschrieben.
    private void PositionTrail(ParticleSystem trail, Transform a, Transform b, Vector3 baseScale)
    {
        if (trail == null || a == null || b == null) return;

        Vector3 diff = b.position - a.position;
        float dist = diff.magnitude;
        if (dist < 0.0001f) return;

        var t = trail.transform;
        t.position = (a.position + b.position) * 0.5f;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(dist, baseScale.y, baseScale.z);
    }
}
