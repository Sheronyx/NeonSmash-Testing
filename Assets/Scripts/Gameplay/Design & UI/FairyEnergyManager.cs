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

    private void Awake() => Instance = this;

    public void SpawnEnergyOrb(PointColor color, Vector3 startPos)
    {
        FairySlot slot = null;
        foreach (var f in fairies)
        {
            if (f.color == color) { slot = f; break; }
        }
        if (slot == null || slot.energyOrbPrefab == null || slot.fairyTransform == null) return;

        var orb   = Instantiate(slot.energyOrbPrefab, startPos, Quaternion.identity);
        var flyer = orb.GetComponent<EnergyOrb>();
        var glow  = slot.glowFlash;

        if (flyer != null)
            flyer.Play(slot.fairyTransform, () =>
            {
                glow?.Flash();
                TriggerGetEnergy(slot);
            });
        else
            Destroy(orb);
    }

    // Stoesst die Arm-Animation auf der maskierten Animator-Ebene an. Der Idle-Flug
    // auf dem Base Layer laeuft dabei ungestoert weiter, nur die Arme werden ueberschrieben.
    private static void TriggerGetEnergy(FairySlot slot)
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

        foreach (var p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == GetEnergyTrigger)
            {
                anim.SetTrigger(GetEnergyTrigger);
                return;
            }
        }
    }
}
