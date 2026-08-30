using UnityEngine;

// Unlock-/Freispiel-Tracking für Welten (z.B. Tropical Jungle, Sky Island). Eine Welt wird durch
// Erreichen einer Spieler-Stufe "aufgeschlossen" (siehe PlayerLevelManager/RewardDefinition mit
// Kind=WorldUnlock) und gibt dabei eine feste Anzahl Freispiele. Um die Welt darüber hinaus
// dauerhaft zu spielen, muss sie zusätzlich regulär gekauft werden (siehe ShopItem/ShopInventory
// für den eigentlichen Kauf — dieser Manager kennt nur den Freispiel-Zustand und ob die Welt
// überhaupt schon sichtbar/anspielbar ist).
public static class WorldUnlockManager
{
    const string UnlockedPrefPrefix  = "world_unlocked_";
    const string FreePlaysPrefPrefix = "world_freeplays_";

    public static event System.Action<string> OnWorldUnlocked;
    public static event System.Action<string, int> OnFreePlaysChanged;

    public static bool IsUnlocked(string worldId) =>
        !string.IsNullOrEmpty(worldId) && PlayerPrefs.GetInt(UnlockedPrefPrefix + worldId, 0) == 1;

    public static int GetFreePlaysRemaining(string worldId)
    {
        if (string.IsNullOrEmpty(worldId)) return 0;
        return PlayerPrefs.GetInt(FreePlaysPrefPrefix + worldId, 0);
    }

    /// <summary>Schaltet die Welt frei (idempotent) und schreibt die mitgegebenen Freispiele gut.
    /// Von PlayerLevelManager beim Erreichen eines WorldUnlock-Rewards aufgerufen.</summary>
    public static void Unlock(string worldId, int freePlaysGranted)
    {
        if (string.IsNullOrEmpty(worldId)) return;
        bool wasUnlocked = IsUnlocked(worldId);
        PlayerPrefs.SetInt(UnlockedPrefPrefix + worldId, 1);
        if (freePlaysGranted > 0)
        {
            int newFreePlays = GetFreePlaysRemaining(worldId) + freePlaysGranted;
            PlayerPrefs.SetInt(FreePlaysPrefPrefix + worldId, newFreePlays);
            OnFreePlaysChanged?.Invoke(worldId, newFreePlays);
        }
        PlayerPrefs.Save();
        if (!wasUnlocked) OnWorldUnlocked?.Invoke(worldId);
    }

    /// <summary>Verbraucht ein Freispiel, falls eines übrig ist. Gibt false zurück, wenn keins mehr
    /// da ist (Welt muss dann regulär gekauft/besessen sein, um weiterspielbar zu bleiben).</summary>
    public static bool TryConsumeFreePlay(string worldId)
    {
        int remaining = GetFreePlaysRemaining(worldId);
        if (remaining <= 0) return false;
        int newRemaining = remaining - 1;
        PlayerPrefs.SetInt(FreePlaysPrefPrefix + worldId, newRemaining);
        PlayerPrefs.Save();
        OnFreePlaysChanged?.Invoke(worldId, newRemaining);
        return true;
    }
}
