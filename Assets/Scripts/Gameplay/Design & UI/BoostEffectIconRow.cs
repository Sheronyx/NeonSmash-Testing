using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Zeigt eine horizontale Reihe von Boost-Effekt-Icons (Color Vanisher, Dust, Extra Life, Mult 2/3,
// Mult Minus) unter dem Score an -- jedes Icon poppt ein/aus, sobald der jeweilige Mystery-Box-Effekt
// gerade aktiv ist. Ersetzt das vorherige einzelne ExtraLifeIconDisplay durch eine generische Loesung
// fuer mehrere gleichzeitig sichtbare Effekt-Icons.
//
// WICHTIG: sitzt als EIN zentrales Skript auf dem Reihen-Parent (der immer aktiv bleibt) und schaltet
// nur die einzelnen Icon-Kind-Objekte an/aus -- nicht sich selbst. So laeuft Update() durchgehend
// weiter, egal wie viele Icons gerade sichtbar sind (gleiches Prinzip wie beim alten
// ExtraLifeIconDisplay, nur diesmal fuer mehrere Icons in einer HorizontalLayoutGroup gebuendelt,
// damit sie sich beim Ein-/Ausblenden automatisch neu anordnen).
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
        SetDesired(dust, hasSystem && sys.IsSmokeActive);
        SetDesired(extraLife, hasSystem && sys.HasExtraLifeCharge);
        SetDesired(mult2, hasSystem && sys.CurrentScoreMultiplier == 2);
        SetDesired(mult3, hasSystem && sys.CurrentScoreMultiplier == 3);
        SetDesired(multMinus, hasSystem && sys.CurrentScoreMultiplier == -1);
    }

    private void SetDesired(IconEntry e, bool desired)
    {
        if (e?.root == null) return;
        if (desired == e.shown) return;
        e.shown = desired;

        if (e.routine != null) StopCoroutine(e.routine);
        e.routine = StartCoroutine(desired ? Co_PopIn(e) : Co_PopOutThenShrink(e));
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
}
