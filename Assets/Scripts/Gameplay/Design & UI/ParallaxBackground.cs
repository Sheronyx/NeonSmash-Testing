using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Parallax Settings")]
    [SerializeField] private float scrollSpeed = 0.5f;  // Units pro Sekunde (negativ = links, positiv = rechts)
    [SerializeField] private float tileSize = 10f;      // Breite einer Tile (für nahtloses Loopen)

    private Vector3 originalPosition;

    private void Start()
    {
        originalPosition = transform.position;
    }

    private void Update()
    {
        // Kontinuierliches Scrolling mit nahtlosem Loop
        float offset = (Time.time * scrollSpeed) % tileSize;
        transform.position = originalPosition + new Vector3(-offset, 0, 0);
    }
}
