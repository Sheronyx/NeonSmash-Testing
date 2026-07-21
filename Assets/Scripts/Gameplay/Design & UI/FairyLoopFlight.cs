using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Tap-Gimmick fürs Hauptmenü: Antippen lässt die Fee zufällig entweder einen Loop oder ein
// liegendes Unendlichkeitszeichen um ihre aktuelle Position fliegen, mit beschleunigtem
// Flügelschlag währenddessen. Danach geht's normal mit dem Schweben (FairyFloat) weiter.
//
// Tap-Erkennung läuft über das neue Input System (wie MenuPortalSwitcher/FairyTapGimmick) statt
// OnMouseDown — OnMouseDown hängt am alten Input Manager und feuert gar nicht, wenn "Active Input
// Handling" in den Project Settings auf "Input System Package (New)" exklusiv steht.
[RequireComponent(typeof(FairyFloat), typeof(FairyWingFlap), typeof(Collider2D))]
public class FairyLoopFlight : MonoBehaviour
{
    [Header("Figur")]
    [SerializeField] private float figureDuration = 1.6f;
    [SerializeField] private float figureRadius   = 0.8f;
    [Tooltip("Wie viel schneller der Flügelschlag während des Kunststücks ist.")]
    [SerializeField] private float flapSpeedBoost = 1.8f;
    [Tooltip("Wie weit (Winkel-Radiant) die Figur VOR dem eigentlichen Start zurückschwingt — " +
             "wie ein kurzer Anlauf/Ausholen, bevor's richtig losgeht. 0 = kein Ausholen.")]
    [SerializeField] private float windUpAngle = 0.5f;
    [Tooltip("Dauer der Ausholphase (schwingt von der aktuellen Position sanft zu -Wind Up Angle " +
             "zurück, bevor die eigentliche Figur beginnt).")]
    [SerializeField] private float windUpDuration = 0.3f;

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
            _routine = StartCoroutine(Co_PlayFigure());
    }

    // Loop: Kreis, der bei (0,0) startet/endet und zuerst nach oben wegdreht.
    // Unendlichkeitszeichen: liegende Lemniskate, ebenfalls start-/endpunktgleich bei (0,0).
    private static Vector3 FigureOffset(bool doLoop, float angle, float radius) => doLoop
        ? new Vector3(Mathf.Sin(angle), 1f - Mathf.Cos(angle), 0f) * radius
        : new Vector3(Mathf.Sin(angle), Mathf.Sin(angle) * Mathf.Cos(angle), 0f) * radius;

    private IEnumerator Co_PlayFigure()
    {
        _fairyFloat.enabled = false;
        _wingFlap.SetSpeedBoost(flapSpeedBoost);

        Vector3 center    = transform.position;
        bool    doLoop    = Random.value < 0.5f;
        float   direction = Random.value < 0.5f ? 1f : -1f;

        // Ausholphase: schwingt bei Winkel 0 (= exakt die aktuelle Position, kein Sprung) los und
        // eased zu -windUpAngle. SmoothStep hat an BEIDEN Enden Geschwindigkeit 0 — sie knüpft also
        // nahtlos an die jetzige Ruheposition an und kommt am Rückschwung-Punkt kurz zum Stillstand,
        // bevor die Hauptfigur unten nahtlos (ebenfalls mit Geschwindigkeit 0 beginnend) übernimmt.
        if (windUpDuration > 0f && windUpAngle > 0f)
        {
            float wt = 0f;
            while (wt < windUpDuration)
            {
                wt += Time.deltaTime;
                float p     = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wt / windUpDuration));
                float angle = Mathf.Lerp(0f, -windUpAngle, p) * direction;
                transform.position = center + FigureOffset(doLoop, angle, figureRadius);
                yield return null;
            }
        }

        float t = 0f;
        while (t < figureDuration)
        {
            t += Time.deltaTime;
            // Winkel läuft nicht linear von -windUpAngle bis 2π, sondern eased (SmoothStep), sodass
            // der Übergang aus der Ausholphase (Geschwindigkeit 0) nahtlos weitergeht und am Ende
            // (= wieder am Ausgangspunkt) ebenso sanft zum Stillstand kommt — wie ein Gummiball, der
            // nach dem Einfedern beschleunigt und vor dem Ausklingen wieder abbremst.
            float easedP = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / figureDuration));
            float angle  = Mathf.Lerp(-windUpAngle, Mathf.PI * 2f, easedP) * direction;
            transform.position = center + FigureOffset(doLoop, angle, figureRadius);
            yield return null;
        }
        transform.position = center;

        _wingFlap.SetSpeedBoost(1f);
        _fairyFloat.enabled = true;
        _routine = null;
    }
}
