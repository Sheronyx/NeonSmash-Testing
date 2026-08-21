using System;
using NUnit.Framework;
using UnityEngine;

// EditMode-Tests für DailyRewardManager (statische, PlayerPrefs-basierte Klasse).
// Sichert/stellt die echten PlayerPrefs-Werte in Setup/TearDown wieder her, damit
// ein Testlauf den tatsächlichen Spielstand im Editor nicht verändert.
public class DailyRewardManagerTests
{
    const string KeyLastDate = "daily_last_date";
    const string KeyStreak   = "daily_streak";
    const string KeyFreeze   = "daily_freeze_charges";

    string _realDate;
    int _realStreak;
    int _realFreeze;

    [SetUp]
    public void SaveRealState()
    {
        _realDate   = PlayerPrefs.GetString(KeyLastDate, "");
        _realStreak = PlayerPrefs.GetInt(KeyStreak, 0);
        _realFreeze = PlayerPrefs.GetInt(KeyFreeze, 0);
    }

    [TearDown]
    public void RestoreRealState()
    {
        PlayerPrefs.SetString(KeyLastDate, _realDate);
        PlayerPrefs.SetInt(KeyStreak, _realStreak);
        PlayerPrefs.SetInt(KeyFreeze, _realFreeze);
        PlayerPrefs.Save();
    }

    static string DaysAgo(int n) => DateTime.UtcNow.AddDays(-n).ToString("yyyy-MM-dd");

    [Test]
    public void AddFreezeCharge_IncrementsCount()
    {
        PlayerPrefs.SetInt(KeyFreeze, 0);

        DailyRewardManager.AddFreezeCharge(2);

        Assert.AreEqual(2, DailyRewardManager.FreezeCharges);
    }

    [Test]
    public void AddFreezeCharge_IgnoresZeroOrNegative()
    {
        PlayerPrefs.SetInt(KeyFreeze, 1);

        DailyRewardManager.AddFreezeCharge(0);
        DailyRewardManager.AddFreezeCharge(-3);

        Assert.AreEqual(1, DailyRewardManager.FreezeCharges);
    }

    [Test]
    public void ClaimTodayReward_MissedDayWithoutFreezeCharge_ResetsStreakToOne()
    {
        PlayerPrefs.SetString(KeyLastDate, DaysAgo(2));
        PlayerPrefs.SetInt(KeyStreak, 5);
        PlayerPrefs.SetInt(KeyFreeze, 0);

        DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(1, DailyRewardManager.CurrentStreak);
    }

    [Test]
    public void ClaimTodayReward_MissedExactlyOneDayWithFreezeCharge_PreservesStreakAndConsumesCharge()
    {
        PlayerPrefs.SetString(KeyLastDate, DaysAgo(2));
        PlayerPrefs.SetInt(KeyStreak, 5);
        PlayerPrefs.SetInt(KeyFreeze, 1);

        int reward = DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(6, DailyRewardManager.CurrentStreak, "Streak sollte trotz verpasstem Tag weiterlaufen (Freeze verbraucht).");
        Assert.AreEqual(0, DailyRewardManager.FreezeCharges, "Freeze-Charge sollte verbraucht sein.");
        Assert.Greater(reward, 0);
    }

    [Test]
    public void ClaimTodayReward_MissedMultipleDaysEvenWithFreezeCharge_ResetsStreak()
    {
        // Freeze deckt bewusst nur genau EINEN verpassten Tag ab, keine längeren Lücken.
        PlayerPrefs.SetString(KeyLastDate, DaysAgo(3));
        PlayerPrefs.SetInt(KeyStreak, 5);
        PlayerPrefs.SetInt(KeyFreeze, 1);

        DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(1, DailyRewardManager.CurrentStreak);
        Assert.AreEqual(1, DailyRewardManager.FreezeCharges, "Freeze-Charge darf bei mehrtägiger Lücke nicht verbraucht werden.");
    }

    [Test]
    public void ClaimTodayReward_StreakContinuesNormally_DoesNotTouchFreezeCharges()
    {
        PlayerPrefs.SetString(KeyLastDate, DaysAgo(1));
        PlayerPrefs.SetInt(KeyStreak, 3);
        PlayerPrefs.SetInt(KeyFreeze, 2);

        DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(4, DailyRewardManager.CurrentStreak);
        Assert.AreEqual(2, DailyRewardManager.FreezeCharges, "Bei normal fortlaufendem Streak darf keine Freeze-Charge verbraucht werden.");
    }

    [Test]
    public void ClaimTodayReward_AlreadyClaimedToday_ReturnsZeroAndDoesNotChangeStreak()
    {
        PlayerPrefs.SetString(KeyLastDate, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetInt(KeyStreak, 7);
        PlayerPrefs.SetInt(KeyFreeze, 0);

        int reward = DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(0, reward);
        Assert.AreEqual(7, DailyRewardManager.CurrentStreak);
    }

    [Test]
    public void ClaimTodayReward_RewardTierWrapsAfterDaySeven()
    {
        // Streak 7 -> Tier 7 (500), Streak 8 -> Tier 1 (50), siehe RewardTierIndex-Kommentar in DailyRewardManager.
        PlayerPrefs.SetString(KeyLastDate, DaysAgo(1));
        PlayerPrefs.SetInt(KeyStreak, 7);
        PlayerPrefs.SetInt(KeyFreeze, 0);

        int reward = DailyRewardManager.ClaimTodayReward();

        Assert.AreEqual(50, reward, "Streak 8 sollte auf Reward-Tier 1 (Tag 1 der 7er-Zyklus-Wiederholung) wrappen.");
        Assert.AreEqual(8, DailyRewardManager.CurrentStreak, "Der rohe Streak-Zähler bleibt unclamped/monoton wachsend.");
    }
}
