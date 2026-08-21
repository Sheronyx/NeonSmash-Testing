using NUnit.Framework;
using UnityEngine;
using System.Threading.Tasks;

// Prüft den Dirty-Flag-Mechanismus für die zwei unabhängigen Cloud-Save-Pfade in
// AchievementManager (completed-Set, Stats-JSON) — gleiches Muster wie
// DreamEnergyManagerCloudDirtyTests, siehe dort für die Begründung.
public class AchievementManagerCloudDirtyTests
{
    const string KeyGamesTotal    = "ach_games_total";
    const string KeyCompletedDirty = "ach_completed_cloud_dirty";
    const string KeyStatsDirty     = "ach_stats_cloud_dirty";

    int _realGamesTotal, _realCompletedDirty, _realStatsDirty;

    [SetUp]
    public void SaveRealState()
    {
        _realGamesTotal     = PlayerPrefs.GetInt(KeyGamesTotal, 0);
        _realCompletedDirty = PlayerPrefs.GetInt(KeyCompletedDirty, 0);
        _realStatsDirty     = PlayerPrefs.GetInt(KeyStatsDirty, 0);
    }

    [TearDown]
    public void RestoreRealState()
    {
        PlayerPrefs.SetInt(KeyGamesTotal, _realGamesTotal);
        PlayerPrefs.SetInt(KeyCompletedDirty, _realCompletedDirty);
        PlayerPrefs.SetInt(KeyStatsDirty, _realStatsDirty);
        PlayerPrefs.Save();
    }

    [Test]
    public async Task OnGameFinished_CloudSaveFailsInEditor_SetsStatsDirtyFlag()
    {
        PlayerPrefs.SetInt(KeyStatsDirty, 0);

        AchievementManager.OnGameFinished(0, GameMode.Infinity);

        bool becameDirty = false;
        for (int i = 0; i < 50 && !becameDirty; i++)
        {
            if (PlayerPrefs.GetInt(KeyStatsDirty, 0) == 1) becameDirty = true;
            else await Task.Delay(20);
        }

        Assert.IsTrue(becameDirty, "Stats-Dirty-Flag sollte nach fehlgeschlagenem Cloud-Save gesetzt sein.");
    }

    [Test]
    public async Task RetryPendingCloudSavesIfNeeded_WithoutDirtyFlags_DoesNothing()
    {
        PlayerPrefs.SetInt(KeyCompletedDirty, 0);
        PlayerPrefs.SetInt(KeyStatsDirty, 0);

        await AchievementManager.RetryPendingCloudSavesIfNeeded();

        Assert.AreEqual(0, PlayerPrefs.GetInt(KeyCompletedDirty, 0));
        Assert.AreEqual(0, PlayerPrefs.GetInt(KeyStatsDirty, 0));
    }

    [Test]
    public async Task RetryPendingCloudSavesIfNeeded_WithBothDirtyFlags_KeepsThemOnRepeatedFailure()
    {
        PlayerPrefs.SetInt(KeyCompletedDirty, 1);
        PlayerPrefs.SetInt(KeyStatsDirty, 1);

        await AchievementManager.RetryPendingCloudSavesIfNeeded();

        Assert.AreEqual(1, PlayerPrefs.GetInt(KeyCompletedDirty, 0));
        Assert.AreEqual(1, PlayerPrefs.GetInt(KeyStatsDirty, 0));
    }
}
