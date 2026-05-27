using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class DailyRewardManager
{
    const string PrefKeyLastDate = "daily_last_date";
    const string PrefKeyStreak   = "daily_streak";
    const string CloudKeyLast    = "daily_last";
    const string CloudKeyStreak  = "daily_streak";

    // Coins per streak day (Day 1–7, then repeats from Day 1)
    static readonly int[] StreakRewards = { 50, 75, 100, 125, 150, 200, 500 };

    public static event Action<int, int, int> OnRewardClaimed; // coins, streak, dayIndex (1-based)

    public static int  CurrentStreak    => PlayerPrefs.GetInt(PrefKeyStreak, 0);
    public static string LastClaimedDate => PlayerPrefs.GetString(PrefKeyLastDate, "");

    public static bool CanClaimToday =>
        LastClaimedDate != DateTime.UtcNow.ToString("yyyy-MM-dd");

    // Which reward is shown as "today's" (even before claiming)
    public static int TodayRewardAmount
    {
        get
        {
            int nextDay = Mathf.Clamp(CurrentStreak + 1, 1, StreakRewards.Length);
            string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            bool streakContinues = LastClaimedDate == yesterday || CurrentStreak == 0;
            int dayIndex = streakContinues ? nextDay : 1;
            return StreakRewards[dayIndex - 1];
        }
    }

    // Returns coins earned (> 0) or 0 if already claimed today
    public static int ClaimTodayReward()
    {
        if (!CanClaimToday) return 0;

        string today     = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        bool streakContinues = LastClaimedDate == yesterday;
        int newStreak = streakContinues ? CurrentStreak + 1 : 1;
        newStreak = Mathf.Clamp(newStreak, 1, StreakRewards.Length);

        int reward   = StreakRewards[newStreak - 1];
        int dayIndex = newStreak;

        PlayerPrefs.SetString(PrefKeyLastDate, today);
        PlayerPrefs.SetInt(PrefKeyStreak, newStreak);
        PlayerPrefs.Save();

        CoinManager.AddCoins(reward);
        AchievementManager.OnStreakReached(newStreak);
        _ = SaveToCloudAsync(today, newStreak);

        Debug.Log($"[DailyReward] Tag {dayIndex}, Streak {newStreak} → +{reward} Coins");
        OnRewardClaimed?.Invoke(reward, newStreak, dayIndex);
        return reward;
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKeyLast, CloudKeyStreak });

            if (result.TryGetValue(CloudKeyLast, out var dateItem))
            {
                string cloudDate = dateItem.Value.GetAs<string>();
                if (!string.IsNullOrEmpty(cloudDate))
                    PlayerPrefs.SetString(PrefKeyLastDate, cloudDate);
            }

            if (result.TryGetValue(CloudKeyStreak, out var streakItem))
            {
                int cloudStreak = streakItem.Value.GetAs<int>();
                if (cloudStreak > CurrentStreak)
                    PlayerPrefs.SetInt(PrefKeyStreak, cloudStreak);
            }

            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyReward] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    static async Task SaveToCloudAsync(string date, int streak)
    {
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
            {
                { CloudKeyLast,   date   },
                { CloudKeyStreak, streak }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyReward] Cloud Save fehlgeschlagen: " + e.Message);
        }
    }
}
