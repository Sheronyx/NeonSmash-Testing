using UnityEngine;

// Kuratierte Liste von Dream-Energy-Meilensteinen für die Fortschrittsleiste im Hauptmenü
// (siehe DreamEnergyProgressUI): bei Erreichen des Schwellenwerts (insgesamt jemals verdiente
// Dream Energy, siehe DreamEnergyManager.LifetimeEarned) wird das zugehörige Shop-Item
// automatisch freigeschaltet. Die Werte selbst sind reine Design-/Balancing-Entscheidung und
// werden im Inspector gepflegt, nicht hier im Code.
[CreateAssetMenu(fileName = "DreamEnergyRewardTrack", menuName = "NeonSmash/Dream Energy Reward Track")]
public class DreamEnergyRewardTrack : ScriptableObject
{
    [System.Serializable]
    public class Tier
    {
        [Tooltip("Kumulierte Dream Energy (insgesamt jemals verdient), ab der dieses Item freigeschaltet wird.")]
        public int threshold;
        public ShopItem item;
    }

    [Tooltip("Muss nicht in aufsteigender Reihenfolge gepflegt werden — SortedTiers() sortiert selbst.")]
    [SerializeField] private Tier[] tiers;

    /// <summary>Kopie der Tiers, aufsteigend nach threshold sortiert.</summary>
    public Tier[] SortedTiers()
    {
        var sorted = (Tier[])(tiers ?? new Tier[0]).Clone();
        System.Array.Sort(sorted, (a, b) => a.threshold.CompareTo(b.threshold));
        return sorted;
    }
}
