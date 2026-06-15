using System.Collections;
using UnityEngine;

// Donnerschock-Element: darf NICHT angetippt werden.
//  - Wird es ANGETIPPT → Leben verlieren + großer Partikel-Effekt + sofort weg.
//  - Läuft die Zeit ab → schrumpft auf Scale 0, dann kleiner Partikel-Effekt.
// Ersetzt anstelle eines normalen Tap/Swipe-Elements (nicht parallel).
public class ThunderPoint : MonoBehaviour
{
    [Header("Tap-Effekt (groß)")]
    [Tooltip("Partikel-PREFAB beim Antippen (groß, volle Wucht).")]
    [SerializeField] private ParticleSystem tapEffect;

    [Header("Timeout-Effekt (klein)")]
    [Tooltip("Partikel-PREFAB beim Timeout (klein, dezent).")]
    [SerializeField] private ParticleSystem timeoutEffect;
    [Tooltip("Scale des Timeout-Partikel-Prefabs (kleiner als beim Tap).")]
    [SerializeField] private float timeoutEffectScale = 0.4f;
    [Tooltip("Dauer der Schrumpf-Animation beim Timeout.")]
    [SerializeField] private float shrinkDuration = 0.35f;

    private bool consumed = false;
    private Vector3 spawnPosition;

    void Awake()
    {
        spawnPosition = transform.position;
    }

    // Vom Spawner aufrufen: startet Countdown-Visuals und plant Timeout-Despawn.
    public void Activate(float reactionTime)
    {
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

        var pulse = GetComponent<PointPulse>();
        if (pulse) pulse.StartPulsing();

        CancelInvoke(nameof(OnTimeout));
        if (reactionTime > 0f)
            Invoke(nameof(OnTimeout), reactionTime);
    }

    // Spieler hat das Element angetippt → Schaden + großer Effekt + sofort weg.
    public void TryTap()
    {
        if (consumed) return;
        if (TutorialManager.IsOrbPhaseActive) return;

        consumed = true;
        CancelInvoke(nameof(OnTimeout));

        HideAndDisable();
        SpawnEffect(tapEffect, 1f);

        if (LivesManager.Instance != null)
        {
            bool stillAlive = LivesManager.Instance.LoseLife(spawnPosition);
            if (ScreenShakeManager.Instance != null)
                ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
            if (!stillAlive)
            {
                var spawner = FindFirstObjectByType<MixedPointSpawner>();
                spawner?.TriggerGameOverFromGravity();
            }
        }

        ComboManager.Instance?.RegisterMiss();
        Destroy(gameObject);
    }

    // Zeit abgelaufen → schrumpft auf 0, dann kleiner Effekt, kein Schaden.
    private void OnTimeout()
    {
        if (consumed) return;
        consumed = true;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(Co_ShrinkAndPuff());
    }

    private IEnumerator Co_ShrinkAndPuff()
    {
        Vector3 startScale = transform.localScale;
        float t = 0f;

        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / shrinkDuration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        SpawnEffect(timeoutEffect, timeoutEffectScale);
        Destroy(gameObject);
    }

    private void SpawnEffect(ParticleSystem prefab, float scale)
    {
        if (prefab == null) return;
        var fx = Instantiate(prefab, transform.position, Quaternion.identity);
        fx.transform.localScale = Vector3.one * scale;
        fx.Play();
        float dur = fx.main.duration + fx.main.startLifetime.constantMax;
        Destroy(fx.gameObject, dur);
    }

    private void HideAndDisable()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }
}
