using UnityEngine;

// Explizite Liste aller Sticker-/Titel-RewardDefinitions fürs Sticker-Album bzw. Titelbuch —
// gleiches Muster wie ShopCatalogue: neue Sticker/Titel per Inspector eintragen, kein Code nötig.
// (Die LevelRewardTrack-Tiers selbst reichen dafür nicht aus, weil ein Sticker/Titel dort nur an
// GENAU der Stufe auftaucht, an der er freigeschaltet wird — das Album muss aber ALLE kennen,
// auch noch nicht erreichte, um sie gesperrt anzuzeigen.)
[CreateAssetMenu(fileName = "RewardCatalogue", menuName = "NeonSmash/Reward Catalogue")]
public class RewardCatalogue : ScriptableObject
{
    public RewardDefinition[] allStickers;
    public RewardDefinition[] allTitles;
}
