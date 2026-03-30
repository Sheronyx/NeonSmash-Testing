using UnityEngine;
using UnityEngine.UI;

public class LoadingSpinner : MonoBehaviour
{
    [SerializeField] float speed = 180f;          // Grad/Sekunde
    [SerializeField] bool clockwise = true;       // Richtung
    [SerializeField] bool useUnscaledTime = true; // dreht auch bei pausiertem TimeScale
    [Header("Optional")]
    [SerializeField] Image image;                 // falls du Sprite hier setzen willst
    [SerializeField] Sprite sprite;

    void Awake()
    {
        if (!image) image = GetComponent<Image>();
        if (image && sprite) image.sprite = sprite;
    }

    void OnEnable()
    {
        // Start-Orientierung (optional)
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float dir = clockwise ? -1f : 1f; // UI wirkt je nach Pivot "spiegelverkehrt"
        transform.Rotate(0f, 0f, dir * speed * dt, Space.Self);
    }
}
