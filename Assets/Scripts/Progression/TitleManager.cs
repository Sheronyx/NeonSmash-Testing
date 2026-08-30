using System.Collections.Generic;
using UnityEngine;

// Besitz- und Auswahl-Tracking für Titel (per Stufenaufstieg freigeschaltet, siehe
// PlayerLevelManager). Gleiches Owned-Set-Muster wie ShopInventory/StickerManager, zusätzlich
// ein "aktuell ausgewählter Titel" (wird unter dem Spielernamen angezeigt).
public static class TitleManager
{
    const string PrefKey        = "titles_owned";
    const string SelectedPrefKey = "titles_selected";

    public static event System.Action<string> OnTitleUnlocked;
    public static event System.Action<string> OnSelectedTitleChanged;

    static HashSet<string> _owned;

    static HashSet<string> Owned
    {
        get
        {
            if (_owned != null) return _owned;
            _owned = new HashSet<string>();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var id in raw.Split(','))
                    if (!string.IsNullOrEmpty(id)) _owned.Add(id);
            return _owned;
        }
    }

    public static bool IsOwned(string rewardId) => Owned.Contains(rewardId);

    public static IReadOnlyCollection<string> OwnedIds => Owned;

    /// <summary>Aktuell für die Anzeige ausgewählter Titel (rewardId der RewardDefinition), oder
    /// leer, wenn noch keiner ausgewählt/besessen ist.</summary>
    public static string SelectedTitleId => PlayerPrefs.GetString(SelectedPrefKey, "");

    public static void Unlock(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId) || IsOwned(rewardId)) return;
        Owned.Add(rewardId);
        Save();
        OnTitleUnlocked?.Invoke(rewardId);

        // Erster besessener Titel wird automatisch ausgewählt, damit unter dem Namen nicht
        // dauerhaft "kein Titel" steht, bis der Spieler manuell ins Titelbuch geht.
        if (string.IsNullOrEmpty(SelectedTitleId))
            SelectTitle(rewardId);
    }

    public static void SelectTitle(string rewardId)
    {
        if (!string.IsNullOrEmpty(rewardId) && !IsOwned(rewardId)) return;
        PlayerPrefs.SetString(SelectedPrefKey, rewardId ?? "");
        PlayerPrefs.Save();
        OnSelectedTitleChanged?.Invoke(rewardId);
    }

    static void Save()
    {
        PlayerPrefs.SetString(PrefKey, string.Join(",", Owned));
        PlayerPrefs.Save();
    }
}
