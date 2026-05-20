#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Services.CloudSave;
using UnityEngine;
using UnityEngine.InputSystem;

public class RewardDebugHelper : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        bool cmd = kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (cmd && kb.eKey.wasPressedThisFrame)
            ResetAll();
    }

    [ContextMenu("Reset — Everything")]
    void ResetAll()
    {
        ResetCoins();
        ResetDailyReward();
        ResetAchievements();
        ResetMissions();
        Debug.Log("[RewardDebug] Alles zurückgesetzt (lokal + Cloud).");
    }

    [ContextMenu("Reset — Coins")]
    async void ResetCoins()
    {
        PlayerPrefs.DeleteKey("coins_balance");
        PlayerPrefs.Save();
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { "coins", 0 } });
        }
        catch { }
        Debug.Log("[RewardDebug] Coins zurückgesetzt.");
    }

    [ContextMenu("Reset — Daily Reward & Streak")]
    async void ResetDailyReward()
    {
        PlayerPrefs.DeleteKey("daily_last_date");
        PlayerPrefs.DeleteKey("daily_streak");
        PlayerPrefs.Save();
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { "daily_last", "" }, { "daily_streak", 0 } });
        }
        catch { }
        Debug.Log("[RewardDebug] Daily Reward & Streak zurückgesetzt.");
    }

    [ContextMenu("Reset — Achievements")]
    async void ResetAchievements()
    {
        PlayerPrefs.DeleteKey("ach_completed");
        PlayerPrefs.DeleteKey("ach_games_total");
        PlayerPrefs.DeleteKey("ach_special_mask");
        PlayerPrefs.DeleteKey("ach_timemode_games");
        PlayerPrefs.Save();
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object>
                {
                    { "ach_completed", "" },
                    { "ach_stats", "{\"gamesTotal\":0,\"specialMask\":0,\"timeModeGames\":0}" }
                });
        }
        catch { }
        Debug.Log("[RewardDebug] Achievements zurückgesetzt.");
    }

    [ContextMenu("Reset — Daily Missions")]
    void ResetMissions()
    {
        PlayerPrefs.DeleteKey("mission_date");
        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.DeleteKey($"mission_{i}_type");
            PlayerPrefs.DeleteKey($"mission_{i}_target");
            PlayerPrefs.DeleteKey($"mission_{i}_reward");
            PlayerPrefs.DeleteKey($"mission_{i}_progress");
            PlayerPrefs.DeleteKey($"mission_{i}_done");
        }
        PlayerPrefs.Save();
        Debug.Log("[RewardDebug] Missions zurückgesetzt (nur lokal — keine Cloud-Daten).");
    }
}
#endif
