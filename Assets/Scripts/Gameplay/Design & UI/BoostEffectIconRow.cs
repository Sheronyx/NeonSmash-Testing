using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Zeigt eine horizontale Reihe von Boost-Effekt-Icons (Color Vanisher, Dust, Extra Life, Mult 2/3,
// Mult Minus) unter dem Score an -- jedes Icon poppt ein/aus, sobald der jeweilige Mystery-Box-Effekt
// gerade aktiv ist. Ersetzt das vorherige einzelne ExtraLifeIconDisplay durch eine generische Loesung
// fuer mehrere gleichzeitig sichtbare Effekt-Icons.
//
// STACKING: Multiplikator- und Smoke-Effekte koennen mehrfach gleichzeitig aktiv sein (siehe
// MysteryBoxEffectSystem) -- pro zusaetzlicher aktiver Instanz wird hier eine eigene Kopie des
// jeweiligen Icons direkt daneben eingeblendet, statt nur ein einzelnes Icon an/aus zu schalten. Farblos
// und Extra Life koennen sich nicht mit sich selbst ueberschneiden, bleiben also einfache An/Aus-Icons.
//
// WICHTIG: sitzt als EIN zentrales Skript auf dem Reihen-Parent (der immer aktiv bleibt) und schaltet
// nur die einzelnen Icon-Kind-Objekte an/aus -- nicht sich selbst. So laeuft Update() durchgehend
// weiter, egal wie viele Icons gerade sichtbar sind (gleiches Prinzip wie beim alten
// ExtraLifeIconDisplay, nur diesmal fuer mehrere Icons in einer Grid Layout Group gebuendelt, damit sie
// sich beim Ein-/Ausblenden automatisch neu anordnen).
public class BoostEffectIconRow : MonoBehaviour
{
    [System.Serializable]
    private class IconEntry
    {
        public GameObject root;
        [System.NonSerialized] public Image image;
        [System.NonSerialized] public RectTransform rect;
        [System.NonSerialized] public bool shown;
        [System.NonSerialized] public Coroutine routine;
        // Zusaetzliche Icon-Kopien fuer gestapelte Instanzen (Count > 1) -- das Original-"root"-Objekt
        // deckt immer die erste Instanz ab, hier kommen nur die WEITEREN dazu.
        [System.NonSerialized] public List<GameObject> extraClones = new List<GameObject>();
    }

    [Header("Icons (Reihenfolge = Anzeigereihenfolge in der Layout Group)")]
    [SerializeField] private IconEntry colorVanisher;
    [SerializeField] private IconEntry dust;
    [SerializeField] private IconEntry extraLife;
    [SerializeField] private IconEntry mult2;
    [SerializeField] private IconEntry mult3;
    [SerializeField] private IconEntry multMinus;

    [Header("Erscheinen (Pop-In)")]
    [SerializeField] private float appearOvershootScale = 1.3f;
    [SerializeField] private float appearDuration = 0.25f;

    [Header("Verschwinden (kleiner Pop, dann schrumpfen)")]
    [SerializeField] private float disappearPopScale = 1.15f;
    [SerializeField] private float disappearPopDuration = 0.1f;
    [SerializeField] private float disappearShrinkDuration = 0.2f;

    private IconEntry[] _all;

    private void Awake()
    {
        _all = new[] { colorVanisher, dust, extraLife, mult2, mult3, multMinus };
        foreach (var e in _all)
        {
            if (e?.root == null) continue;
            e.image = e.root.GetComponent<Image>();
            e.rect = (RectTransform)e.root.transform;
            e.shown = e.root.activeSelf;
            if (!e.shown) e.root.SetActive(false);
            else e.rect.localScale = Vector3.one;
        }
    }

    private void Update()
    {
        var sys = MysteryBoxEffectSystem.Instance;
        bool hasSystem = sys != null;

        SetDesired(colorVanisher, hasSystem && sys.IsColorlessActive);
        SetDesired(extraLife, hasSystem && sys.HasExtraLifeCharge);

        // Stapelbare Effekte: ein Icon PRO aktiver Instanz, nicht nur ein einzelnes An/Aus-Flag -- bei
        // z.B. zwei gleichzeitig aktiven x2-Boosts sollen auch wirklich zwei x2-Icons zu sehen sein.
        SetDesiredCount(dust, hasSystem ? sys.SmokeCount : 0);
        SetDesiredCount(mult2, hasSystem ? sys.MultiplierX2Count : 0);
        SetDesiredCount(mult3, hasSystem ? sys.MultiplierX3Count : 0);
        SetDesiredCount(multMinus, hasSystem ? sys.MultiplierMinus1Count : 0);
    }

    private void SetDesired(IconEntry e, bool desired)
    {
        if (e?.root == null) return;
        if (desired == e.shown) return;
        e.shown = desired;

        if (e.routine != null) StopCoroutine(e.routine);
        e.routine = StartCoroutine(desired ? Co_PopIn(e) : Co_PopOutThenShrink(e));
    }

    private void SetDesiredCount(IconEntry e, int count)
    {
        if (e?.root == null) return;
        count = Mathf.Max(0, count);

        // Erste Instanz laeuft weiter ueber das normale An/Aus-Icon (Original-Objekt in der Szene).
        SetDesired(e, count > 0);

        // Jede weitere gleichzeitig aktive Instanz bekommt eine eigene Kopie direkt daneben.
        int desiredExtra = count - 1;
        if (desiredExtra < 0) desiredExtra = 0;

        while (e.extraClones.Count < desiredExtra)
        {
            var clone = Instantiate(e.root, e.root.transform.parent);
            clone.name = e.root.name + " (Stack)";
            clone.transform.SetSiblingIndex(e.root.transform.GetSiblingIndex() + e.extraClones.Count + 1);
            clone.SetActive(true);
            ((RectTransform)clone.transform).localScale = Vector3.zero;
            e.extraClones.Add(clone);
            StartCoroutine(Co_PopInClone((RectTransform)clone.transform));
        }
        while (e.extraClones.Count > desiredExtra)
        {
            var clone = e.extraClones[e.extraClones.Count - 1];
            e.extraClones.RemoveAt(e.extraClones.Count - 1);
            if (clone != null) StartCoroutine(Co_PopOutThenDestroy((RectTransform)clone.transform));
        }
    }

    private IEnumerator Co_PopIn(IconEntry e)
    {
        e.root.SetActive(true);
        e.rect.localScale = Vector3.zero;

        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / appearDuration);
            float scale = p < 0.7f
                ? Mathf.Lerp(0f, appearOvershootScale, p / 0.7f)
                : Mathf.Lerp(appearOvershootScale, 1f, (p - 0.7f) / 0.3f);
            e.rect.localScale = Vector3.one * scale;
            yield return null;
        }

        e.rect.localScale = Vector3.one;
        e.routine = null;
    }

    private IEnumerator Co_PopOutThenShrink(IconEntry e)
    {
        Vector3 startScale = e.rect.localScale;
        Vector3 popScale = Vector3.one * disappearPopScale;

        float t = 0f;
        while (t < disappearPopDuration)
        {
            t += Time.unscaledDeltaTime;
            e.rect.localScale = Vector3.Lerp(startScale, popScale, t / disappearPopDuration);
            yield return null;
        }

        t = 0f;
        while (t < disappearShrinkDuration)
        {
            t += Time.unscaledDeltaTime;
            e.rect.localScale = Vector3.Lerp(popScale, Vector3.zero, t / disappearShrinkDuration);
            yield return null;
        }

        e.rect.localScale = Vector3.one;
        e.root.SetActive(false);
        e.routine = null;
    }

    // Varianten von Co_PopIn/Co_PopOutThenShrink fuer dynamisch instanziierte Stapel-Kopien: laufen auf
    // einem reinen RectTransform statt einem IconEntry und zerstoeren das Objekt am Ende, statt es nur
    // zu deaktivieren (die Kopie wird ja nicht wiederverwendet, sondern bei Bedarf neu instanziiert).
    private IEnumerator Co_PopInClone(RectTransform rect)
    {
        float t = 0f;
        while (t < appearDuration)
        {
            if (rect == null) yield break;
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / appearDuration);
            float scale = p < 0.7f
                ? Mathf.Lerp(0f, appearOvershootScale, p / 0.7f)
                : Mathf.Lerp(appearOvershootScale, 1f, (p - 0.7f) / 0.3f);
            rect.localScale = Vector3.one * scale;
            yield return null;
        }
        if (rect != null) rect.localScale = Vector3.one;
    }

    private IEnumerator Co_PopOutThenDestroy(RectTransform rect)
    {
        if (rect == null) yield break;
        Vector3 startScale = rect.localScale;
        Vector3 popScale = Vector3.one * disappearPopScale;

        float t = 0f;
        while (t < disappearPopDuration)
        {
            if (rect == null) yield break;
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(startScale, popScale, t / disappearPopDuration);
            yield return null;
        }

        t = 0f;
        while (t < disappearShrinkDuration)
        {
            if (rect == null) yield break;
            t += Time.unscaledDeltaTime;
            rect.localScale = Vector3.Lerp(popScale, Vector3.zero, t / disappearShrinkDuration);
            yield return null;
        }

        if (rect != null) Destroy(rect.gameObject);
    }
}
