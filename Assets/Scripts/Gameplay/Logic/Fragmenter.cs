using UnityEngine;

// Optionale Komponente pro Element-Prefab: liefert die "Fruit Ninja"-Slice-Optik für den Boost
// "Swipe How You Like" (siehe BasePoint.SpawnExplosion). Nur wo halfPrefabA/B im Inspector zugewiesen
// sind — ohne Zuweisung liefert TrySlice() false, der Aufrufer fällt dann auf die normale
// Partikel-Explosion zurück (kein Zwang, Elemente ohne Slice-Grafik funktionieren weiterhin normal).
//
// Manuelle Bewegung statt Rigidbody2D (Performance/Mobile, siehe altes Fragment-Konzept): jedes Stück
// bekommt einen FragmentPieceMover, der Position/Rotation/Fade selbst über die Zeit animiert und sich
// danach selbst zerstört.
public class Fragmenter : MonoBehaviour
{
    [Header("Hälften (vorgefertigte Sprite-Prefabs)")]
    [Tooltip("Zwei Hälften des Elements, neutral ausgerichtet (Schnittlinie waagrecht gedacht) — werden " +
             "beim Slicen zur Wischrichtung gedreht und auseinandergeschleudert.")]
    [SerializeField] private GameObject halfPrefabA;
    [SerializeField] private GameObject halfPrefabB;

    [Header("Bewegung")]
    [SerializeField] private float flingSpeed = 4f;
    [SerializeField] private float gravity    = 6f;
    [SerializeField] private float spinSpeed  = 180f;
    [SerializeField] private float fadeDuration = 0.4f;

    /// <summary>Liefert false, wenn keine Hälften zugewiesen sind (Aufrufer soll dann auf die normale
    /// Explosion zurückfallen). swipeDirection = Richtung, in die die Wisch-/Schnittbewegung lief.</summary>
    public bool TrySlice(Vector3 position, Vector2 swipeDirection)
    {
        if (halfPrefabA == null || halfPrefabB == null) return false;

        if (swipeDirection.sqrMagnitude < 0.0001f) swipeDirection = Vector2.right;
        swipeDirection.Normalize();

        float angle = Mathf.Atan2(swipeDirection.y, swipeDirection.x) * Mathf.Rad2Deg;
        // Schnittlinie verläuft entlang der Wischrichtung (wie ein durchgezogenes Messer) — die
        // beiden Hälften fliegen senkrecht dazu in entgegengesetzte Richtungen auseinander.
        Vector2 normal = new Vector2(-swipeDirection.y, swipeDirection.x);

        SpawnHalf(halfPrefabA, position, angle, normal);
        SpawnHalf(halfPrefabB, position, angle, -normal);
        return true;
    }

    private void SpawnHalf(GameObject prefab, Vector3 position, float angleDeg, Vector2 flingDir)
    {
        var half = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angleDeg));
        var mover = half.AddComponent<FragmentPieceMover>();
        mover.Init(flingDir * flingSpeed, gravity, spinSpeed * Mathf.Sign(flingDir.x + 0.0001f), fadeDuration);
    }
}

// Bewegt ein einzelnes Slice-Stück: konstante Anfangsgeschwindigkeit + Gravitation, konstanter Spin,
// Alpha-Fade gegen Ende, danach Selbstzerstörung. Wird dynamisch per AddComponent an frisch
// instanziierte Hälften-Prefabs gehängt (siehe Fragmenter.SpawnHalf).
public class FragmentPieceMover : MonoBehaviour
{
    private Vector2 velocity;
    private float   gravity;
    private float   spinSpeed;
    private float   fadeDuration;

    private float t;
    private SpriteRenderer[] renderers;
    private float[] baseAlpha;

    public void Init(Vector2 initialVelocity, float gravityStrength, float rotationSpeed, float fade)
    {
        velocity     = initialVelocity;
        gravity      = gravityStrength;
        spinSpeed    = rotationSpeed;
        fadeDuration = Mathf.Max(0.01f, fade);

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseAlpha[i] = renderers[i].color.a;
    }

    private void Update()
    {
        t += Time.deltaTime;

        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime);

        if (t >= fadeDuration)
        {
            Destroy(gameObject);
            return;
        }

        float alphaMul = 1f - Mathf.Clamp01(t / fadeDuration);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = renderers[i].color;
            c.a = baseAlpha[i] * alphaMul;
            renderers[i].color = c;
        }
    }
}
