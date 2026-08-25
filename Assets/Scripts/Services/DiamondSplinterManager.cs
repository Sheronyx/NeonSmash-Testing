using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class DiamondSplinterManager
{
    const string PrefKey  = "diamond_splinters_balance";
    const string CloudKey = "diamond_splinters";
    const string CloudDirtyPrefKey = "diamond_splinters_cloud_dirty";

    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public static event Action<int> OnSplintersChanged;

    public static int Balance => PlayerPrefs.GetInt(PrefKey, 0);

    public static void AddSplinters(int amount)
    {
        if (amount <= 0) return;
        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnSplintersChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        Debug.Log($"[DiamondSplinters] +{amount} → {newBalance}");
    }

    public static bool TrySpendSplinters(int amount)
    {
        if (amount <= 0) return false;
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnSplintersChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        return true;
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKey });

            if (result.TryGetValue(CloudKey, out var item))
            {
                int cloudBalance = item.Value.GetAs<int>();
                if (cloudBalance > Balance)
                {
                    PlayerPrefs.SetInt(PrefKey, cloudBalance);
                    PlayerPrefs.Save();
                    OnSplintersChanged?.Invoke(cloudBalance);
                    Debug.Log($"[DiamondSplinters] Cloud wiederhergestellt: {cloudBalance}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DiamondSplinters] Cloud Load fehlgeschlagen: " + e.Message);
        }

        await RetryPendingCloudSaveIfNeeded();
    }

    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            int balance = Balance;
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance } });
            Debug.Log($"[DiamondSplinters] Cloud gespeichert: {balance}");
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 0);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DiamondSplinters] Cloud Save fehlgeschlagen: " + e.Message);
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 1);
            PlayerPrefs.Save();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Siehe DreamEnergyManager.RetryPendingCloudSaveIfNeeded für die Begründung — gleiches Muster,
    // von LoadFromCloudAsync (App-Resume) aufgerufen.
    public static async Task RetryPendingCloudSaveIfNeeded()
    {
        if (PlayerPrefs.GetInt(CloudDirtyPrefKey, 0) == 0) return;
        Debug.Log("[DiamondSplinters] Offener Cloud-Save aus vorherigem Fehlversuch wird nachgeholt.");
        await SaveToCloudAsync();
    }
}
