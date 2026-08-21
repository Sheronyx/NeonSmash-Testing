using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class DailyRewardManager
{
    const string PrefKeyLastDate = "daily_last_date";
    const string PrefKeyStreak   = "daily_streak";
    const string PrefKeyFreeze   = "daily_freeze_charges";
    const string CloudKeyLast    = "daily_last";
    const string CloudKeyStreak  = "daily_streak";
    const string CloudKeyFreeze  = "daily_freeze_charges";

    // Dream Energy per streak day (Day 1–7, then repeats from Day 1)
    static readonly int[] StreakRewards = { 50, 75, 100, 125, 150, 200, 500 };

    public static event Action<int, int, int> OnRewardClaimed; // dreamEnergy, streak, dayIndex (1-based)
    public static event Action<int> OnFreezeChargesChanged;    // neue Anzahl Freeze-Charges
    public static event Action OnStreakFrozen;                 // ein verpasster Tag wurde per Freeze-Charge abgefedert

    public static int  CurrentStreak    => PlayerPrefs.GetInt(PrefKeyStreak, 0);
    public static string LastClaimedDate => PlayerPrefs.GetString(PrefKeyLastDate, "");

    // Anzahl verfügbarer Streak-Freeze-Charges (z.B. per Rewarded-Ad verdient).
    // Ein verpasster Tag verbraucht automatisch eine Charge statt den Streak zu resetten.
    public static int FreezeCharges => PlayerPrefs.GetInt(PrefKeyFreeze, 0);

    public static void AddFreezeCharge(int amount = 1)
    {
        if (amount <= 0) return;
        int newTotal = FreezeCharges + amount;
        PlayerPrefs.SetInt(PrefKeyFreeze, newTotal);
        PlayerPrefs.Save();
        OnFreezeChargesChanged?.Invoke(newTotal);
    }

    public static bool CanClaimToday =>
        LastClaimedDate != DateTime.UtcNow.ToString("yyyy-MM-dd");

    // Maps a raw (unclamped, ever-growing) streak count onto the 7-day reward
    // cycle, e.g. streak 8 -> tier 1, streak 14 -> tier 7, streak 15 -> tier 1.
    // The raw streak itself (PlayerPrefs/Cloud) is never wrapped, so it keeps
    // working as a monotonically increasing counter for AchievementManager.
    static int RewardTierIndex(int rawStreak) =>
        ((Mathf.Max(rawStreak, 1) - 1) % StreakRewards.Length) + 1;

    // Which reward is shown as "today's" (even before claiming)
    public static int TodayRewardAmount
    {
        get
        {
            int nextDay = CurrentStreak + 1;
            string yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            bool streakContinues = LastClaimedDate == yesterday || CurrentStreak == 0;
            int rawDay = streakContinues ? nextDay : 1;
            return StreakRewards[RewardTierIndex(rawDay) - 1];
        }
    }

    // Returns Dream Energy earned (> 0) or 0 if already claimed today
    public static int ClaimTodayReward()
    {
        if (!CanClaimToday) return 0;

        string today       = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string yesterday   = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        string twoDaysAgo  = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd");

        bool streakContinues = LastClaimedDate == yesterday;

        // Genau ein Tag verpasst + Freeze-Charge vorhanden -> Charge verbrauchen statt Streak zu resetten.
        // Deckt bewusst nur einen einzelnen verpassten Tag ab, keine längeren Lücken.
        bool useFreeze = !streakContinues && LastClaimedDate == twoDaysAgo && FreezeCharges > 0;
        if (useFreeze)
        {
            PlayerPrefs.SetInt(PrefKeyFreeze, FreezeCharges - 1);
            streakContinues = true;
        }

        int newStreak = streakContinues ? CurrentStreak + 1 : 1;

        int dayIndex = RewardTierIndex(newStreak);
        int reward   = StreakRewards[dayIndex - 1];

        PlayerPrefs.SetString(PrefKeyLastDate, today);
        PlayerPrefs.SetInt(PrefKeyStreak, newStreak);
        PlayerPrefs.Save();

        DreamEnergyManager.AddDreamEnergy(reward);
        AchievementManager.OnStreakReached(newStreak);
        _ = SaveToCloudAsync(today, newStreak, FreezeCharges);

        Debug.Log($"[DailyReward] Tag {dayIndex}, Streak {newStreak} → +{reward} Dream Energy" + (useFreeze ? " (Freeze-Charge verbraucht)" : ""));
        OnRewardClaimed?.Invoke(reward, newStreak, dayIndex);
        if (useFreeze)
        {
            OnFreezeChargesChanged?.Invoke(FreezeCharges);
            OnStreakFrozen?.Invoke();
        }
        return reward;
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKeyLast, CloudKeyStreak, CloudKeyFreeze });

            if (result.TryGetValue(CloudKeyLast, out var dateItem))
            {
                string cloudDate = dateItem.Value.GetAs<string>();
                // Nur übernehmen, wenn das Cloud-Datum neuer als das lokale ist -
                // sonst kann ein veralteter/verzögerter Cloud-Load einen frischeren
                // lokalen Claim zurücksetzen und den heutigen Reward erneut freigeben.
                if (!string.IsNullOrEmpty(cloudDate)
                    && DateTime.TryParse(cloudDate, out var cloudDt)
                    && (string.IsNullOrEmpty(LastClaimedDate)
                        || !DateTime.TryParse(LastClaimedDate, out var localDt)
                        || cloudDt > localDt))
                {
                    PlayerPrefs.SetString(PrefKeyLastDate, cloudDate);
                }
            }

            if (result.TryGetValue(CloudKeyStreak, out var streakItem))
            {
                int cloudStreak = streakItem.Value.GetAs<int>();
                if (cloudStreak > CurrentStreak)
                    PlayerPrefs.SetInt(PrefKeyStreak, cloudStreak);
            }

            if (result.TryGetValue(CloudKeyFreeze, out var freezeItem))
            {
                int cloudFreeze = freezeItem.Value.GetAs<int>();
                if (cloudFreeze > FreezeCharges)
                    PlayerPrefs.SetInt(PrefKeyFreeze, cloudFreeze);
            }

            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyReward] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    static async Task SaveToCloudAsync(string date, int streak, int freezeCharges)
    {
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
            {
                { CloudKeyLast,   date   },
                { CloudKeyStreak, streak },
                { CloudKeyFreeze, freezeCharges }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyReward] Cloud Save fehlgeschlagen: " + e.Message);
        }
    }
}
