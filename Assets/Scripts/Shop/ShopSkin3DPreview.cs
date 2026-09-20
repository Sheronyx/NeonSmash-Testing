using UnityEngine;
using UnityEngine.UI;

// Zeigt pro Shop-Karte eine echte 3D-Figur -- OHNE RenderTexture und ohne eigene Kamera.
//
// Warum: Die Canvases laufen im Modus "Screen Space - Camera" über die Main Camera. Ein normales
// 3D-Objekt in der Welt wird dadurch von derselben Kamera im selben Durchgang gerendert und per
// z-Position zwischen die UI-Ebenen einsortiert -- genau so funktioniert die Fee im Startmenü
// (dort ist z = -5 der entscheidende Wert). Nur auf diesem Weg stimmen Auflösung, Beleuchtung und
// vor allem die Outline (ein Fullscreen-Renderer-Feature, siehe "Free Outline Settings - FairyTest")
// exakt mit der Startmenü-Fee überein. Der frühere RenderTexture-Umweg konnte das prinzipbedingt
// nicht: eigene Auflösung, eigenes AA, eigener Tiefenpuffer -- daher Unschärfe und der sichtbare
// Spalt zwischen Rahmen und Modell.
//
// Die Figur wird bewusst NICHT auf die Karte beschnitten. UI-Masken beschneiden ohnehin nur
// Canvas-Inhalte, keine Weltgeometrie -- und das Überstehen über den Kartenrand ist in Titeln wie
// Brawl Stars gewollter Teil des Looks. Sobald die Karte aus dem Scroll-Bereich läuft, wird die
// Figur komplett ausgeblendet.
public class ShopSkin3DPreview : MonoBehaviour
{
    [Tooltip("Die 3D-Figur dieses Skins -- dasselbe Prefab, das auch im Startmenü benutzt wird " +
             "(z.B. FairyCrystal_3D). Muss auf dem Outline-Layer der jeweiligen Fee liegen, " +
             "damit der schwarze Rahmen greift.")]
    [SerializeField] GameObject characterPrefab;

    [Tooltip("Welt-z, auf dem die Figur platziert wird. -5 entspricht der Startmenü-Fee und liegt " +
             "vor den UI-Ebenen.")]
    [SerializeField] float worldDepth = -5f;

    [Tooltip("Y-Rotation der Figur. -180 entspricht der Startmenü-Fee (Blick zur Kamera).")]
    [SerializeField] float yRotation = -180f;

    [Tooltip("Zusätzlicher Skalierungsfaktor auf die Prefab-Größe. 1 = exakt so groß wie im Startmenü.")]
    [SerializeField] float scaleMultiplier = 1f;

    [Tooltip("Verschiebung gegenüber der Kartenmitte, in Karten-Prozent (0.5 = halbe Kartenbreite).")]
    [SerializeField] Vector2 cardOffset = Vector2.zero;

    RectTransform _rect;
    Canvas        _canvas;
    RectTransform _viewport;
    CanvasGroup[] _fadeGroups;
    GameObject    _instance;
    float         _boundsCenterY;    // Höhe der Figur, relativ zu ihrer Position
    float         _boundsHalfHeight;

    void Awake()
    {
        _rect   = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();

        // Die Figur ist ein Welt-3D-Objekt und wird von der CanvasGroup des Shop-Panels NICHT
        // mitgeblendet. Ohne diese Prüfung bliebe sie beim Schließen noch sichtbar, während das
        // Panel schon ausblendet.
        _fadeGroups = GetComponentsInParent<CanvasGroup>(true);

        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll != null) _viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
    }

    void OnEnable()
    {
        if (_instance != null || characterPrefab == null) return;

        _instance = Instantiate(characterPrefab);
        _instance.transform.rotation   = Quaternion.Euler(0f, yRotation, 0f);
        _instance.transform.localScale = characterPrefab.transform.localScale * scaleMultiplier;
        CacheHeight();
    }

    // Die Höhe der Figur einmalig merken: Renderer-Bounds sind bei deaktiviertem Objekt nicht
    // zuverlässig -- gebraucht werden sie aber gerade dann, um zu entscheiden, ob die Figur wieder
    // vollständig in den Scroll-Bereich passt und eingeblendet werden darf.
    void CacheHeight()
    {
        var renderers = _instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        _boundsCenterY    = bounds.center.y - _instance.transform.position.y;
        _boundsHalfHeight = bounds.extents.y;
    }

    void OnDisable()
    {
        if (_instance == null) return;
        Destroy(_instance);
        _instance = null;
    }

    // LateUpdate, damit Layout und Scrolling dieses Frames bereits angewendet sind -- sonst hängt
    // die Figur beim Scrollen sichtbar einen Frame hinter ihrer Karte her.
    void LateUpdate()
    {
        if (_instance == null) return;

        var cam = _canvas != null ? _canvas.worldCamera : null;
        if (cam == null) return;

        var rect = _rect.rect;
        Vector3 anchorWorld = _rect.TransformPoint(new Vector3(cardOffset.x * rect.width,
                                                              cardOffset.y * rect.height,
                                                              0f));

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, anchorWorld);

        // Bei einer orthografischen Kamera ist die z-Komponente der Abstand entlang der Blickrichtung.
        float distance = worldDepth - cam.transform.position.z;
        Vector3 world  = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, distance));
        _instance.transform.position = world;

        bool visible = IsFullyFadedIn() && IsWorthRendering(cam, world);
        if (_instance.activeSelf != visible) _instance.SetActive(visible);
    }

    bool IsFullyFadedIn()
    {
        if (_fadeGroups == null) return true;

        foreach (var group in _fadeGroups)
            if (group != null && group.alpha < 0.999f) return false;

        return true;
    }

    // Die Figur wird NICHT ausgeblendet, sobald sie den Scroll-Rand berührt -- Header und Tab Bar
    // liegen in einem eigenen Canvas vor ihr (siehe ShopFrontLayer), sie gleitet also sauber
    // dahinter. Ausgeblendet wird nur, was ohnehin komplett außerhalb liegt.
    bool IsWorthRendering(Camera cam, Vector3 world)
    {
        if (_viewport == null) return true;

        Rect  view    = ScreenRect(_viewport, cam);
        float centerY = world.y + _boundsCenterY;
        float top     = cam.WorldToScreenPoint(new Vector3(world.x, centerY + _boundsHalfHeight, world.z)).y;
        float bottom  = cam.WorldToScreenPoint(new Vector3(world.x, centerY - _boundsHalfHeight, world.z)).y;

        return top >= view.yMin && bottom <= view.yMax;
    }

    static Rect ScreenRect(RectTransform rt, Camera cam)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return new Rect(min, max - min);
    }
}
