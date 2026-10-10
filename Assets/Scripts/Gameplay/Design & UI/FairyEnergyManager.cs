using System;
using UnityEngine;

// Zentrale Anlaufstelle: beim Zerstören eines Farbelements wird hier (parallel zur VFX-Explosion)
// eine Energiekugel Richtung der passenden Fairy losgeschickt; bei Ankunft blitzt die Fairy kurz
// auf (FairyGlowFlash). Aufgerufen von MixedPointSpawner.HandlePointHit.
public class FairyEnergyManager : MonoBehaviour
{
    public static FairyEnergyManager Instance { get; private set; }

    [Serializable]
    private class FairySlot
    {
        public PointColor color;
        public GameObject energyOrbPrefab;
        public Transform fairyTransform;
        public FairyGlowFlash glowFlash;

        [NonSerialized] public Animator animator;
        [NonSerialized] public bool animatorGesucht;
    }

    [SerializeField] private FairySlot[] fairies = new FairySlot[3];

    private const string GetEnergyTrigger = "GetEnergy";
    private const string GetEnergySpeedParam = "GetEnergySpeed";

    [Tooltip("Tempo-Faktor fuer die GetEnergy-Animation WAEHREND eines Special Modes. Dort prasseln " +
             "die Treffer deutlich schneller -- mit 1.0 wuerde ein Schlag direkt am naechsten haengen. " +
             "Wirkt als Multiplikator auf die Geschwindigkeit des Animator-Zustands.")]
    [SerializeField] private float specialModeAnimSpeed = 1.6f;

    private void Awake() => Instance = this;

    /// <summary>Spielt nur die GetEnergy-Animation der zur Farbe passenden Fee, ohne Energiekugel.
    /// Fuer Special-Mode-Treffer: dort entstehen keine Kugeln, die Fee soll aber mitreagieren.</summary>
    public void PlayGetEnergy(PointColor color)
    {
        foreach (var f in fairies)
            if (f != null && f.color == color) { TriggerGetEnergy(f); return; }
    }

    public void SpawnEnergyOrb(PointColor color, Vector3 startPos)
    {
        FairySlot slot = null;
        foreach (var f in fairies)
        {
            if (f.color == color) { slot = f; break; }
        }
        if (slot == null) return;

        // Die Fee reagiert SOFORT beim Zerstoeren des Elements, nicht erst wenn die Energiekugel
        // bei ihr ankommt -- sonst haengt der Faustschlag spuerbar hinter dem Treffer her.
        TriggerGetEnergy(slot);

        if (slot.energyOrbPrefab == null || slot.fairyTransform == null) return;

        var orb   = Instantiate(slot.energyOrbPrefab, startPos, Quaternion.identity);
        var flyer = orb.GetComponent<EnergyOrb>();
        var glow  = slot.glowFlash;

        if (flyer != null)
            // Das Aufleuchten bleibt am Ankunftszeitpunkt: DA kommt die Energie tatsaechlich an.
            flyer.Play(slot.fairyTransform, () => glow?.Flash());
        else
            Destroy(orb);
    }

    // Stoesst die Arm-Animation auf der maskierten Animator-Ebene an. Der Idle-Flug
    // auf dem Base Layer laeuft dabei ungestoert weiter, nur die Arme werden ueberschrieben.
    private void TriggerGetEnergy(FairySlot slot)
    {
        if (!slot.animatorGesucht)
        {
            slot.animatorGesucht = true;
            if (slot.glowFlash != null)
                slot.animator = slot.glowFlash.GetComponentInParent<Animator>();
            if (slot.animator == null && slot.fairyTransform != null)
                slot.animator = slot.fairyTransform.GetComponentInChildren<Animator>();
        }

        var anim = slot.animator;
        if (anim == null || anim.runtimeAnimatorController == null) return;

        bool hatTrigger = false, hatTempo = false;
        foreach (var p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == GetEnergyTrigger)
                hatTrigger = true;
            else if (p.type == AnimatorControllerParameterType.Float && p.name == GetEnergySpeedParam)
                hatTempo = true;
        }
        if (!hatTrigger) return;   // Fee ohne eigene GetEnergy-Animation (Gruen/Blau)

        // Im Special Mode schneller abspielen. Bewusst bei JEDEM Ausloesen gesetzt statt per
        // Start/Ende-Event: so stimmt der Wert auch, wenn ein Modus abbricht oder die Fee erst
        // mitten im Modus das erste Mal reagiert.
        if (hatTempo)
        {
            bool imSpecialMode = SpecialModeManager.Instance != null
                              && SpecialModeManager.Instance.IsModeActive;
            anim.SetFloat(GetEnergySpeedParam, imSpecialMode ? specialModeAnimSpeed : 1f);
        }

        anim.SetTrigger(GetEnergyTrigger);
    }
}
