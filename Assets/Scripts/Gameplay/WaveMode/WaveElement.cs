using System.Collections;
using UnityEngine;

// Ein Element der Wellen/Korb-Mechanik (Fruit-Ninja-Stil). Ein Typ deckt alle
// 5 Spielarten ab (Normal/Special/Multiplier/Shocker/Bombe) — sie unterscheiden
// sich nur im Punktwert und der Swipe-Wirkung, nicht im Bewegungsverhalten.
public class WaveElement : BasePoint
{
    [Header("Typ")]
    [SerializeField] private WaveElementType type;
    public WaveElementType Type => type;

    [Header("Fly-to-Basket (nur Normal/Special/Multiplier)")]
    [SerializeField] private float flyToBasketDuration = 0.35f;

    [Header("Bewegung (eine durchgehende Bewegung, kein Phasenwechsel)")]
    [Tooltip("Wie träge das Element seinem Ziel folgt (Vector3.SmoothDamp). Größer = weicheres, langsameres Einholen.")]
    [SerializeField] private float approachSmoothTime = 0.35f;
    [Tooltip("Amplitude ENTLANG der Einflugrichtung — dieselbe Achse, in die das Element sowieso unterwegs ist.")]
    [SerializeField] private float bobAmplitudeAlong = 0.15f;
    [Tooltip("Amplitude QUER zur Einflugrichtung, für etwas organische Seitwärts-Variation.")]
    [SerializeField] private float bobAmplitudeAcross = 0.08f;
    [SerializeField] private float bobPeriod = 2f;
    [Range(0f, 0.6f)]
    [Tooltip("Zufällige Streuung von Amplitude/Periode pro Element — sonst schweben alle synchron wie eine Formation.")]
    [SerializeField] private float bobVariance = 0.35f;

    [Header("Sanfte Abstoßung (löst Restüberlappung auf)")]
    [Tooltip("Ab welchem Abstand zu einem Nachbar-Element die Abstoßung einsetzt.")]
    [SerializeField] private float repulsionRadius = 1.6f;
    [Tooltip("Wie stark weggedrückt wird, wenn Elemente sich berühren/überlappen (0 Abstand).")]
    [SerializeField] private float repulsionStrength = 3f;

    // Richtung des Einflugs (normiert) + deren Senkrechte.
    private Vector3 _bobDir;
    private Vector3 _bobPerp;
    private float _bobAmpAlong, _bobAmpAcross;
    private float _bobFreqAlong, _bobFreqAcross;
    private float _bobPhaseAcross;
    private Vector3 _smoothVelocity; // für SmoothDamp

    [Header("Shocker")]
    [SerializeField] private float shockerShakeDuration = 2f;
    [SerializeField] private float shockerShakeStrength = 0.25f;

    private Vector3 _hoverPos;
    private float _flyInDuration;
    private float _hoverDuration;
    private bool _resolved;
    private Collider2D _col;

    /// <summary>Ziel-Schwebeposition (nicht die aktuelle Live-Position!). Der WaveBasketController
    /// nutzt das für die Überlappungs-Prüfung neuer Elemente — auch während dieses Element hier
    /// noch mitten im Einflug ist und transform.position deshalb noch woanders steht.</summary>
    public Vector3 HoverPosition => _hoverPos;

    /// <summary>True sobald das Element per Swipe getroffen wurde ODER gerade auslöst/verpufft.
    /// Der WaveBasketController ignoriert solche Elemente bei der Überlappungs-Prüfung neuer
    /// Schwebepositionen — sie verschwinden ohnehin sofort und stören die nächste Welle nicht.</summary>
    public bool IsResolved => _resolved;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
    }

    // Vom WaveBasketController direkt nach Instantiate aufgerufen.
    public void Init(Vector3 startWorldPos, Vector3 hoverWorldPos, float flyInDuration, float hoverDuration)
    {
        _hoverPos = hoverWorldPos;
        _flyInDuration = flyInDuration;
        _hoverDuration = hoverDuration;
        transform.position = startWorldPos;
        _smoothVelocity = Vector3.zero;

        // Bob-Achse = Einflugrichtung (+ deren Senkrechte).
        Vector3 delta = hoverWorldPos - startWorldPos;
        _bobDir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.right;
        _bobPerp = new Vector3(-_bobDir.y, _bobDir.x, 0f);

        // Pro Element zufällig streuen (Amplitude/Periode), sonst schweben
        // alle Elemente einer Welle synchron wie eine Formation.
        _bobAmpAlong = bobAmplitudeAlong * Random.Range(1f - bobVariance, 1f + bobVariance);
        _bobAmpAcross = bobAmplitudeAcross * Random.Range(1f - bobVariance, 1f + bobVariance);
        float periodAlong = bobPeriod * Random.Range(1f - bobVariance, 1f + bobVariance);
        float periodAcross = periodAlong * Random.Range(1.4f, 1.9f); // andere Frequenz → kein simples Hin-und-Her im Gleichtakt
        _bobFreqAlong = Mathf.PI * 2f / periodAlong;
        _bobFreqAcross = Mathf.PI * 2f / periodAcross;
        _bobPhaseAcross = Random.Range(0f, Mathf.PI * 2f); // Across hat keine "natürliche" Richtung -> Phase darf frei streuen

        StartCoroutine(Co_Lifecycle());
    }

    // EINE durchgehende Bewegung über die komplette Lebenszeit: das Element "jagt" die ganze
    // Zeit einem Zielpunkt hinterher (Vector3.SmoothDamp, kontinuierliche Geschwindigkeit,
    // kein Reset). Das Ziel ist die Schwebeposition + ein Wobble, dessen Amplitude über die
    // Einflugzeit sanft hochfährt. Anfangs (weit entfernt) ist das Wobble neben der großen
    // Distanz kaum sichtbar; ist das Element einmal nah dran, sieht man nur noch das Wobble.
    // Dadurch gibt es KEINEN Phasenwechsel/Stop zwischen Einfliegen und Schweben — es ist
    // technisch die ganze Zeit dieselbe Bewegung.
    private IEnumerator Co_Lifecycle()
    {
        float elapsed = 0f;
        float totalLife = _flyInDuration + _hoverDuration;

        while (elapsed < totalLife)
        {
            if (_resolved) yield break;
            elapsed += Time.deltaTime;

            float amp = SmootherStep01(elapsed / _flyInDuration);
            float along = Mathf.Cos(elapsed * _bobFreqAlong) * _bobAmpAlong * amp;
            float across = Mathf.Sin(elapsed * _bobFreqAcross + _bobPhaseAcross) * _bobAmpAcross * amp;
            Vector3 target = _hoverPos + _bobDir * along + _bobPerp * across + ComputeRepulsion();

            transform.position = Vector3.SmoothDamp(transform.position, target, ref _smoothVelocity, approachSmoothTime);

            yield return null;
        }

        // Timeout: für ALLE Typen komplett folgenlos verpuffen.
        if (_resolved) yield break;
        _resolved = true;
        if (_col != null) _col.enabled = false;
        SpawnExplosion();
        Destroy(gameObject);
    }

    // Vom PlayerInputHandler bei Swipe-Treffer aufgerufen (Multi-Slice-fähig).
    public void OnSliced()
    {
        if (_resolved) return;
        _resolved = true;
        if (_col != null) _col.enabled = false;
        StopAllCoroutines();

        switch (type)
        {
            case WaveElementType.Shocker:
                SpawnExplosion();
                ScreenShakeManager.Instance?.Shake(shockerShakeDuration, shockerShakeStrength);
                Destroy(gameObject);
                break;

            case WaveElementType.Bomb:
                SpawnExplosion();
                WaveBasketController.Instance?.OnBombSliced();
                Destroy(gameObject);
                break;

            default: // Normal, Special, Multiplier
                WaveBasketController.Instance?.OnElementCollected(type);
                StartCoroutine(Co_FlyToBasket());
                break;
        }
    }

    private IEnumerator Co_FlyToBasket()
    {
        Vector3 start = transform.position;
        Vector3 target = WaveBasketController.Instance != null
            ? WaveBasketController.Instance.BasketWorldPosition
            : start;
        Vector3 startScale = transform.localScale;

        float t = 0f;
        while (t < flyToBasketDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / flyToBasketDuration);
            transform.position = Vector3.Lerp(start, target, k);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, k);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Sanfte Abstoßung: schaut bei allen anderen noch nicht aufgelösten Elementen der
    // WaveBasketController-Liste nach, wie nah sie an der aktuellen (Live-)Position sind, und
    // drückt proportional dazu weg. Das fließt nur als zusätzlicher Versatz ins SmoothDamp-Ziel
    // ein — keine eigene Bewegungsphase, löst aber Restüberlappung (z.B. vom Rejection-Sampling-
    // Fallback bei zu vielen Elementen) über die Zeit sanft auf, statt hart zu clippen.
    private Vector3 ComputeRepulsion()
    {
        var controller = WaveBasketController.Instance;
        if (controller == null) return Vector3.zero;

        Vector3 push = Vector3.zero;
        foreach (var other in controller.ActiveElements)
        {
            if (other == null || other == this || other.IsResolved) continue;

            Vector3 diff = transform.position - other.transform.position;
            float dist = diff.magnitude;
            if (dist >= repulsionRadius) continue;

            Vector3 dir = dist > 0.001f ? diff / dist : (Vector3)Random.insideUnitCircle.normalized;
            float t = 1f - (dist / repulsionRadius); // 0 am Radius-Rand, 1 bei voller Überlappung
            push += dir * (t * repulsionStrength);
        }
        return push;
    }

    // Wie Mathf.SmoothStep, aber auch die zweite Ableitung ist an den Rändern 0 (Ken Perlins
    // "smootherstep") — sorgt dafür, dass die Wobble-Amplitude ganz sanft mit hochfährt.
    private static float SmootherStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
