using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Tap-Gimmick fürs Hauptmenü: Antippen lässt die Fee direkt kleiner werden und die Flügel
// einklappen (ein durchgehender, smooth geeasteter Verlauf, siehe Co_Burst), dann für eine Weile
// verstärkt, breit gestreut und leicht vibrierend Seifenblasen ausstoßen, bevor alles wieder
// smooth zum Normalzustand zurückkehrt.
// Seifenblasen-Partikelsysteme werden automatisch über alle Kind-Objekte mit "bubble" im Namen
// gefunden — funktioniert auch mit verschachtelten Prefab-Instanzen wie CFXR4 Bubbles Breath
// Underwater Loop, ohne sie manuell verdrahten zu müssen.
//
// Tap-Erkennung läuft über das neue Input System (wie MenuPortalSwitcher/FairyTapGimmick) statt
// OnMouseDown — OnMouseDown hängt am alten Input Manager und feuert gar nicht, wenn "Active Input
// Handling" in den Project Settings auf "Input System Package (New)" exklusiv steht.
[RequireComponent(typeof(FairyFloat), typeof(FairyWingFlap), typeof(Collider2D))]
public class FairyBubbleBurst : MonoBehaviour
{
    [Header("Klein machen (Flügel einklappen + Körper schrumpft)")]
    [SerializeField] private float shrinkDuration        = 0.4f;
    [SerializeField] private float shrinkScaleMultiplier = 0.8f;

    [Header("Zurück zum Normalzustand (Flügel öffnen + Körper wächst zurück)")]
    [SerializeField] private float returnDuration = 0.4f;

    [Header("Seifenblasen-Ausstoß")]
    [SerializeField] private float burstDuration        = 2f;
    [SerializeField] private float boostedRateOverTime  = 100f;
    [Tooltip("Max Particles der Blasen-Systeme während des Ausstoßes — das Original-Limit ist meist " +
             "auf die normale Ambient-Rate zugeschnitten und verwirft sonst einen Großteil der " +
             "zusätzlichen Emission stillschweigend (Partikel werden nicht erzeugt, wenn das System " +
             "sein Max Particles bereits erreicht hat).")]
    [SerializeField] private int   boostedMaxParticles = 300;
    [Tooltip("Zufällige seitliche Kraft (Force over Lifetime, X-Achse), die während des Ausstoßes " +
             "zusätzlich zur normalen (konstanten, senkrechten) Aufsteig-Kraft wirkt — jede Blase " +
             "bekommt beim Spawnen einen zufälligen Wert zwischen -Wert und +Wert, dadurch drifteten " +
             "einige nach links, andere nach rechts statt alle exakt geradeaus nach oben zu schießen.")]
    [SerializeField] private float sidewaysForce = 0.6f;

    [Header("Vibrieren")]
    [SerializeField] private float vibrationAmount    = 0.04f;
    [Tooltip("Wie schnell das Vibrieren oszilliert (höher = schnelleres Zittern, niedriger = " +
             "langsameres, weicheres Wackeln).")]
    [SerializeField] private float vibrationFrequency = 6f;

    private FairyFloat       _fairyFloat;
    private FairyWingFlap    _wingFlap;
    private ParticleSystem[] _bubbleSystems;
    private float[]                      _originalRates;
    private int[]                        _originalMaxParticles;
    private ParticleSystem.MinMaxCurve[] _originalForceX;
    private Coroutine        _routine;

    private void Awake()
    {
        _fairyFloat = GetComponent<FairyFloat>();
        _wingFlap   = GetComponent<FairyWingFlap>();

        var found = new List<ParticleSystem>();
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            if (ps.name.IndexOf("bubble", StringComparison.OrdinalIgnoreCase) >= 0)
                found.Add(ps);
        _bubbleSystems = found.ToArray();

        _originalRates        = new float[_bubbleSystems.Length];
        _originalMaxParticles = new int[_bubbleSystems.Length];
        _originalForceX       = new ParticleSystem.MinMaxCurve[_bubbleSystems.Length];
        for (int i = 0; i < _bubbleSystems.Length; i++)
        {
            _originalRates[i]        = _bubbleSystems[i].emission.rateOverTime.constant;
            _originalMaxParticles[i] = _bubbleSystems[i].main.maxParticles;
            _originalForceX[i]       = _bubbleSystems[i].forceOverLifetime.x;
        }
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
            _routine = StartCoroutine(Co_Burst());
    }

    private IEnumerator Co_Burst()
    {
        // FairyFloat pausieren, solange das Gimmick läuft — sonst bobbt/wandert die Fee über ihren
        // eigenen unabhängigen Takt weiter, während wir Pose/Scale/Position selbst steuern, was wie
        // ein Ruckler zwischen beiden Bewegungen aussieht (siehe FairyLoopFlight, gleiches Prinzip).
        _fairyFloat.enabled = false;

        Vector3 restPosition = transform.position;
        Vector3 baseScale    = transform.localScale;
        Vector3 smallScale   = baseScale * shrinkScaleMultiplier;

        // Direkt kleiner werden und Flügel einklappen — kein Aufplustern mehr davor. In
        // FairyWingFlap ist Pose 0 = Ruheposition = Flügel OFFEN/gespreizt, Pose 1 = Extrem =
        // Flügel nach hinten GEFALTET (siehe foldScaleAtExtreme dort), also Richtung 1.
        float t = 0f;
        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(shrinkDuration, 0.0001f)));
            _wingFlap.SetPose(p); // offen (0) -> gefaltet (1)
            transform.localScale = Vector3.Lerp(baseScale, smallScale, p);
            yield return null;
        }

        // Flügel bleiben BEWUSST in der Pose gehalten (nicht ClearPose!) — sonst würde die normale
        // automatische Flatter-Animation sofort wieder einsetzen und die Flügel erneut öffnen. Sie
        // sollen während der ganzen Vibrations-/Blasenphase sichtbar zusammengeklappt bleiben.
        _wingFlap.SetPose(1f);
        transform.localScale = smallScale;

        // Verstärkter Seifenblasen-Ausstoß für eine Weile, breiter gestreut statt nur senkrecht nach
        // oben. Max Particles mit hochsetzen, sonst verwirft das Partikelsystem einen Großteil der
        // zusätzlichen Emission stillschweigend, weil das normale Limit auf die ambiente Rate
        // zugeschnitten ist. Die tatsächliche Flugrichtung wird NICHT vom Emissions-Kegel bestimmt
        // (Start Speed ist 0), sondern von "Force over Lifetime" — eine konstante Weltraum-Kraft
        // senkrecht nach oben, X/Z bisher 0. Deshalb hier zufällige X-Kraft pro Blase hinzufügen
        // (TwoConstants-Modus: jede Blase bekommt beim Spawn einen zufälligen Wert zwischen den
        // beiden Grenzen), damit ein Teil nach links, ein Teil nach rechts abdriftet.
        for (int i = 0; i < _bubbleSystems.Length; i++)
        {
            var main = _bubbleSystems[i].main;
            main.maxParticles = Mathf.Max(boostedMaxParticles, _originalMaxParticles[i]);

            var force = _bubbleSystems[i].forceOverLifetime;
            force.x = new ParticleSystem.MinMaxCurve(-sidewaysForce, sidewaysForce);

            var emission = _bubbleSystems[i].emission;
            emission.rateOverTime = boostedRateOverTime;
        }

        // Perlin-Noise-basiertes Zittern statt jeden Frame ein neuer Zufallspunkt — reiner Random-
        // Jitter pro Frame flackert bei 60fps viel zu schnell. Perlin liefert eine stetige, weichere
        // Wellenbewegung, deren Tempo über vibrationFrequency separat von der Stärke steuerbar ist.
        float shakeSeedX = UnityEngine.Random.Range(0f, 1000f);
        float shakeSeedY = UnityEngine.Random.Range(0f, 1000f);
        float shakeTimer = 0f;
        while (shakeTimer < burstDuration)
        {
            shakeTimer += Time.deltaTime;

            float nx = Mathf.PerlinNoise(shakeSeedX, shakeTimer * vibrationFrequency) * 2f - 1f;
            float ny = Mathf.PerlinNoise(shakeSeedY, shakeTimer * vibrationFrequency) * 2f - 1f;
            transform.position = restPosition + new Vector3(nx, ny, 0f) * vibrationAmount;
            yield return null;
        }
        transform.position = restPosition;

        for (int i = 0; i < _bubbleSystems.Length; i++)
        {
            var emission = _bubbleSystems[i].emission;
            emission.rateOverTime = _originalRates[i];

            var main = _bubbleSystems[i].main;
            main.maxParticles = _originalMaxParticles[i];

            var force = _bubbleSystems[i].forceOverLifetime;
            force.x = _originalForceX[i];
        }

        // Zurück zum Normalzustand: Flügel wieder öffnen, Körper zurück auf Normalgröße.
        t = 0f;
        while (t < returnDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(returnDuration, 0.0001f)));
            _wingFlap.SetPose(1f - p); // gefaltet (1) -> offen (0)
            transform.localScale = Vector3.Lerp(smallScale, baseScale, p);
            yield return null;
        }
        _wingFlap.SetPose(0f);
        transform.localScale = baseScale;

        _wingFlap.ClearPose();
        _fairyFloat.enabled = true;
        _routine = null;
    }
}
