using UnityEngine;

public class BackgroundLooperPerfect : MonoBehaviour
{
    [SerializeField] float scrollSpeed = 1.2f;

    Transform a, b;
    float h;               // echte Höhe in Weltkoordinaten (inkl. Scale)
    float pix;             // 1 Pixel in Weltkoordinaten
    float overlap;         // minimale Überlappung (½ Pixel)

    void Start()
    {
        a = transform.GetChild(0);
        b = transform.GetChild(1);

        var sr = a.GetComponent<SpriteRenderer>();
        h = sr.bounds.size.y;

        // 1 Pixel in Weltkoordinaten (PPU berücksichtigt Scale):
        // Achtung: bei ungleich skalierten Eltern ggf. lossyScale.y statt localScale verwenden
        float ppu = sr.sprite.pixelsPerUnit;
        pix = (1f / ppu) * a.lossyScale.y;
        overlap = pix * 0.5f;

        // Start sauber & pixelgenau ausrichten
        Vector3 basePos = new Vector3(RoundToPixel(a.position.x), RoundToPixel(a.position.y), 0f);
        a.position = basePos;
        b.position = new Vector3(basePos.x, basePos.y + h - overlap, 0f); // kleine Überlappung
    }

    void Update()
    {
        Vector3 d = Vector3.down * scrollSpeed * Time.deltaTime;
        a.position += d;
        b.position += d;

        // Pixel-Snap Y jedes Frame (verhindert Subpixel-Flimmern an der Kante)
        a.position = new Vector3(a.position.x, RoundToPixel(a.position.y), 0f);
        b.position = new Vector3(b.position.x, RoundToPixel(b.position.y), 0f);

        // Re-Stack mit Überlappung
        if (a.position.y <= -h)
            a.position = new Vector3(a.position.x, b.position.y + h - overlap, 0f);
        if (b.position.y <= -h)
            b.position = new Vector3(b.position.x, a.position.y + h - overlap, 0f);
    }

    float RoundToPixel(float v)
    {
        return Mathf.Round(v / pix) * pix;
    }
}
