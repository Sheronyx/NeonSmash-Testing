using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Zentraler Dienst (Singleton, EIN Objekt in der Szene): lässt frisch instanziierte Elemente
// erscheinen, statt einfach in voller Größe/Position aufzutauchen. Zwei Varianten, je nach Prefab-Aufbau:
//  - Mehrteilige Prefabs (haben MagneticFragmentFloat/-Orbit-Stücke, z.B. Diamant/Zufallsbox/Colorless):
//    Root bleibt sofort auf voller Zielgröße/-position, die einzelnen Stücke starten zentriert
//    übereinander und schweben von dort zu ihrer im Editor gesetzten Position (Co_PiecesFlyIn).
//  - Einfache Prefabs (ein Visual-Sprite ohne solche Stücke): klassischer Scale-Pop von klein auf
//    Zielgröße (Co_PopIn) — bei denen gäbe es beim Auseinanderschweben nichts sichtbar zu bewegen.
// Gilt einheitlich für ALLE "zu zerstörenden" UND "Hindrance"-Elemente (Farbe, Thunder/Shocker, Fake,
// Diamant, Zufallsbox).
// Rein optisch — der Collider wird NICHT angefasst, Elemente sind von der ersten Sekunde an
// antippbar/swipebar, auch während sie noch einschweben/wachsen. Slot-Bookkeeping und Reaktionszeit
// starten beim Aufrufer bereits VOR dieser Animation, nicht erst in onArrived.
//
// Liegt EINMAL in der Szene (z.B. auf dem MixedPointSpawner-Objekt) statt auf jedem einzelnen
// Prefab — die Tuning-Werte gelten damit automatisch identisch für alle Normal-Mode-Elemente, ohne
// sie auf mehreren Prefabs synchron halten zu müssen.
public class PointFlyIn : MonoBehaviour
{
    public static PointFlyIn Instance { get; private set; }

    [Header("Spawn-Pop (klein → groß) — für einteilige Prefabs")]
    [Tooltip("Startgröße relativ zur Zielgröße (0.4 = beginnt bei 40% der Zielgröße).")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float startScalePercent = 0.4f;
    [SerializeField] private float growDuration = 0.25f;

    [Header("Stücke auseinanderschweben — für mehrteilige Prefabs (MagneticFragmentFloat/-Orbit)")]
    [SerializeField] private float pieceFlyDuration = 0.35f;
    [Tooltip("Wie stark die Stücke zu Beginn zufällig verdreht sind, bevor sie sich beim Einschweben in ihre Ausgangsrotation einpendeln (Grad, in beide Richtungen).")]
    [SerializeField] private float pieceStartRotationVariance = 35f;

    private void Awake() => Instance = this;

    public void PlaySpawnAnimation(GameObject target, Vector3 targetPosition, Vector3 targetScale, Action onArrived)
    {
        if (target == null) { onArrived?.Invoke(); return; }

        target.GetComponentInChildren<SpawnPulse>()?.Cancel();

        target.transform.position = targetPosition;

        // Sprites sofort unsichtbar machen: die eigentliche Animation startet erst einen Frame später
        // (siehe Kommentare unten), bis dahin würde das Element sonst kurz in voller Originalgröße/
        // -position aufblitzen, bevor es in die Startpose der Animation "springt" — sichtbares Flackern.
        var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderers) r.enabled = false;

        var floats = target.GetComponentsInChildren<MagneticFragmentFloat>(true);
        var orbits = target.GetComponentsInChildren<MagneticFragmentOrbit>(true);

        if (floats.Length > 0 || orbits.Length > 0)
        {
            target.transform.localScale = targetScale;
            StartCoroutine(Co_PiecesFlyIn(target, floats, orbits, renderers, onArrived));
        }
        else
        {
            StartCoroutine(Co_PopIn(target, targetScale, renderers, onArrived));
        }
    }

    // Mehrteilige Prefabs: Root behält sofort seine volle Zielgröße/-position, stattdessen starten
    // die einzelnen Fragment-Stücke zentriert übereinander (wie beim alten Kompressions-Bug) und
    // schweben von dort zu ihrer eigentlichen, im Editor gesetzten Position — mit leichter
    // Rotations-Einpendelung für einen organischen statt robotischen Eindruck.
    private IEnumerator Co_PiecesFlyIn(GameObject target, MagneticFragmentFloat[] floats,
        MagneticFragmentOrbit[] orbits, SpriteRenderer[] renderers, Action onArrived)
    {
        // Komponenten SOFORT deaktivieren, bevor ihr eigenes Start() läuft — sonst würden sie ihre
        // Schwebe-Baseline (Position/Rotation) an der falschen Stelle festhalten. Da wir hier
        // synchron (kein yield davor) deaktivieren, läuft ihr Start() erst, wenn wir sie am Ende
        // wieder aktivieren — mit der dann korrekten, endgültigen Position/Rotation als Basis.
        foreach (var f in floats) f.enabled = false;
        foreach (var o in orbits) o.enabled = false;

        var pieces = new List<Transform>(floats.Length + orbits.Length);
        foreach (var f in floats) pieces.Add(f.transform);
        foreach (var o in orbits) pieces.Add(o.transform);

        // Einen Frame warten, BEVOR wir die "Heimat"-Rotation der Stücke einfangen: andere Skripte auf
        // der Wurzel (z.B. SwipePoint.Start() → RotateIcon(), dreht die Wurzel auf die Diagonal-/
        // Richtungs-Anzeige) laufen ERST jetzt. Würden wir sofort (synchron) einfangen, bekämen wir die
        // Rotation VOR dieser Drehung — die Stücke würden dauerhaft auf der ungedrehten Ausgangslage
        // einfrieren, egal welche Richtung eigentlich angezeigt werden soll. Die Float/Orbit-Deaktivierung
        // oben bleibt davon unberührt (die ist schon synchron passiert, läuft also so oder so nicht an).
        yield return null;
        if (target == null) { onArrived?.Invoke(); yield break; }

        Vector3 center = target.transform.position;
        var homePos = new Vector3[pieces.Count];
        var homeRot = new Quaternion[pieces.Count];
        var startRot = new Quaternion[pieces.Count];

        for (int i = 0; i < pieces.Count; i++)
        {
            homePos[i] = pieces[i].position;
            homeRot[i] = pieces[i].rotation;
            startRot[i] = homeRot[i] * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-pieceStartRotationVariance, pieceStartRotationVariance));
            pieces[i].position = center;
            pieces[i].rotation = startRot[i];
        }

        foreach (var r in renderers) if (r != null) r.enabled = true;

        float t = 0f;
        while (t < pieceFlyDuration)
        {
            if (target == null) { onArrived?.Invoke(); yield break; }
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / pieceFlyDuration));
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].position = Vector3.Lerp(center, homePos[i], k);
                pieces[i].rotation = Quaternion.Slerp(startRot[i], homeRot[i], k);
            }
            yield return null;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] == null) continue;
            pieces[i].position = homePos[i];
            pieces[i].rotation = homeRot[i];
        }

        if (target == null) { onArrived?.Invoke(); yield break; }

        foreach (var f in floats) if (f != null) f.enabled = true;
        foreach (var o in orbits) if (o != null) o.enabled = true;

        target.GetComponent<SwipePoint>()?.RefreshEffectiveRadius();

        onArrived?.Invoke();
    }

    // Einteilige Prefabs (nur ein Visual-Sprite, kein Kind-Stücke-Aufbau): klassischer Scale-Pop.
    private IEnumerator Co_PopIn(GameObject target, Vector3 targetScale, SpriteRenderer[] renderers, Action onArrived)
    {
        // PointPulse pulsiert ebenfalls die Root-Scale (FinishSlotSpawn startet es bereits VOR dieser
        // Animation, da das Element ja von Anfang an treffbar sein soll) — würde sich sonst direkt mit
        // unserer eigenen Scale-Animation beißen. Deaktivieren, bis wir fertig sind, dann sauber neu
        // starten (statt einfach wieder zu aktivieren, damit die Puls-Phase nicht mitten drin einsetzt).
        var pulse = target.GetComponent<PointPulse>();
        if (pulse != null) pulse.enabled = false;

        // Einen Frame warten, BEVOR die Root-Skalierung verändert wird: Kind-Skripte, die in ihrem
        // eigenen Start() eine Baseline relativ zur aktuellen Position/Skalierung festhalten, laufen
        // sonst GENAU in dem Frame los, in dem wir die Root-Scale schon auf startScalePercent gesetzt
        // haben, und verewigen dadurch fälschlich diese geschrumpfte Zwischengröße als ihre "Heimat".
        yield return null;
        if (target == null) { onArrived?.Invoke(); yield break; }

        target.transform.localScale = targetScale * startScalePercent;

        foreach (var r in renderers) if (r != null) r.enabled = true;

        float t = 0f;
        while (t < growDuration)
        {
            if (target == null) { onArrived?.Invoke(); yield break; }
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / growDuration);
            target.transform.localScale = targetScale * Mathf.Lerp(startScalePercent, 1f, k);
            yield return null;
        }
        target.transform.localScale = targetScale;

        if (target == null) { onArrived?.Invoke(); yield break; }

        target.GetComponent<SwipePoint>()?.RefreshEffectiveRadius();

        if (pulse != null) { pulse.enabled = true; pulse.StartPulsing(); }

        onArrived?.Invoke();
    }
}
