using UnityEngine;

// Kuratierte Liste von Spieler-Stufen für das Fortschritt-/XP-/Belohnungssystem (siehe
// PlayerLevelManager): bei Erreichen des kumulierten Traumenergie-Schwellenwerts (siehe
// DreamEnergyManager.LifetimeEarned) schaltet die zugehörige Stufe frei und ihre Belohnungen
// werden vergeben. Werte/Belohnungen sind reine Design-/Balancing-Entscheidung, im Inspector
// gepflegt — neue Stufe hinzufügen erfordert keine Code-Änderung (Baukastensystem).
[CreateAssetMenu(fileName = "LevelRewardTrack", menuName = "NeonSmash/Level Reward Track")]
public class LevelRewardTrack : ScriptableObject
{
    [System.Serializable]
    public class LevelTier
    {
        [Tooltip("Stufennummer (z.B. 1, 2, 3, ...) — Stufe 1 hat xpThreshold 0 und ist damit sofort collectbar.")]
        public int level;
        [Tooltip("Kumulierte Traumenergie (insgesamt jemals verdient), ab der diese Stufe erreicht ist.")]
        public int xpThreshold;
        public RewardDefinition[] rewards;
    }

    [Tooltip("Muss nicht in aufsteigender Reihenfolge gepflegt werden — SortedTiers() sortiert selbst.")]
    [SerializeField] private LevelTier[] tiers;

    /// <summary>Kopie der Tiers, aufsteigend nach level sortiert.</summary>
    public LevelTier[] SortedTiers()
    {
        var sorted = (LevelTier[])(tiers ?? new LevelTier[0]).Clone();
        System.Array.Sort(sorted, (a, b) => a.level.CompareTo(b.level));
        return sorted;
    }
}
