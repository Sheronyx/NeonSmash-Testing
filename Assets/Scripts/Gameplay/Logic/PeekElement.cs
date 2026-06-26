using System.Collections;
using UnityEngine;

public enum PeekType { Tap, Shock, Fake }

// Helfer auf einem Peek-a-boo-Element. Die Elemente einer Runde sind als Gruppe
// gekoppelt: Ein Klick löst alle aus — ABER das echte Tap-Element löst nur dann
// sein positives (Punkt) aus, wenn man es selbst trifft. Trifft man stattdessen
// einen Fake (oder nichts), löst das Tap sein negatives aus (Leben verlieren).
public class PeekElement : MonoBehaviour
{
    public PeekType Type { get; private set; }
    public bool Resolved => resolved;

    private bool resolved;
    private PeekElement[] group;

    public void Init(PeekType type, float holdDuration)
    {
        Type = type;

        // SpawnPulse abbrechen (kein Aufpoppen im Peek-a-boo) — nur disable reicht
        // nicht, da die Pop-Coroutine in OnEnable schon läuft.
        var sp = GetComponent<SpawnPulse>();
        if (sp != null) sp.Cancel();

        DisableIfPresent<IdleFloat>();

        foreach (var cs in GetComponentsInChildren<CountdownSquare>(true)) cs.StartCountdown(holdDuration);
        foreach (var f  in GetComponentsInChildren<FuseCountdown>(true))   f.StartBurn(holdDuration);
        foreach (var l  in GetComponentsInChildren<LineFuse>(true))        l.StartBurn(holdDuration);
        foreach (var b  in GetComponentsInChildren<BurnSparks>(true))      { b.SetQuadMode(true); b.StartBurn(holdDuration); }
    }

    public void SetGroup(PeekElement[] g) => group = g;

    // --- Scale-Animation (klein in der Mitte → groß an Endposition → Pop) ---
    private Vector3 fullScale = Vector3.one;

    // Merkt sich die Vollgröße (Prefab-Scale) und startet klein.
    public void InitPeekScale(float startFraction)
    {
        fullScale = transform.localScale;
        transform.localScale = fullScale * startFraction;
    }

    // Wird während des Rausschießens aufgerufen (0..1 der Vollgröße).
    public void SetScaleFraction(float f) => transform.localScale = fullScale * f;

    // Pop an der Endposition: Vollgröße → Overshoot → Vollgröße.
    public void PlayPeekPop(float overshoot, float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(Co_Pop(overshoot, duration));
    }

    private IEnumerator Co_Pop(float overshoot, float duration)
    {
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(fullScale, fullScale * overshoot, Mathf.SmoothStep(0f, 1f, t / half));
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(fullScale * overshoot, fullScale, Mathf.SmoothStep(0f, 1f, t / half));
            yield return null;
        }
        transform.localScale = fullScale;
    }

    // Vom Input (Tap): klickt man eines, lösen alle aus.
    //  - Geklicktes Element + alle Fakes → positiver Effekt (Hit)
    //  - Das Tap-Element löst nur Hit aus, wenn man ES selbst geklickt hat,
    //    sonst Miss (Leben verlieren).
    public void OnHit()
    {
        if (resolved) return;

        bool clickedIsTap = Type == PeekType.Tap;

        if (group == null) { FireHit(); return; }

        foreach (var e in group)
        {
            if (e == null) continue;
            if (e == this)                                   e.FireHit();
            else if (!clickedIsTap && e.Type == PeekType.Tap) e.FireMiss();
            else                                              e.FireHit();
        }
    }

    // Positiver Effekt (getroffen)
    public void FireHit()
    {
        if (resolved) return;
        resolved = true;

        switch (Type)
        {
            case PeekType.Tap:
                ScoreManager.Instance?.AddPointsFromHit();
                // Peek-Elemente haben keine Farbe → neutraler Treffer, kein Kombo-Aufbau
                AudioManager.Instance?.PlayNormalPoint();
                SendMessage("SpawnExplosion", SendMessageOptions.DontRequireReceiver);
                Destroy(gameObject);
                break;

            case PeekType.Shock:
                var ths = GetComponent<ThunderPoint>();
                if (ths != null) ths.TryTap(); else Destroy(gameObject);
                break;

            case PeekType.Fake:
                var fps = GetComponent<FakePoint>();
                if (fps != null) fps.TryTap(); else Destroy(gameObject);
                break;
        }
    }

    // Negativer Effekt (verpasst / falsch) — Zeitablauf-Explosion + Leben verlieren.
    public void FireMiss()
    {
        if (resolved) return;
        resolved = true;

        switch (Type)
        {
            case PeekType.Tap:
                if (LivesManager.Instance != null)
                {
                    LivesManager.Instance.LoseLife(transform.position);
                    if (ScreenShakeManager.Instance != null) ScreenShakeManager.Instance.Shake(0.35f, 0.25f);
                }
                ComboManager.Instance?.RegisterMiss();
                Destroy(gameObject);
                break;

            case PeekType.Shock:
                var th = GetComponent<ThunderPoint>();
                if (th != null) th.Vanish(); else Destroy(gameObject);
                break;

            case PeekType.Fake:
                var fp = GetComponent<FakePoint>();
                if (fp != null) fp.Dismiss(); else Destroy(gameObject);
                break;
        }
    }

    // Nichts geklickt → alle ihr negatives/normales Verhalten (Tap verliert Leben).
    public void ResolveTimeout() => FireMiss();

    private void DisableIfPresent<T>() where T : MonoBehaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }
}
