using UnityEngine;

// "Fake" Tap-Element: dient nur als Ablenkung.
//  - Wird es ANGETIPPT, verpufft es (Partikel) → Zeitverlust, kein Punkt.
//  - Wird das ORIGINAL-Element geräumt (getappt/Timeout), verschwindet es lautlos.
// KEIN Punkt, KEIN nächster Point, kein Spawner-Aufruf.
public class FakePoint : MonoBehaviour
{
    [Header("Verpuff-Effekt")]
    [Tooltip("Partikel-PREFAB, das beim Antippen an der Fake-Position instanziiert wird (z.B. Smoke Explosion Dissolve Effect).")]
    [SerializeField] private ParticleSystem puffEffect;

    private bool consumed = false;

    // Vom Spawner aufrufen: startet die Countdown-Visuals (Tarnung wie ein echtes
    // Element). KEIN Selbst-Despawn — das Fake lebt, bis es getappt oder vom
    // Spawner per Dismiss() geräumt wird.
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
            sparks.SetQuadMode(true); // Tap-Element → Quad-Outline
            sparks.StartBurn(reactionTime);
        }

        var pulse = GetComponent<PointPulse>();
        if (pulse) pulse.StartPulsing();
    }

    // Spieler hat das Fake angetippt → verpuffen.
    public void TryTap()
    {
        if (consumed) return;
        if (TutorialManager.IsOrbPhaseActive) return;  // Tutorial nicht stören
        Puff();
    }

    // Vom Spawner aufrufen, wenn das Original geräumt wurde → ebenfalls verpuffen.
    public void Dismiss()
    {
        Puff();
    }

    // Gemeinsames Verpuffen für alle Szenarien (Tap / Original geräumt / Timeout).
    private void Puff()
    {
        if (consumed) return;
        consumed = true;

        // Sprites ausblenden, nicht mehr antippbar
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Puff-Prefab an Fake-Position instanziieren (eigenständig)
        if (puffEffect != null)
        {
            var fx = Instantiate(puffEffect, transform.position, Quaternion.identity);
            fx.Play();
            float dur = fx.main.duration + fx.main.startLifetime.constantMax;
            Destroy(fx.gameObject, dur);
        }

        Destroy(gameObject);
    }
}
