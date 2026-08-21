using NUnit.Framework;
using UnityEngine;
using System.Threading.Tasks;

// Prüft den Dirty-Flag-Mechanismus für fehlgeschlagene Cloud-Saves in DiamondManager und
// DiamondSplinterManager — gleiches Muster wie DreamEnergyManagerCloudDirtyTests, siehe dort für
// die Begründung, warum ein Fehlschlag im Editor-Testkontext deterministisch ist.
public class CurrencyManagersCloudDirtyTests
{
    const string KeyDiamondsBalance = "diamonds_balance";
    const string KeyDiamondsDirty   = "diamonds_cloud_dirty";
    const string KeySplintersBalance = "diamond_splinters_balance";
    const string KeySplintersDirty   = "diamond_splinters_cloud_dirty";

    int _realDiamondsBalance, _realDiamondsDirty, _realSplintersBalance, _realSplintersDirty;

    [SetUp]
    public void SaveRealState()
    {
        _realDiamondsBalance  = PlayerPrefs.GetInt(KeyDiamondsBalance, 0);
        _realDiamondsDirty    = PlayerPrefs.GetInt(KeyDiamondsDirty, 0);
        _realSplintersBalance = PlayerPrefs.GetInt(KeySplintersBalance, 0);
        _realSplintersDirty   = PlayerPrefs.GetInt(KeySplintersDirty, 0);
    }

    [TearDown]
    public void RestoreRealState()
    {
        PlayerPrefs.SetInt(KeyDiamondsBalance, _realDiamondsBalance);
        PlayerPrefs.SetInt(KeyDiamondsDirty, _realDiamondsDirty);
        PlayerPrefs.SetInt(KeySplintersBalance, _realSplintersBalance);
        PlayerPrefs.SetInt(KeySplintersDirty, _realSplintersDirty);
        PlayerPrefs.Save();
    }

    [Test]
    public async Task AddDiamonds_CloudSaveFailsInEditor_SetsDirtyFlag()
    {
        PlayerPrefs.SetInt(KeyDiamondsDirty, 0);

        DiamondManager.AddDiamonds(1);

        bool becameDirty = false;
        for (int i = 0; i < 50 && !becameDirty; i++)
        {
            if (PlayerPrefs.GetInt(KeyDiamondsDirty, 0) == 1) becameDirty = true;
            else await Task.Delay(20);
        }

        Assert.IsTrue(becameDirty, "Dirty-Flag sollte nach fehlgeschlagenem Cloud-Save gesetzt sein.");
    }

    [Test]
    public async Task DiamondManager_RetryWithoutDirtyFlag_DoesNothing()
    {
        PlayerPrefs.SetInt(KeyDiamondsDirty, 0);
        await DiamondManager.RetryPendingCloudSaveIfNeeded();
        Assert.AreEqual(0, PlayerPrefs.GetInt(KeyDiamondsDirty, 0));
    }

    [Test]
    public async Task DiamondManager_RetryWithDirtyFlag_KeepsFlagOnRepeatedFailure()
    {
        PlayerPrefs.SetInt(KeyDiamondsDirty, 1);
        await DiamondManager.RetryPendingCloudSaveIfNeeded();
        Assert.AreEqual(1, PlayerPrefs.GetInt(KeyDiamondsDirty, 0));
    }

    [Test]
    public async Task AddSplinters_CloudSaveFailsInEditor_SetsDirtyFlag()
    {
        PlayerPrefs.SetInt(KeySplintersDirty, 0);

        DiamondSplinterManager.AddSplinters(1);

        bool becameDirty = false;
        for (int i = 0; i < 50 && !becameDirty; i++)
        {
            if (PlayerPrefs.GetInt(KeySplintersDirty, 0) == 1) becameDirty = true;
            else await Task.Delay(20);
        }

        Assert.IsTrue(becameDirty, "Dirty-Flag sollte nach fehlgeschlagenem Cloud-Save gesetzt sein.");
    }

    [Test]
    public async Task DiamondSplinterManager_RetryWithoutDirtyFlag_DoesNothing()
    {
        PlayerPrefs.SetInt(KeySplintersDirty, 0);
        await DiamondSplinterManager.RetryPendingCloudSaveIfNeeded();
        Assert.AreEqual(0, PlayerPrefs.GetInt(KeySplintersDirty, 0));
    }

    [Test]
    public async Task DiamondSplinterManager_RetryWithDirtyFlag_KeepsFlagOnRepeatedFailure()
    {
        PlayerPrefs.SetInt(KeySplintersDirty, 1);
        await DiamondSplinterManager.RetryPendingCloudSaveIfNeeded();
        Assert.AreEqual(1, PlayerPrefs.GetInt(KeySplintersDirty, 0));
    }
}
