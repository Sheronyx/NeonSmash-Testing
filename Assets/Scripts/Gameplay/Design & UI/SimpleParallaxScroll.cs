using UnityEngine;

public class SimpleParallaxScroll : MonoBehaviour
{
    [Header("Parallax Settings")]
    [SerializeField] private float scrollSpeed = 0.5f;  // Units pro Sekunde

    [Header("Child References")]
    [SerializeField] private Transform image1;  // Erstes Bild
    [SerializeField] private Transform image2;  // Zweites Bild (Kopie)

    private float imageWidth;
    private float scrollPosition = 0f;

    private void Start()
    {
        // Breite automatisch vom SpriteRenderer berechnen
        SpriteRenderer sr = image1.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            imageWidth = sr.bounds.size.x;
            Debug.Log($"[ParallaxScroll] Image Width automatisch berechnet: {imageWidth}");
        }
        else
        {
            Debug.LogError("[ParallaxScroll] Kein SpriteRenderer auf Image1 gefunden!");
        }
    }

    private void Update()
    {
        // Kontinuierlich scrollen
        scrollPosition += scrollSpeed * Time.deltaTime;

        // Wenn über imageWidth hinaus, zurücksetzen
        if (scrollPosition >= imageWidth)
        {
            scrollPosition -= imageWidth;
        }

        // Beide Bilder bewegen
        image1.localPosition = new Vector3(-scrollPosition, image1.localPosition.y, 0);
        image2.localPosition = new Vector3(imageWidth - scrollPosition, image2.localPosition.y, 0);
    }
}
