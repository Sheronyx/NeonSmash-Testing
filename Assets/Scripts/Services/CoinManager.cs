using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class CoinManager
{
    const string PrefKey  = "coins_balance";
    const string CloudKey = "coins";

    public static event Action<int> OnCoinsChanged;

    public static int Balance => PlayerPrefs.GetInt(PrefKey, 0);

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync(newBalance);
        Debug.Log($"[Coins] +{amount} → {newBalance}");
    }

    public static bool TrySpendCoins(int amount)
    {
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync(newBalance);
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

    static async Task SaveToCloudAsync(int balance)
    {
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance } });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Coins] Cloud Save fehlgeschlagen: " + e.Message);
        }
    }
}
