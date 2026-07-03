using UnityEngine;

// Positioniert dieses GameObject zur Laufzeit auf die Weltkoordinaten eines UI-Elements.
// Als Particle-System Collision Plane einsetzen → funktioniert auf allen Bildschirmgrößen.
public class UICollisionPlane : MonoBehaviour
{
    [Tooltip("Viewport Y-Position der Plane (0=unten, 1=oben). Justiere bis es mit der Top Bar übereinstimmt.")]
    [Range(0f, 1f)]
    [SerializeField] private float viewportY = 0.92f;

    [Tooltip("Feinabstimmung in Welteinheiten (+ = höher, - = tiefer).")]
    [SerializeField] private float yOffset = 0f;

    private void Awake()
    {
        if (Camera.main == null) return;
        Camera cam = Camera.main;
        float z = Mathf.Abs(cam.transform.position.z);
        if (z < 0.01f) z = 10f;
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(0.5f, viewportY, z));
        transform.position = new Vector3(transform.position.x, world.y + yOffset, transform.position.z);
    }
}
