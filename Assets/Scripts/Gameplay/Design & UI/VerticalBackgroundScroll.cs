using UnityEngine;

// Vertikaler Endlos-Scroll für den Base-Background (Tropical Jungle): das Bild
// scrollt oben → unten. Zwei gestapelte Kind-Bilder werden per Leapfrog recycelt,
// sodass keine Naht entsteht. Analog zu SimpleParallaxScroll, nur vertikal.
public class VerticalBackgroundScroll : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("Geschwindigkeit nach unten (Units/Sek).")]
    [SerializeField] private float scrollSpeed = 0.5f;
    [Tooltip("Winziger Überlapp gegen Naht-Flackern (z.B. 0.02). 0 = exakt aneinander.")]
    [SerializeField] private float seamOverlap = 0f;

    [Header("Child References (gestapelt)")]
    [SerializeField] private Transform image1;
    [SerializeField] private Transform image2;  // darf gespiegelt sein (Scale Y = -1)

    private float imageHeight;
    private float Step => imageHeight - seamOverlap;

    private void Start()
    {
        SpriteRenderer sr = image1 != null ? image1.GetComponent<SpriteRenderer>() : null;
        if (sr != null && sr.sprite != null)
        {
            float ppu = sr.sprite.pixelsPerUnit;
            imageHeight = (sr.sprite.rect.height / ppu) * Mathf.Abs(image1.localScale.y);
        }
        else
        {
            Debug.LogError("[VerticalBackgroundScroll] Kein SpriteRenderer/Sprite auf Image1 gefunden!");
            enabled = false;
            return;
        }

        // image1 unten, image2 direkt darüber (mit optionalem Überlapp)
        image1.localPosition = new Vector3(image1.localPosition.x, 0f,   0f);
        image2.localPosition = new Vector3(image2.localPosition.x, Step, 0f);
    }

    private void Update()
    {
        Vector3 move = Vector3.down * scrollSpeed * Time.deltaTime;
        image1.localPosition += move;
        image2.localPosition += move;

        // Leapfrog: das unten komplett rausgelaufene Bild über das andere setzen.
        // Nur EINES springt → das (ggf. gespiegelte) Muster bleibt nahtlos.
        Recycle(image1, image2);
        Recycle(image2, image1);
    }

    private void Recycle(Transform img, Transform other)
    {
        if (img.localPosition.y <= -imageHeight)
        {
            img.localPosition = new Vector3(
                img.localPosition.x,
                other.localPosition.y + Step,
                0f);
        }
    }
}
