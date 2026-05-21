using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class CoinManager
{
    const string PrefKey  = "coins_balance";
    const string CloudKey = "coins";

    // Ensures cloud saves are sequential — prevents out-of-order writes
    // when multiple AddCoins calls fire rapidly (e.g. score + mission + achievement rewards).
    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public static event Action<int> OnCoinsChanged;

    public static int Balance => PlayerPrefs.GetInt(PrefKey, 0);

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        Debug.Log($"[Coins] +{amount} → {newBalance}");
    }

    public static bool TrySpendCoins(int amount)
    {
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newBalance);
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
                    OnCoinsChanged?.Invoke(cloudBalance);
                    Debug.Log($"[Coins] Cloud wiederhergestellt: {cloudBalance}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Coins] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    // Reads the current balance at the time the lock is acquired, so that even if
    // multiple saves are queued, the last one always writes the most recent value.
    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            int balance = Balance;
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance } });
            Debug.Log($"[Coins] Cloud gespeichert: {balance}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Coins] Cloud Save fehlgeschlagen: " + e.Message);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
