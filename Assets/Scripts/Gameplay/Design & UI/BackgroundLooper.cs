using UnityEngine;

public class BackgroundLooperPerfect : MonoBehaviour
{
    [SerializeField] float scrollSpeed = 1.2f;

float currentSpeed;
float targetSpeed;
[SerializeField] float smoothTime = 0.5f;
float velocity;

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

    float ppu = sr.sprite.pixelsPerUnit;
    pix = (1f / ppu) * a.lossyScale.y;
    overlap = pix * 0.5f;

    Vector3 basePos = new Vector3(RoundToPixel(a.position.x), RoundToPixel(a.position.y), 0f);
    a.position = basePos;
    b.position = new Vector3(basePos.x, basePos.y + h - overlap, 0f);

    // 👉 initial nach unten
    currentSpeed = -scrollSpeed;
    targetSpeed = -scrollSpeed;
}

void Update()
{
    // 👉 smooth Übergang zur Zielgeschwindigkeit
    currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref velocity, smoothTime);

    Vector3 d = Vector3.up * currentSpeed * Time.deltaTime;

    a.position += d;
    b.position += d;

    a.position = new Vector3(a.position.x, RoundToPixel(a.position.y), 0f);
    b.position = new Vector3(b.position.x, RoundToPixel(b.position.y), 0f);

    if (currentSpeed < 0)
    {
        // nach unten
        if (a.position.y <= -h)
            a.position = new Vector3(a.position.x, b.position.y + h - overlap, 0f);
        if (b.position.y <= -h)
            b.position = new Vector3(b.position.x, a.position.y + h - overlap, 0f);
    }
    else
    {
        // nach oben
        if (a.position.y >= h)
            a.position = new Vector3(a.position.x, b.position.y - h + overlap, 0f);
        if (b.position.y >= h)
            b.position = new Vector3(b.position.x, a.position.y - h + overlap, 0f);
    }
}

public void SetGravityMode(bool active)
{
    targetSpeed = active ? scrollSpeed : -scrollSpeed;
}

    float RoundToPixel(float v)
    {
        return Mathf.Round(v / pix) * pix;
    }
}
