using UnityEngine;

// Hält ein World-Space-Objekt (z.B. SpriteRenderer/ParticleSystem-Prefabs wie DiamondBonusIndicatorUI,
// die NICHT unter dem Canvas hängen können) live an der Bildschirmposition eines UI-Elements fest.
// Nötig, weil eine einmalig im Play Mode eingemessene World-Position nur für die eine Auflösung/das eine
// Seitenverhältnis stimmt, mit dem sie gesetzt wurde — auf anderen Geräten verschiebt sich sonst die Lücke
// zwischen Weltkoordinate und tatsächlicher Bildschirmposition der UI-Box.
public class WorldSpaceUIAnchor : MonoBehaviour
{
    [Tooltip("UI-Element, dessen Bildschirmposition dieses Objekt verfolgt (z.B. die Pink/Green/Blue-Fortschrittsbox).")]
    [SerializeField] private RectTransform anchorUIElement;
    [Tooltip("Versatz in Canvas-LOKALEN Einheiten (relativ zur Größe/Skalierung des Anchor-UI-Elements), " +
             "NICHT in rohen Bildschirm-Pixeln — sonst wirkt der Versatz je nach Gerät/Canvas-Scale-Factor " +
             "unterschiedlich groß. Größenordnung wie andere anchoredPosition-Werte in diesem UI (z.B. 120–150).")]
    [SerializeField] private Vector2 canvasOffset = new Vector2(0f, 120f);
    [Tooltip("Optional: Kamera fest zuweisen (wie mainCamera in MixedPointSpawner) statt dich auf Camera.main " +
             "zu verlassen — Camera.main kann falsch greifen, wenn über DontDestroyOnLoad noch eine zweite " +
             "MainCamera-getaggte Kamera aus einer vorherigen Szene existiert. Leer lassen = Fallback auf Camera.main.")]
    [SerializeField] private Camera cam;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (anchorUIElement == null || cam == null) return;

        // Versatz VOR der Screen-Umrechnung in Canvas-lokalem Raum anwenden (TransformPoint läuft durch
        // die komplette Parent-Kette inkl. des vom Canvas Scaler skalierten Canvas-Transforms) — dadurch
        // bleibt der Versatz auf jedem Gerät visuell gleich groß, statt in rohen Pixeln zu "schrumpfen"
        // oder zu "wachsen", wenn der Scale Factor vom Referenz-Gerät abweicht.
        Vector3 anchoredWorldPoint = anchorUIElement.TransformPoint(new Vector3(canvasOffset.x, canvasOffset.y, 0f));

        // "Canvas Top Bar" läuft im Render Mode "Screen Space - Camera", nicht Overlay — dort ist
        // RectTransform.position KEINE Bildschirm-Pixel-Koordinate, sondern eine echte Weltposition auf
        // einer Ebene vor der Kamera. WorldToScreenPoint übersetzt das für JEDEN Canvas-Render-Modus
        // korrekt in echte Bildschirm-Pixel (bei Overlay wäre cam hier egal, bei Camera/World Space nicht).
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, anchoredWorldPoint);

        var ray = cam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 p = ray.GetPoint(enter);
            p.z = 0f;
            transform.position = p;
        }
    }
}
