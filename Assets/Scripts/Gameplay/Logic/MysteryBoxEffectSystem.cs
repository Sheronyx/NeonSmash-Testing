using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public enum MysteryBoxEffect { MultiplierX3, MultiplierX2, MultiplierMinus1, Smoke, Colorless, BiggerSize, SmallerSize, ExtraLife }

// Zentrale Steuerung der Zufallsbox-Effekte (Mario-Kart-artige "?"-Box, siehe MysteryBoxPoint /
// MixedPointSpawner.SpawnMysteryBox). Beim Einsammeln wird GEWICHTET ein Effekt gewählt, angewendet
// und nach seiner Dauer wieder aufgehoben. Solange IRGENDEIN Effekt aktiv ist, darf laut Design keine
// weitere Box spawnen — dafür prüft MixedPointSpawner vor jedem Box-Spawn-Wurf IsEffectActive.
public class MysteryBoxEffectSystem : MonoBehaviour
{
    public static MysteryBoxEffectSystem Instance { get; private set; }

    [Header("Effekt-Gewichte (relativ zueinander, müssen sich nicht exakt zu 100 aufsummieren)")]
    [SerializeField] private float weightMultiplierX3     = 6f;
    [SerializeField] private float weightMultiplierX2     = 15f;
    [SerializeField] private float weightMultiplierMinus1 = 9f;
    [SerializeField] private float weightSmoke           = 15f;
    [SerializeField] private float weightColorless       = 10f;
    [SerializeField] private float weightBiggerSize      = 20f;
    [SerializeField] private float weightSmallerSize     = 15f;
    [SerializeField] private float weightExtraLife       = 10f;

    [Header("Multiplikator (alle 3 Varianten)")]
    [SerializeField] private float multiplierDuration = 8f;

    [Header("Traumnebel (Sicht-Overlay)")]
    [Tooltip("Leeres Parent-Objekt mit mehreren Partikelsystemen als Kindern — wird im Spielbereich " +
             "verteilt (siehe RandomizeSmokeParticlePositions) statt einmalig in der Bildschirmmitte, " +
             "weil der Rauch als einziger Effekt durchgehend sichtbar bleibt, während er wirkt.")]
    [FormerlySerializedAs("smokeOverlayPrefab")]
    [SerializeField] private GameObject smokeVfxPrefab;
    [SerializeField] private float smokeDuration = 6f;

    [Header("Farblos")]
    [SerializeField] private float colorlessDuration = 8f;

    [Header("Größe (Normal & Special Mode)")]
    [SerializeField] private float biggerSizeMultiplier  = 1.5f;
    [SerializeField] private float smallerSizeMultiplier = 0.5f;
    [SerializeField] private float sizeDuration = 8f;

    [Header("Ankündigungs-Prefabs (eigene, fertig animierte Partikel-Animation pro Effekt)")]
    [Tooltip("Wartezeit ab dem Antippen (= Start der Zerstören-Explosion am Box-Ort), bevor Ankündigung " +
             "+ der eigentliche Effekt parallel starten.")]
    [SerializeField] private float announceStartDelay = 0.2f;
    [Tooltip("Jeweils ein komplett vorgefertigtes Prefab (Text + Effekt-Animation in einem) — wird beim " +
             "Antippen in der Bildschirmmitte instanziiert und abgespielt.")]
    [SerializeField] private GameObject announceMultiplierX3Prefab;
    [SerializeField] private GameObject announceMultiplierX2Prefab;
    [SerializeField] private GameObject announceMultiplierMinus1Prefab;
    [SerializeField] private GameObject announceSmokePrefab;
    [SerializeField] private GameObject announceColorlessPrefab;
    [SerializeField] private GameObject announceBiggerSizePrefab;
    [SerializeField] private GameObject announceSmallerSizePrefab;
    [SerializeField] private GameObject announceExtraLifePrefab;

    [Header("Extra Life verbraucht (eigene Animation + Spielpause)")]
    [Tooltip("Prefab, das abgespielt wird, wenn eine Extra-Life-Ladung tatsächlich einen eigentlich " +
             "tödlichen Fehler verhindert hat (siehe ConsumeExtraLifeIfActive) — unterscheidet sich vom " +
             "Ankündigungs-Prefab oben, das nur beim ERHALT der Ladung läuft. Spawning bleibt pausiert, " +
             "bis diese Animation fertig ist.")]
    [SerializeField] private GameObject extraLifeConsumedPrefab;

    /// <summary>Solange true, darf keine neue Zufallsbox spawnen (siehe Klassenkommentar).</summary>
    public bool IsEffectActive { get; private set; }

    public int CurrentScoreMultiplier { get; private set; } = 1;
    public bool IsColorlessActive { get; private set; }
    public float CurrentSizeMultiplier { get; private set; } = 1f;
    public bool HasExtraLifeCharge { get; private set; }

    private GameObject smokeOverlayInstance;

    private void Awake() => Instance = this;

    /// <summary>Bei Game Over / Start eines neuen Runs aufgerufen (siehe PhaseManager) — "Play Again"
    /// lädt die Szene NICHT neu, daher würden sonst laufende Effekt-Coroutinen, ein hängendes
    /// Rauch-Overlay, eine ungenutzte Extra-Life-Ladung oder ein noch aktiver Größen-/Farblos-Zustand
    /// in den nächsten Run überlaufen (u.a. IsEffectActive dauerhaft true → keine neuen Boxen,
    /// CurrentSizeMultiplier ≠ 1 → übergroße Elemente zu Run-Beginn). Analog zum ForceStop() der
    /// Special-Mode-Systeme.</summary>
    public void ResetState()
    {
        StopAllCoroutines();

        if (smokeOverlayInstance != null) Destroy(smokeOverlayInstance);
        smokeOverlayInstance = null;

        IsEffectActive = false;
        CurrentScoreMultiplier = 1;
        IsColorlessActive = false;
        CurrentSizeMultiplier = 1f;
        HasExtraLifeCharge = false;
    }

    /// <summary>Multipliziert die Skalierung von go mit dem aktuellen Größen-Effekt (falls einer
    /// aktiv ist) — gemeinsamer Helfer für alle Spawn-Stellen (Normal Mode UND die 3 Special Modes).
    /// Direkt nach jedem Instantiate() aufrufen.</summary>
    public static void ApplySizeMultiplier(GameObject go)
    {
        if (Instance == null || go == null) return;
        if (!Mathf.Approximately(Instance.CurrentSizeMultiplier, 1f))
            go.transform.localScale *= Instance.CurrentSizeMultiplier;
    }

    /// <summary>Vom MysteryBoxPoint beim Antippen aufgerufen: würfelt den Effekt, wartet announceStartDelay
    /// ab dem Antippen (parallel zur Zerstören-Explosion am Box-Ort), spielt DANN parallel das
    /// Ankündigungs-Prefab in der Bildschirmmitte UND den eigentlichen Effekt (z.B. den Rauch) ab. Der
    /// Spawner bleibt pausiert, bis die Ankündigungs-Partikelanimation komplett durchgelaufen ist.</summary>
    public void PlayCollectSequence(MixedPointSpawner spawner)
    {
        StartCoroutine(Co_CollectSequence(spawner));
    }

    private IEnumerator Co_CollectSequence(MixedPointSpawner spawner)
    {
        if (IsEffectActive) // Spawn-Gate sollte das schon verhindern, defensiv trotzdem
        {
            spawner?.ResumeSpawningAfterPause();
            yield break;
        }

        MysteryBoxEffect effect = RollEffect();

        // 1) Kurzer, fester Delay ab dem Antippen (nicht an die Explosionsdauer gekoppelt).
        yield return new WaitForSeconds(announceStartDelay);

        // 2) Danach parallel: Ankündigungs-Prefab (enthält bereits Text + Effekt-Animation in einem) an
        // seiner eigenen, im Prefab festgelegten Position UND der eigentliche Gameplay-Effekt. Smoke
        // ausgenommen: dessen sichtbarer Teil ist NICHT das Ankündigungs-Prefab, sondern das separate, im
        // Spielbereich verteilte Overlay (siehe Co_Smoke) — die Ankündigung selbst (Text) spielt trotzdem
        // ganz normal mit. Colorless ebenfalls ausgenommen: objektbasierter Effekt (siehe
        // PortalColorlessEffect) statt reinem Partikelsystem, braucht eigene Dauer-Berechnung.
        float announceDuration = effect == MysteryBoxEffect.Colorless
            ? PlayColorlessAnnouncement()
            : PlayParticlePrefab(GetAnnouncementPrefab(effect), warnIfMissing: true);
        ApplyEffect(effect, spawner);

        // 3) Erst wenn die Ankündigung fertig ist, geht's weiter.
        yield return new WaitForSeconds(announceDuration);

        spawner?.ResumeSpawningAfterPause();
    }

    private GameObject GetAnnouncementPrefab(MysteryBoxEffect effect) => effect switch
    {
        MysteryBoxEffect.MultiplierX3     => announceMultiplierX3Prefab,
        MysteryBoxEffect.MultiplierX2     => announceMultiplierX2Prefab,
        MysteryBoxEffect.MultiplierMinus1 => announceMultiplierMinus1Prefab,
        MysteryBoxEffect.Smoke            => announceSmokePrefab,
        // Colorless bewusst nicht hier — läuft über PlayColorlessAnnouncement(), siehe Co_CollectSequence.
        MysteryBoxEffect.BiggerSize       => announceBiggerSizePrefab,
        MysteryBoxEffect.SmallerSize      => announceSmallerSizePrefab,
        MysteryBoxEffect.ExtraLife        => announceExtraLifePrefab,
        _                                  => null
    };

    // Colorless ist objektbasiert (siehe PortalColorlessEffect: Portal erscheint, Steine fliegen rein
    // und werden eingesogen, Quetsch-Effekt + Burst, Portal fadet weg) statt reinem Partikelsystem — die
    // Dauer ergibt sich deterministisch aus dessen eigenen Timing-Feldern, nicht aus einer Partikel-
    // Lifetime wie bei PlayParticlePrefab.
    private float PlayColorlessAnnouncement()
    {
        if (announceColorlessPrefab == null)
        {
            Debug.LogWarning("[MysteryBoxEffectSystem] Kein Announce Colorless Prefab zugewiesen.");
            return 0f;
        }

        var go = Instantiate(announceColorlessPrefab);
        // InChildren statt nur GetComponent: PortalColorlessEffect sitzt auf "Portal Colorless", das
        // innerhalb des Announce-Prefabs ein Kind sein kann statt dessen Root-Objekt.
        var colorlessEffect = go.GetComponentInChildren<PortalColorlessEffect>(true);
        if (colorlessEffect == null)
        {
            Debug.LogWarning("[MysteryBoxEffectSystem] Announce Colorless Prefab hat keine PortalColorlessEffect-Komponente (auch nicht in Kind-Objekten).");
            Destroy(go);
            return 0f;
        }

        colorlessEffect.Play();
        Destroy(go, colorlessEffect.TotalDuration);
        return colorlessEffect.TotalDuration;
    }

    // Instanziiert prefab UNVERÄNDERT an seiner eigenen im Prefab festgelegten Position (kein
    // Positions-Override), spielt alle Kind-Partikelsysteme ab und räumt es nach der längsten Laufzeit
    // selbst auf (analog zu MysteryBoxPoint.SpawnDestroyEffect) — gibt diese Laufzeit zurück.
    // warnIfMissing=true nur für Prefabs, die eigentlich immer gesetzt sein sollten (Ankündigung).
    // useUnscaledTime=true für Aufrufer, die währenddessen Time.timeScale=0 setzen (siehe
    // LivesManager.Co_ExtraLifeConsumed) — sonst würde die Partikel-Simulation selbst einfrieren.
    private float PlayParticlePrefab(GameObject prefab, bool warnIfMissing = false, bool useUnscaledTime = false)
    {
        if (prefab == null)
        {
            if (warnIfMissing)
                Debug.LogWarning("[MysteryBoxEffectSystem] Kein Ankündigungs-Prefab für diesen Effekt zugewiesen.");
            return 0f;
        }

        var go = Instantiate(prefab);
        float dur = 0f;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (useUnscaledTime)
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
            ps.Play();
            dur = Mathf.Max(dur, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        Destroy(go, dur);
        return dur;
    }

    private void ApplyEffect(MysteryBoxEffect effect, MixedPointSpawner spawner)
    {
        switch (effect)
        {
            case MysteryBoxEffect.MultiplierX3:     StartCoroutine(Co_Multiplier(3));   break;
            case MysteryBoxEffect.MultiplierX2:     StartCoroutine(Co_Multiplier(2));   break;
            case MysteryBoxEffect.MultiplierMinus1: StartCoroutine(Co_Multiplier(-1));  break;
            case MysteryBoxEffect.Smoke:            StartCoroutine(Co_Smoke(spawner));  break;
            case MysteryBoxEffect.Colorless:        StartCoroutine(Co_Colorless());     break;
            case MysteryBoxEffect.BiggerSize:       StartCoroutine(Co_Size(biggerSizeMultiplier));  break;
            case MysteryBoxEffect.SmallerSize:       StartCoroutine(Co_Size(smallerSizeMultiplier)); break;
            case MysteryBoxEffect.ExtraLife:        ActivateExtraLife(); break;
        }
    }

    private MysteryBoxEffect RollEffect()
    {
        float total = weightMultiplierX3 + weightMultiplierX2 + weightMultiplierMinus1 + weightSmoke +
                      weightColorless + weightBiggerSize + weightSmallerSize + weightExtraLife;
        float r = Random.Range(0f, total);

        if ((r -= weightMultiplierX3) < 0f) return MysteryBoxEffect.MultiplierX3;
        if ((r -= weightMultiplierX2) < 0f) return MysteryBoxEffect.MultiplierX2;
        if ((r -= weightMultiplierMinus1) < 0f) return MysteryBoxEffect.MultiplierMinus1;
        if ((r -= weightSmoke) < 0f) return MysteryBoxEffect.Smoke;
        if ((r -= weightColorless) < 0f) return MysteryBoxEffect.Colorless;
        if ((r -= weightBiggerSize) < 0f) return MysteryBoxEffect.BiggerSize;
        if ((r -= weightSmallerSize) < 0f) return MysteryBoxEffect.SmallerSize;
        return MysteryBoxEffect.ExtraLife;
    }

    private IEnumerator Co_Multiplier(int value)
    {
        IsEffectActive = true;
        CurrentScoreMultiplier = value;
        yield return new WaitForSeconds(multiplierDuration);
        CurrentScoreMultiplier = 1;
        IsEffectActive = false;
    }

    private IEnumerator Co_Smoke(MixedPointSpawner spawner)
    {
        IsEffectActive = true;
        if (smokeVfxPrefab != null)
        {
            smokeOverlayInstance = Instantiate(smokeVfxPrefab);
            RandomizeSmokeParticlePositions(smokeOverlayInstance, spawner);
        }

        yield return new WaitForSeconds(smokeDuration);

        if (smokeOverlayInstance != null) Destroy(smokeOverlayInstance);
        smokeOverlayInstance = null;
        IsEffectActive = false;
    }

    // Positioniert jedes Kind-Partikelsystem des Overlays im normalen Spawn-Bereich (derselbe Bereich
    // wie die 3 Farb-Slots), statt in einem festen Weltbereich um den Ursprung — verhindert Rauch
    // außerhalb des Spielfelds unabhängig von Kamera/Bildschirmgröße.
    private void RandomizeSmokeParticlePositions(GameObject overlay, MixedPointSpawner spawner)
    {
        if (spawner == null) return;
        foreach (var ps in overlay.GetComponentsInChildren<ParticleSystem>(true))
            ps.transform.position = spawner.GetRandomWorldPosInSpawnArea();
    }

    private IEnumerator Co_Colorless()
    {
        IsEffectActive = true;
        IsColorlessActive = true;
        yield return new WaitForSeconds(colorlessDuration);
        IsColorlessActive = false;
        IsEffectActive = false;
    }

    private IEnumerator Co_Size(float multiplier)
    {
        IsEffectActive = true;
        CurrentSizeMultiplier = multiplier;
        yield return new WaitForSeconds(sizeDuration);
        CurrentSizeMultiplier = 1f;
        IsEffectActive = false;
    }

    // Setzt bewusst NICHT IsEffectActive — im Gegensatz zu den anderen 5 Effekten soll das Halten einer
    // ungenutzten Extra-Life-Ladung neue Zufallsboxen nicht blockieren, sie kann beliebig lange
    // unbenutzt herumliegen, bis sie tatsächlich einen Fehler verhindert (siehe ConsumeExtraLifeIfActive).
    private void ActivateExtraLife()
    {
        HasExtraLifeCharge = true;
    }

    /// <summary>Vom MixedPointSpawner aufgerufen, kurz bevor ein Timeout eigentlich Game Over
    /// auslösen würde. Gibt true zurück UND verbraucht die Ladung, wenn eine aktiv war — der
    /// Aufrufer räumt den Fehler dann nur normal weg, statt Game Over auszulösen.</summary>
    public bool ConsumeExtraLifeIfActive()
    {
        if (!HasExtraLifeCharge) return false;
        HasExtraLifeCharge = false;
        return true;
    }

    /// <summary>Vom MixedPointSpawner aufgerufen, NACHDEM ConsumeExtraLifeIfActive() true zurückgegeben
    /// hat: spielt die Verbrauchs-Animation in der Bildschirmmitte ab und gibt den Spawner erst danach
    /// wieder frei (spawner ist zu diesem Zeitpunkt bereits per SetBannerPause(true) pausiert).</summary>
    public void PlayExtraLifeConsumedSequence(MixedPointSpawner spawner)
    {
        StartCoroutine(Co_ExtraLifeConsumedSequence(spawner));
    }

    private IEnumerator Co_ExtraLifeConsumedSequence(MixedPointSpawner spawner)
    {
        float dur = PlayParticlePrefab(extraLifeConsumedPrefab, warnIfMissing: true);
        yield return new WaitForSeconds(dur);
        spawner?.ResumeSpawningAfterPause();
    }

    /// <summary>Vom LivesManager aufgerufen, NACHDEM ConsumeExtraLifeIfActive() true zurückgegeben hat
    /// (Special Modes, Shocker-Antippen, Peek-a-boo, ...) — spielt nur die Verbrauchs-Animation ab und
    /// gibt deren Laufzeit zurück. Die eigentliche Spielpause übernimmt der LivesManager selbst über
    /// dieselbe Time.timeScale-Pause wie beim normalen Lebensverlust, da diese Aufrufer kein
    /// MixedPointSpawner-Pausieren nutzen können (Special Modes laufen mit eigener Spawn-Loop).</summary>
    public float PlayExtraLifeConsumedAnnouncement()
    {
        return PlayParticlePrefab(extraLifeConsumedPrefab, warnIfMissing: true, useUnscaledTime: true);
    }
}
