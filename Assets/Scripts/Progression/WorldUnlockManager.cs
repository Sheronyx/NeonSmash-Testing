using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
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
    // Die Welt-Stände liegen pro Welt in eigenen Schlüsseln. Zum Hochladen brauchen wir eine
    // Liste, WELCHE Welten es ueberhaupt gibt -- die fuehrt dieser Schluessel mit.
    const string KnownWorldsPrefKey  = "world_known_ids";
    const string CloudKey            = "world_unlocks";
    const string CloudDirtyPrefKey   = "world_unlocks_cloud_dirty";

    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

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
        MerkeWelt(worldId);
        PlayerPrefs.SetInt(UnlockedPrefPrefix + worldId, 1);
        if (freePlaysGranted > 0)
        {
            int newFreePlays = GetFreePlaysRemaining(worldId) + freePlaysGranted;
            PlayerPrefs.SetInt(FreePlaysPrefPrefix + worldId, newFreePlays);
            OnFreePlaysChanged?.Invoke(worldId, newFreePlays);
        }
        PlayerPrefs.Save();
        _ = SaveToCloudAsync();
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
        _ = SaveToCloudAsync();
        OnFreePlaysChanged?.Invoke(worldId, newRemaining);
        return true;
    }

    // ── Cloud Save ────────────────────────────────────────────────────────────
    // Eine freigeschaltete Welt ist aus Kundensicht ein dauerhafter Inhalt -- auch wenn sie mit
    // Diamanten bezahlt wurde, die formal ein Verbrauchsgut sind. Sie muss deshalb am Konto
    // haengen und eine Neuinstallation ueberstehen.

    [Serializable] class WeltStand { public string id; public bool unlocked; public int freePlays; }
    [Serializable] class Huelle    { public WeltStand[] welten; }

    static List<string> BekannteWelten()
    {
        var liste = new List<string>();
        string roh = PlayerPrefs.GetString(KnownWorldsPrefKey, "");
        if (string.IsNullOrEmpty(roh)) return liste;
        foreach (var id in roh.Split(','))
            if (!string.IsNullOrEmpty(id)) liste.Add(id);
        return liste;
    }

    static void MerkeWelt(string worldId)
    {
        var liste = BekannteWelten();
        if (liste.Contains(worldId)) return;
        liste.Add(worldId);
        PlayerPrefs.SetString(KnownWorldsPrefKey, string.Join(",", liste));
        PlayerPrefs.Save();
    }

    /// <summary>Beim App-Start aufzurufen, nachdem die Anmeldung steht.</summary>
    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKey });

            if (result.TryGetValue(CloudKey, out var item))
            {
                var huelle = JsonUtility.FromJson<Huelle>(item.Value.GetAs<string>());
                int ergaenzt = 0;

                if (huelle?.welten != null)
                    foreach (var w in huelle.welten)
                    {
                        if (w == null || string.IsNullOrEmpty(w.id)) continue;
                        MerkeWelt(w.id);

                        // Freischaltung: nie zuruecknehmen, nur ergaenzen.
                        if (w.unlocked && !IsUnlocked(w.id))
                        {
                            PlayerPrefs.SetInt(UnlockedPrefPrefix + w.id, 1);
                            ergaenzt++;
                            OnWorldUnlocked?.Invoke(w.id);
                        }

                        // Freispiele: nur uebernehmen, wenn lokal noch gar nichts bekannt ist.
                        // Sonst gewinnt der lokale Stand -- er kennt die seither verbrauchten
                        // Spiele, die Cloud womoeglich noch nicht.
                        if (!PlayerPrefs.HasKey(FreePlaysPrefPrefix + w.id) && w.freePlays > 0)
                        {
                            PlayerPrefs.SetInt(FreePlaysPrefPrefix + w.id, w.freePlays);
                            OnFreePlaysChanged?.Invoke(w.id, w.freePlays);
                        }
                    }

                PlayerPrefs.Save();
                if (ergaenzt > 0) Debug.Log($"[WorldUnlock] Aus der Cloud wiederhergestellt: {ergaenzt} Welten.");
            }
            else if (BekannteWelten().Count > 0)
            {
                await SaveToCloudAsync();   // erster Start nach dem Update: lokalen Stand sichern
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WorldUnlock] Cloud Load fehlgeschlagen: " + e.Message);
        }

        await RetryPendingCloudSaveIfNeeded();
    }

    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            var welten = new List<WeltStand>();
            foreach (var id in BekannteWelten())
                welten.Add(new WeltStand
                {
                    id        = id,
                    unlocked  = IsUnlocked(id),
                    freePlays = GetFreePlaysRemaining(id)
                });

            string json = JsonUtility.ToJson(new Huelle { welten = welten.ToArray() });
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, json } });
            Debug.Log($"[WorldUnlock] Cloud gespeichert: {welten.Count} Welten.");
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 0);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WorldUnlock] Cloud Save fehlgeschlagen: " + e.Message);
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 1);
            PlayerPrefs.Save();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public static async Task RetryPendingCloudSaveIfNeeded()
    {
        if (PlayerPrefs.GetInt(CloudDirtyPrefKey, 0) == 0) return;
        Debug.Log("[WorldUnlock] Offener Cloud-Save aus vorherigem Fehlversuch wird nachgeholt.");
        await SaveToCloudAsync();
    }
}
