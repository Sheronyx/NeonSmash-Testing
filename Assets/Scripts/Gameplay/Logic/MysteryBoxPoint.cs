using System.Collections;
using UnityEngine;

// Zufallsbox ("?"-Box wie in Mario Kart): Bonus-Collectible im Normal Mode, zusätzlich zu den 3
// Farbelementen (siehe MixedPointSpawner.SpawnMysteryBox). Sammeln = löst EINEN zufälligen Effekt aus
// (siehe MysteryBoxEffectSystem), positiv oder negativ — man weiß vorher nicht, welchen. Verpassen =
// folgenlos (kein Schaden), genau wie der Diamant.
public class MysteryBoxPoint : MonoBehaviour
{
    [Header("Pop-In")]
    [SerializeField] private float popInDuration  = 0.25f;
    [SerializeField] private float popInOvershoot = 1.25f;

    [Header("Zerstören (Partikel-Effekt)")]
    [Tooltip("Prefab: leeres Parent-Objekt mit mehreren Partikelsystemen als Kindern — spielt beim " +
             "erfolgreichen Antippen (volle Größe) UND beim Verpuffen (kleiner, siehe unten) ab.")]
    [SerializeField] private GameObject destroyEffectPrefab;

    [Header("Verpuffen (Timeout / weggeräumt)")]
    [Range(0f, 1f)]
    [SerializeField] private float puffEffectScale = 0.33f;
    [SerializeField] private float puffShrinkDuration = 0.2f;

    [HideInInspector] public MixedPointSpawner spawner;

    private bool consumed = false;
    private Vector3 targetScale;

    // Vom Spawner aufrufen: startet die Countdown-Visuals und plant das folgenlose Auslaufen.
    // skipPopIn=true: PointFlyIn hat die Box bereits von außerhalb eingeflogen/gepoppt — eigenes
    // Pop-In würde die Skalierung nochmal auf 0 zurücksetzen (doppelter Pop).
    public void Activate(float reactionTime, bool skipPopIn = false)
    {
        targetScale = transform.localScale;
        if (skipPopIn)
        {
            GetComponent<PointPulse>()?.StartPulsing();
        }
        else
        {
            transform.localScale = Vector3.zero;
            StartCoroutine(Co_PopInThenPulse());
        }

        foreach (var cs in GetComponentsInChildren<CountdownSquare>())
            cs.StartCountdown(reactionTime);

        foreach (var fuse in GetComponentsInChildren<FuseCountdown>())
            fuse.StartBurn(reactionTime);

        foreach (var line in GetComponentsInChildren<LineFuse>())
            line.StartBurn(reactionTime);

        foreach (var sparks in GetComponentsInChildren<BurnSparks>())
        {
            sparks.SetQuadMode(true);
            sparks.StartBurn(reactionTime);
        }

        CancelInvoke(nameof(Dismiss));
        if (reactionTime > 0f)
            Invoke(nameof(Dismiss), reactionTime);
    }

    // Spieler hat die Box angetippt → zufälliger Effekt, keine Punkte direkt.
    public void TryTap()
    {
        if (consumed) return;
        if (TutorialManager.IsOrbPhaseActive) return;

        consumed = true;
        CancelInvoke(nameof(Dismiss));

        AudioManager.Instance?.PlayNormalPoint();
        SpawnDestroyEffect();

        MysteryBoxEffectSystem.Instance?.PlayCollectSequence(spawner);
        spawner?.HandleMysteryBoxCollected();
        Destroy(gameObject);
    }

    // Zeit abgelaufen ODER vom Spawner geräumt (z.B. Phasenwechsel) → folgenlos verpuffen.
    public void Dismiss()
    {
        if (consumed) return;
        consumed = true;

        var pulse = GetComponent<PointPulse>();
        if (pulse) pulse.StopPulsing(); // sonst überschreibt der Puls-Update() den Verpuff-Scale

        spawner?.HandleMysteryBoxResolved();
        StartCoroutine(Co_PuffAndDestroy());
    }

    private void SpawnDestroyEffect(float scale = 1f)
    {
        if (destroyEffectPrefab == null) return;
        var fx = Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;

        float dur = 0f;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Play();
            dur = Mathf.Max(dur, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        Destroy(fx, dur);
    }

    private IEnumerator Co_PuffAndDestroy()
    {
        Vector3 startScale = transform.localScale;

        float t = 0f;
        while (t < puffShrinkDuration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.Clamp01(t / puffShrinkDuration));
            yield return null;
        }
        transform.localScale = Vector3.zero;

        SpawnDestroyEffect(puffEffectScale);

        Destroy(gameObject);
    }

    private IEnumerator Co_PopIn()
    {
        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popInDuration);
            float s = k < 0.7f
                ? Mathf.Lerp(0f, popInOvershoot, k / 0.7f)
                : Mathf.Lerp(popInOvershoot, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = targetScale * s;
            yield return null;
        }
        transform.localScale = targetScale;
    }

    // Pop-In und PointPulse steuern beide transform.localScale — erst Pop-In fertig abspielen,
    // DANACH pulsieren starten, sonst überschreiben sich beide gegenseitig.
    private IEnumerator Co_PopInThenPulse()
    {
        yield return Co_PopIn();

        var pulse = GetComponent<PointPulse>();
        if (pulse) pulse.StartPulsing();
    }
}
