using NUnit.Framework;
using UnityEngine;

// Prüft den Dirty-Flag-Mechanismus für fehlgeschlagene Cloud-Saves in DreamEnergyManager.
// CloudSaveService ist im Editor-Testkontext nie initialisiert, jeder SaveToCloudAsync-Aufruf
// schlägt also garantiert fehl (catch-Pfad) — das macht den Fehlerfall deterministisch testbar,
// ohne echte Unity-Services-Verbindung zu brauchen.
public class DreamEnergyManagerCloudDirtyTests
{
    const string KeyDirty     = "dream_energy_cloud_dirty";
    const string KeyBalance   = "dream_energy_balance";
    const string KeyLifetime  = "dream_energy_lifetime_earned";

    int _realBalance, _realLifetime, _realDirty;

    [SetUp]
    public void SaveRealState()
    {
        _realBalance  = PlayerPrefs.GetInt(KeyBalance, 0);
        _realLifetime = PlayerPrefs.GetInt(KeyLifetime, 0);
        _realDirty    = PlayerPrefs.GetInt(KeyDirty, 0);
    }

    [TearDown]
    public void RestoreRealState()
    {
        PlayerPrefs.SetInt(KeyBalance, _realBalance);
        PlayerPrefs.SetInt(KeyLifetime, _realLifetime);
        PlayerPrefs.SetInt(KeyDirty, _realDirty);
        PlayerPrefs.Save();
    }

    [Test]
    public async System.Threading.Tasks.Task AddDreamEnergy_CloudSaveFailsInEditor_SetsDirtyFlag()
    {
        PlayerPrefs.SetInt(KeyDirty, 0);

        DreamEnergyManager.AddDreamEnergy(10);

        // SaveToCloudAsync läuft bewusst fire-and-forget (kein awaitbares Handle nach außen) -
        // hier per kurzem Poll auf das Flag warten statt straight nach dem Call zu assertieren,
        // um die Race-Bedingung zu vermeiden statt sie zu verstecken.
        bool becameDirty = false;
        for (int i = 0; i < 50 && !becameDirty; i++)
        {
            if (PlayerPrefs.GetInt(KeyDirty, 0) == 1) becameDirty = true;
            else await System.Threading.Tasks.Task.Delay(20);
        }

        Assert.IsTrue(becameDirty, "Dirty-Flag sollte nach fehlgeschlagenem Cloud-Save (spätestens nach 1s) gesetzt sein.");
    }

    [Test]
    public async System.Threading.Tasks.Task RetryPendingCloudSaveIfNeeded_WithoutDirtyFlag_DoesNothing()
    {
        PlayerPrefs.SetInt(KeyDirty, 0);

        await DreamEnergyManager.RetryPendingCloudSaveIfNeeded();

        Assert.AreEqual(0, PlayerPrefs.GetInt(KeyDirty, 0), "Ohne gesetztes Dirty-Flag darf RetryPendingCloudSaveIfNeeded es nicht verändern.");
    }

    [Test]
    public async System.Threading.Tasks.Task RetryPendingCloudSaveIfNeeded_WithDirtyFlag_AttemptsSaveAndKeepsFlagOnRepeatedFailure()
    {
        PlayerPrefs.SetInt(KeyDirty, 1);

        await DreamEnergyManager.RetryPendingCloudSaveIfNeeded();

        // CloudSaveService ist im Editor-Test nicht initialisiert -> Retry schlägt ebenfalls
        // fehl -> Flag bleibt bewusst auf 1 (kein Datenverlust-Risiko durch fälschliches Löschen).
        Assert.AreEqual(1, PlayerPrefs.GetInt(KeyDirty, 0), "Bei erneutem Fehlschlag muss das Dirty-Flag gesetzt bleiben.");
    }
}
