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
    }

    [SerializeField] private FairySlot[] fairies = new FairySlot[3];

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
            flyer.Play(slot.fairyTransform, () => glow?.Flash());
        else
            Destroy(orb);
    }
}
