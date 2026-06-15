using UnityEngine;

public class SimpleParallaxScroll : MonoBehaviour
{
    [Header("Parallax Settings")]
    [SerializeField] private float scrollSpeed = 0.5f;
    [Tooltip("Winziger Überlapp gegen Naht-Flackern (z.B. 0.02). 0 = exakt aneinander.")]
    [SerializeField] private float seamOverlap = 0f;

    [Header("Child References")]
    [SerializeField] private Transform image1;
    [SerializeField] private Transform image2;  // darf gespiegelt sein (Scale X = -1)

    private float imageWidth;
    private float Step => imageWidth - seamOverlap;

    private void Start()
    {
        SpriteRenderer sr = image1.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float pixelsPerUnit = sr.sprite.pixelsPerUnit;
            imageWidth = (sr.sprite.rect.width / pixelsPerUnit) * Mathf.Abs(image1.localScale.x);
        }
        else
        {
            Debug.LogError("[ParallaxScroll] Kein SpriteRenderer/Sprite auf Image1 gefunden!");
            return;
        }

        // image1 links, image2 direkt rechts daneben (mit optionalem Überlapp)
        image1.localPosition = new Vector3(0f,    image1.localPosition.y, 0f);
        image2.localPosition = new Vector3(Step,  image2.localPosition.y, 0f);
    }

    private void Update()
    {
        Vector3 move = Vector3.left * scrollSpeed * Time.deltaTime;
        image1.localPosition += move;
        image2.localPosition += move;

        // Leapfrog: das komplett links rausgelaufene Bild hinter das andere setzen.
        // Nur EINES springt → das (gespiegelte) Muster bleibt nahtlos erhalten.
        Recycle(image1, image2);
        Recycle(image2, image1);
    }

    private void Recycle(Transform img, Transform other)
    {
        if (img.localPosition.x <= -imageWidth)
        {
            img.localPosition = new Vector3(
                other.localPosition.x + Step,
                img.localPosition.y,
                0f);
        }
    }
}
