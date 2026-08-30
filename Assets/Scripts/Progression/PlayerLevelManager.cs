using System.Collections.Generic;
using UnityEngine;

// Zentrale Stufen-/Fortschrittslogik: berechnet die aktuelle Spieler-Stufe aus der insgesamt
// jemals verdienten Traumenergie (DreamEnergyManager.LifetimeEarned). Eine Stufe ist "erreicht",
// sobald die XP-Schwelle überschritten ist (reine Funktion, kein Speicherzustand nötig) — ihre
// Belohnungen werden aber ERST vergeben, wenn der Spieler im Belohnungsfenster aktiv auf
// "COLLECT" klickt (siehe TryCollect). "Erreicht" und "abgeholt" sind also zwei getrennte
// Zustände; nur "abgeholt" wird persistiert.
public static class PlayerLevelManager
{
    const string ResourcePath        = "LevelRewardTrack";
    const string CatalogueResourcePath = "RewardCatalogue";
    const string CollectedPrefKey    = "player_level_collected"; // komma-getrennte Stufen-Nummern

    static LevelRewardTrack _track;
    static LevelRewardTrack Track => _track != null ? _track : (_track = Resources.Load<LevelRewardTrack>(ResourcePath));

    static RewardCatalogue _catalogue;
    static RewardCatalogue Catalogue => _catalogue != null ? _catalogue : (_catalogue = Resources.Load<RewardCatalogue>(CatalogueResourcePath));

    /// <summary>Feuert, wenn eine Stufe erfolgreich abgeholt wurde (siehe TryCollect).</summary>
    public static event System.Action<LevelRewardTrack.LevelTier> OnTierCollected;

    static HashSet<int> _collectedLevels;
    static HashSet<int> CollectedLevels
    {
        get
        {
            if (_collectedLevels != null) return _collectedLevels;
            _collectedLevels = new HashSet<int>();
            string raw = PlayerPrefs.GetString(CollectedPrefKey, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part, out int level)) _collectedLevels.Add(level);
            return _collectedLevels;
        }
    }

    /// <summary>Aktuelle Stufe (1 = Startzustand, wenn noch keine Schwelle erreicht wurde) — rein
    /// XP-basiert, unabhängig davon, ob Belohnungen schon abgeholt wurden.</summary>
    public static int CurrentLevel
    {
        get
        {
            var (_, currentTierIndex, _) = Evaluate(DreamEnergyManager.LifetimeEarned);
            var tiers = SortedTiers();
            return currentTierIndex >= 0 ? tiers[currentTierIndex].level : 1;
        }
    }

    static LevelRewardTrack.LevelTier[] SortedTiers() =>
        Track != null ? Track.SortedTiers() : new LevelRewardTrack.LevelTier[0];

    /// <summary>Fortschritt innerhalb der aktuellen Stufe (0..1) sowie Index der aktuellen/nächsten
    /// Stufe im sortierten Tier-Array (-1 = noch keine erreicht bzw. Track komplett abgeschlossen).</summary>
    public static (float fill, int currentTierIndex, int nextTierIndex) Evaluate(int lifetimeXp)
    {
        var tiers = SortedTiers();
        int currentTierIndex = -1;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].xpThreshold <= lifetimeXp) currentTierIndex = i;
            else break; // aufsteigend sortiert — ab hier ist auch keine weitere Schwelle erreicht
        }

        int nextTierIndex = currentTierIndex + 1;
        if (nextTierIndex >= tiers.Length) return (1f, currentTierIndex, -1);

        int segStart = currentTierIndex >= 0 ? tiers[currentTierIndex].xpThreshold : 0;
        int segEnd   = tiers[nextTierIndex].xpThreshold;
        float fill = segEnd > segStart ? Mathf.Clamp01((float)(lifetimeXp - segStart) / (segEnd - segStart)) : 1f;
        return (fill, currentTierIndex, nextTierIndex);
    }

    public static LevelRewardTrack.LevelTier GetTierByLevel(int level)
    {
        foreach (var tier in SortedTiers())
            if (tier.level == level) return tier;
        return null;
    }

    public static bool IsReached(LevelRewardTrack.LevelTier tier) =>
        tier != null && DreamEnergyManager.LifetimeEarned >= tier.xpThreshold;

    public static bool IsCollected(LevelRewardTrack.LevelTier tier) =>
        tier != null && CollectedLevels.Contains(tier.level);

    /// <summary>Vergibt die Belohnungen dieser Stufe (idempotent), falls sie erreicht und noch
    /// nicht abgeholt ist. Auf den "COLLECT"-Button in RewardTierCardUI verdrahtet. Gibt false
    /// zurück, wenn die Stufe noch nicht erreicht oder schon abgeholt wurde.</summary>
    public static bool TryCollect(LevelRewardTrack.LevelTier tier)
    {
        if (!IsReached(tier) || IsCollected(tier)) return false;

        GrantTierRewards(tier);
        CollectedLevels.Add(tier.level);
        PlayerPrefs.SetString(CollectedPrefKey, string.Join(",", CollectedLevels));
        PlayerPrefs.Save();

        OnTierCollected?.Invoke(tier);
        return true;
    }

    static void GrantTierRewards(LevelRewardTrack.LevelTier tier)
    {
        if (tier.rewards == null) return;
        foreach (var reward in tier.rewards)
        {
            if (reward == null) continue;
            switch (reward.kind)
            {
                case RewardKind.DiamondSplinters:
                    DiamondSplinterManager.AddSplinters(reward.amount);
                    break;
                case RewardKind.Diamonds:
                    DiamondManager.AddDiamonds(reward.amount);
                    break;
                case RewardKind.Sticker:
                    // Sticker sind immer zufällig (gewichtet nach Seltenheit) — die konkrete
                    // RewardDefinition in tier.rewards dient hier nur als "es gibt einen Sticker"-
                    // Markierung, nicht als fixe Auswahl.
                    StickerManager.GrantRandom(Catalogue);
                    break;
                case RewardKind.Title:
                    TitleManager.Unlock(reward.rewardId);
                    break;
                case RewardKind.Booster:
                    BoosterInventoryManager.Add(reward.rewardId, reward.amount > 0 ? reward.amount : 1);
                    break;
                case RewardKind.WorldUnlock:
                    WorldUnlockManager.Unlock(reward.worldId, reward.freePlaysGranted);
                    break;
            }
        }
    }
}
