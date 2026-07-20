using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class DiamondManager
{
    const string PrefKey  = "diamonds_balance";
    const string CloudKey = "diamonds";

    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public static event Action<int> OnDiamondsChanged;

    public static int Balance => PlayerPrefs.GetInt(PrefKey, 0);

    public static void AddDiamonds(int amount)
    {
        if (amount <= 0) return;
        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnDiamondsChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        Debug.Log($"[Diamonds] +{amount} → {newBalance}");
    }

    public static bool TrySpendDiamonds(int amount)
    {
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnDiamondsChanged?.Invoke(newBalance);
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
                    OnDiamondsChanged?.Invoke(cloudBalance);
                    Debug.Log($"[Diamonds] Cloud wiederhergestellt: {cloudBalance}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Diamonds] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            int balance = Balance;
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance } });
            Debug.Log($"[Diamonds] Cloud gespeichert: {balance}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Diamonds] Cloud Save fehlgeschlagen: " + e.Message);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
