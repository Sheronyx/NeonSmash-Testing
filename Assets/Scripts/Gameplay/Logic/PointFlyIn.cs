using System;
using System.Collections;
using UnityEngine;

// Zentraler Dienst (Singleton, EIN Objekt in der Szene): lässt frisch instanziierte Elemente
// erscheinen. Zwei Modi:
//
// Play() — einfacher Pop-in-place (klein → Zielgröße mit Überschwingen), für Thunder/Diamant.
//
// PlayDissolveSpawn() — für die drei normalen Farbelemente: "Zaubermaterie" entsteht an Ort und
// Stelle. Das per SpawnDissolveTarget markierte Kind-Objekt blendet per Dissolve-Shader
// (_DissolveAmount 1→0) smooth ein. Parallel wachsen alle per SpawnGrowTarget markierten Kind-
// Objekte von Skalierung 0 auf ihre Originalgröße (z.B. innerRect/outerRect/FuseVisual — Name und
// Anzahl variieren pro Skin, deshalb Marker-Komponenten statt fester Namenssuche). Collider bleibt
// bis zum Ende der Materialisierung (dissolveDuration) deaktiviert — erst dann feuert onArrived,
// worüber der Aufrufer die Reaktionszeit startet. Das Element ist also erst "da" (antippbar, Uhr
// läuft), sobald es fertig sichtbar gespawnt ist, nicht schon während des Entstehens.
//
// Liegt EINMAL in der Szene (z.B. auf dem MixedPointSpawner-Objekt) statt auf jedem einzelnen
// Prefab — die Tuning-Werte gelten damit automatisch identisch für alle Normal-Mode-Elemente, ohne
// sie auf mehreren Prefabs synchron halten zu müssen.
public class PointFlyIn : MonoBehaviour
{
    public static PointFlyIn Instance { get; private set; }

    [Header("Pop-In (Thunder/Diamant — einfaches Wachstum mit Überschwingen)")]
    [SerializeField] private float duration          = 0.32f;
    [SerializeField] private float startScalePercent = 0.35f;
    [SerializeField] private float scaleOvershoot    = 1.70158f;

    [Header("Dissolve-Spawn (Farbelemente — \"Zaubermaterie\" entsteht an Ort und Stelle)")]
    [Tooltip("Dauer bis _DissolveAmount von 1 (unsichtbar) auf 0 (voll sichtbar) animiert ist — Shader Graphs/2D Dissolve Shader.")]
    [SerializeField] private float dissolveDuration = 0.35f;

    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");

    private void Awake() => Instance = this;

    // target = das bereits instanziierte Element, das erscheinen soll.
    // targetPosition/targetScale = die vom Aufrufer bereits berechnete finale Spawn-Pose.
    public void Play(GameObject target, Vector3 targetPosition, Vector3 targetScale, Action onArrived)
    {
        if (target == null) { onArrived?.Invoke(); return; }

        target.GetComponentInChildren<SpawnPulse>()?.Cancel();

        var col = target.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 startScale = targetScale * startScalePercent;

        target.transform.position   = targetPosition;
        target.transform.localScale = startScale;

        StartCoroutine(Co_PopIn(target, col, startScale, targetPosition, targetScale, onArrived));
    }

    private IEnumerator Co_PopIn(GameObject target, Collider2D col, Vector3 startScale, Vector3 targetPos,
                                  Vector3 targetScale, Action onArrived)
    {
        Transform tr = target.transform;

        float t = 0f;
        while (t < duration)
        {
            if (target == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float scaleK = EaseOutBack(k, scaleOvershoot);
            tr.localScale = Vector3.LerpUnclamped(startScale, targetScale, scaleK);

            yield return null;
        }

        if (target == null) yield break;

        tr.position   = targetPos;
        tr.localScale = targetScale;

        target.GetComponent<SwipePoint>()?.RefreshEffectiveRadius();
        if (col != null) col.enabled = true;

        onArrived?.Invoke();
    }

    public void PlayDissolveSpawn(GameObject target, Vector3 targetPosition, Vector3 targetScale, Action onArrived)
    {
        if (target == null) { onArrived?.Invoke(); return; }

        target.GetComponentInChildren<SpawnPulse>()?.Cancel();

        // Element steht sofort auf Zielgröße/-position — das Dissolve-Material übernimmt die
        // gesamte "Entsteht gerade"-Optik, kein Scale-Pop des Root-Objekts mehr nötig.
        target.transform.position   = targetPosition;
        target.transform.localScale = targetScale;

        // Collider bleibt deaktiviert, bis die Materialisierung abgeschlossen ist (siehe
        // Co_FinishDissolveSpawn) — nicht antippbar, solange das Element noch entsteht.
        var col = target.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var dissolveTarget = target.GetComponentInChildren<SpawnDissolveTarget>();
        if (dissolveTarget != null)
        {
            var sr = dissolveTarget.GetComponent<SpriteRenderer>();
            var mat = sr.material; // .material (nicht sharedMaterial) → automatische Instanz pro Objekt
            mat.SetFloat(DissolveAmountId, 1f);
            StartCoroutine(Co_Dissolve(target, mat));
        }

        // Alle markierten Kind-Objekte (innerRect/outerRect/FuseVisual je nach Skin) wachsen parallel
        // von 0 auf ihre Originalgröße. NICHT auf Objekte gesetzt, die ihre Skalierung selbst
        // verwalten (z.B. CountdownSquare, siehe SpawnGrowTarget-Kommentar) — daher hier keine
        // Sonderbehandlung mehr nötig, die Marker-Zuweisung im Prefab regelt das bereits.
        var growTargets = target.GetComponentsInChildren<SpawnGrowTarget>();
        foreach (var growTarget in growTargets)
            StartCoroutine(Co_GrowFromZero(growTarget.transform));

        StartCoroutine(Co_FinishDissolveSpawn(target, col, onArrived));
    }

    // Wartet, bis die Materialisierung (Dissolve + Grow-Targets, beide dissolveDuration lang) fertig
    // ist, aktiviert dann erst den Collider und feuert onArrived — darüber startet der Aufrufer
    // üblicherweise die Reaktionszeit, die also erst ab hier läuft, nicht schon beim Spawn-Aufruf.
    private IEnumerator Co_FinishDissolveSpawn(GameObject target, Collider2D col, Action onArrived)
    {
        yield return new WaitForSeconds(dissolveDuration);

        if (target == null) { onArrived?.Invoke(); yield break; }

        target.GetComponent<SwipePoint>()?.RefreshEffectiveRadius();
        if (col != null) col.enabled = true;

        onArrived?.Invoke();
    }

    private IEnumerator Co_Dissolve(GameObject target, Material mat)
    {
        float t = 0f;
        while (t < dissolveDuration)
        {
            if (target == null || mat == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dissolveDuration);
            mat.SetFloat(DissolveAmountId, Mathf.SmoothStep(1f, 0f, k));
            yield return null;
        }
        if (target != null && mat != null) mat.SetFloat(DissolveAmountId, 0f);
    }

    private IEnumerator Co_GrowFromZero(Transform tr)
    {
        if (tr == null) yield break;
        Vector3 target = tr.localScale;
        tr.localScale = Vector3.zero;

        float t = 0f;
        while (t < dissolveDuration)
        {
            if (tr == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dissolveDuration);
            tr.localScale = target * Mathf.SmoothStep(0f, 1f, k);
            yield return null;
        }
        if (tr != null) tr.localScale = target;
    }

    // Robert-Penner-Style Ease-Out-Back: 0→1, überschwingt kurz über 1 hinaus, landet exakt bei 1.
    private static float EaseOutBack(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}
