using System.Collections;
using UnityEngine;

// Diamant: Bonus-Collectible ab Phase 9 (zusätzlich zu den 3 Farbelementen im Normal Mode).
// Sammeln = Punkte + zählt für den Special-Mode-Bonus. Verpassen = folgenlos (kein Schaden).
public class DiamondPoint : MonoBehaviour
{
    [Header("Score")]
    [SerializeField] private int scoreValue = 20;

    [Header("Pop-In")]
    [SerializeField] private float popInDuration  = 0.25f;
    [SerializeField] private float popInOvershoot = 1.25f;

    [Header("Zerstören (Partikel-Effekt)")]
    [Tooltip("Partikel-Effekt beim erfolgreichen Antippen (volle Größe) UND beim Verpuffen (kleiner, siehe unten).")]
    [SerializeField] private ParticleSystem destroyEffect;

    [Header("Verpuffen (Timeout / weggeräumt)")]
    [Tooltip("Diamant schrumpft erst auf 0, dann kommt der Zerstören-Effekt in dieser Größe (Anteil der vollen Größe).")]
    [Range(0f, 1f)]
    [SerializeField] private float puffEffectScale = 0.33f;
    [SerializeField] private float puffShrinkDuration = 0.2f;

    [HideInInspector] public MixedPointSpawner spawner;

    private bool consumed = false;
    private Vector3 targetScale;

    // Vom Spawner aufrufen: startet die Countdown-Visuals und plant das folgenlose Auslaufen.
    public void Activate(float reactionTime)
    {
        targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        StartCoroutine(Co_PopInThenPulse());

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

    // Spieler hat den Diamanten angetippt → Punkte, zählt für den Special-Mode-Bonus.
    public void TryTap()
    {
        if (consumed) return;
        if (TutorialManager.IsOrbPhaseActive) return;

        consumed = true;
        CancelInvoke(nameof(Dismiss));

        ScoreManager.Instance?.AddPoints(scoreValue);
        AudioManager.Instance?.PlayNormalPoint();
        SpawnDestroyEffect();

        spawner?.HandleDiamondCollected();
        Destroy(gameObject);
    }

    // Zeit abgelaufen ODER vom Spawner geräumt (z.B. Phasenwechsel) → folgenlos verpuffen.
    public void Dismiss()
    {
        if (consumed) return;
        consumed = true;

        var pulse = GetComponent<PointPulse>();
        if (pulse) pulse.StopPulsing(); // sonst überschreibt der Puls-Update() den Verpuff-Scale

        spawner?.HandleDiamondResolved();
        StartCoroutine(Co_PuffAndDestroy());
    }

    private void SpawnDestroyEffect(float scale = 1f)
    {
        if (destroyEffect == null) return;
        var fx = Instantiate(destroyEffect, transform.position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        fx.Play();
        float dur = fx.main.duration + fx.main.startLifetime.constantMax;
        Destroy(fx.gameObject, dur);
    }

    // Diamant schrumpft & verschwindet, DANACH kommt der Zerstören-Effekt in kleiner Größe.
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
