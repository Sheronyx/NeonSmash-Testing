using UnityEngine;

// Lässt das Vorschau-Modell im Skin-Shop-Tab gleichmäßig um die Y-Achse rotieren
// (Brawl-Stars-artiger Effekt für die Live-3D-Karten).
public class ShopPreviewRotator : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 25f;

    void Update()
    {
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
    }
}
