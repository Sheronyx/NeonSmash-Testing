using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Tap-Gimmick fürs Hauptmenü: Antippen drückt die Fee sanft nach unten, direkt danach fliegt sie
// mit beschleunigtem Flügelschlag zurück zu ihrer aktuellen Schwebe-Position. Beide Teilstrecken
// bekommen einen zufälligen seitlichen Versatz und werden als Kurve statt gerader Linie geflogen,
// damit es nicht mechanisch/schnurgerade wirkt.
// FairyFloat wird währenddessen deaktiviert (nicht nur die Position überschrieben), damit ihr
// interner Wander-Timer währenddessen pausiert und beim Wiedereinschalten nahtlos genau dort
// weitermacht, wo sie gerade tatsächlich steht — kein Sprung beim Übergang zurück zum Schweben.
//
// Tap-Erkennung läuft über das neue Input System (wie MenuPortalSwitcher) statt OnMouseDown —
// OnMouseDown hängt am alten Input Manager und feuert gar nicht, wenn "Active Input Handling"
// in den Project Settings auf "Input System Package (New)" exklusiv steht.
[RequireComponent(typeof(FairyFloat), typeof(FairyWingFlap), typeof(Collider2D))]
public class FairyTapGimmick : MonoBehaviour
{
    [Header("Runterdrücken")]
    [SerializeField] private float pushDownDistance = 1f;
    [SerializeField] private float pushDownDuration  = 0.35f;

    [Header("Zurückfliegen")]
    [SerializeField] private float flyBackDuration = 0.6f;
    [Tooltip("Wie viel schneller der Flügelschlag beim Zurückfliegen ist (2 = doppelt so schnell wie normal).")]
    [SerializeField] private float flapSpeedBoost  = 2.5f;

    [Header("Seitliche Kurven (nicht alles schnurgerade)")]
    [Tooltip("Zufälliger seitlicher Versatz, der auf den unteren Zielpunkt draufaddiert wird.")]
    [SerializeField] private float sidewaysJitter = 0.35f;
    [Tooltip("Wie stark jede Teilstrecke seitlich ausbeult (Kurve statt gerader Linie).")]
    [SerializeField] private float curveStrength  = 0.4f;

    private FairyFloat    _fairyFloat;
    private FairyWingFlap _wingFlap;
    private Coroutine     _routine;

    private void Awake()
    {
        _fairyFloat = GetComponent<FairyFloat>();
        _wingFlap   = GetComponent<FairyWingFlap>();
    }

    private void Update()
    {
        if (_routine != null) return;
        // Während ein Overlay offen ist (Shop etc.), keine Taps auf die Fee im Hintergrund annehmen.
        if (DimOverlay.Instance != null && DimOverlay.Instance.IsShowing) return;

        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(pointer.position.ReadValue());
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray);
        if (hit.collider != null && hit.collider.gameObject == gameObject)
            _routine = StartCoroutine(Co_PushAndReturn());
    }

    private IEnumerator Co_PushAndReturn()
    {
        _fairyFloat.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 downPos  = startPos + Vector3.down * pushDownDistance + RandomSideways();

        yield return Co_MoveCurved(startPos, downPos, pushDownDuration);

        // Zurückfliegen: deutlich schnellerer Flügelschlag, bis sie wieder oben ankommt.
        _wingFlap.SetSpeedBoost(flapSpeedBoost);
        yield return Co_MoveCurved(downPos, startPos, flyBackDuration);
        _wingFlap.SetSpeedBoost(1f);

        _fairyFloat.enabled = true;
        _routine = null;
    }

    private Vector3 RandomSideways() => Vector3.right * Random.Range(-sidewaysJitter, sidewaysJitter);

    // Fliegt von "from" nach "to" nicht schnurgerade, sondern über einen zufällig seitlich
    // versetzten Kontrollpunkt (quadratische Bezier-Kurve) — landet trotzdem exakt auf "to".
    private IEnumerator Co_MoveCurved(Vector3 from, Vector3 to, float duration)
    {
        Vector3 direction = to - from;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f).normalized;
        Vector3 control = (from + to) * 0.5f + perpendicular * Random.Range(-curveStrength, curveStrength);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            Vector3 a = Vector3.Lerp(from, control, p);
            Vector3 b = Vector3.Lerp(control, to, p);
            transform.position = Vector3.Lerp(a, b, p);
            yield return null;
        }
        transform.position = to;
    }
}
