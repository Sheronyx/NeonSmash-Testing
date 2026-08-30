using UnityEngine;

// Verbrauchbares Booster-Inventar: pro Booster-Typ (rewardId der RewardDefinition, Kind=Booster)
// eine Stückzahl, gedeckelt bei MaxPerBooster. Wird durch Level-Belohnungen (siehe
// PlayerLevelManager) und später ggf. weitere Quellen (Shop, Tasks) befüllt; beim Einsatz im
// Spiel wird ein Stück verbraucht (Consume). Ein Booster-Typ selbst wird weiterhin über
// BoostDefinition/BoostType beschrieben — dieser Manager zählt nur, wie viele man davon hat.
public static class BoosterInventoryManager
{
    const string PrefPrefix = "booster_count_";
    public const int MaxPerBooster = 1000;

    public static event System.Action<string, int> OnCountChanged; // (rewardId, newCount)

    public static int GetCount(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return 0;
        return PlayerPrefs.GetInt(PrefPrefix + rewardId, 0);
    }

    public static void Add(string rewardId, int amount)
    {
        if (string.IsNullOrEmpty(rewardId) || amount <= 0) return;
        int newCount = Mathf.Min(GetCount(rewardId) + amount, MaxPerBooster);
        PlayerPrefs.SetInt(PrefPrefix + rewardId, newCount);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(rewardId, newCount);
    }

    /// <summary>Verbraucht ein Stück des Boosters (z.B. bei Einsatz zu Session-Beginn). Gibt false
    /// zurück, wenn keiner mehr übrig ist.</summary>
    public static bool TryConsume(string rewardId)
    {
        int count = GetCount(rewardId);
        if (count <= 0) return false;
        int newCount = count - 1;
        PlayerPrefs.SetInt(PrefPrefix + rewardId, newCount);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(rewardId, newCount);
        return true;
    }
}
