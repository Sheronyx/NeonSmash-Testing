using NUnit.Framework;
using UnityEngine;

// Prüft die reine Streak-/Multiplier-Logik in ComboManager (kein PlayerPrefs, kein Cloud-Save
// betroffen). Jeder Test erzeugt seine eigene Instanz und zerstört sie danach wieder, damit
// ComboManager.Instance nicht zwischen Tests oder mit einer laufenden Play-Mode-Session kollidiert.
public class ComboManagerTests
{
    ComboManager _combo;

    [SetUp]
    public void CreateInstance()
    {
        var go = new GameObject("ComboManagerTestInstance");
        _combo = go.AddComponent<ComboManager>();
    }

    [TearDown]
    public void DestroyInstance()
    {
        if (_combo != null) Object.DestroyImmediate(_combo.gameObject);
    }

    [Test]
    public void RegisterHit_SameColorFourTimes_ComboNotYetActive()
    {
        for (int i = 0; i < 4; i++) _combo.RegisterHit(PointColor.Pink);

        Assert.AreEqual(4, _combo.ComboCount);
        Assert.IsFalse(_combo.IsComboActive);
        Assert.AreEqual(1, _combo.Multiplier);
    }

    [Test]
    public void RegisterHit_SameColorFiveTimes_ActivatesCombo()
    {
        for (int i = 0; i < 5; i++) _combo.RegisterHit(PointColor.Green);

        Assert.AreEqual(5, _combo.ComboCount);
        Assert.IsTrue(_combo.IsComboActive);
        Assert.AreEqual(5, _combo.Multiplier);
        Assert.AreEqual(PointColor.Green, _combo.CurrentColor);
    }

    [Test]
    public void RegisterHit_ColorChange_ResetsStreakToOne()
    {
        for (int i = 0; i < 5; i++) _combo.RegisterHit(PointColor.Blue);
        Assert.IsTrue(_combo.IsComboActive);

        _combo.RegisterHit(PointColor.Pink);

        Assert.AreEqual(1, _combo.ComboCount);
        Assert.IsFalse(_combo.IsComboActive);
        Assert.AreEqual(PointColor.Pink, _combo.CurrentColor);
    }

    [Test]
    public void RegisterMiss_DuringActiveCombo_BreaksCombo()
    {
        for (int i = 0; i < 6; i++) _combo.RegisterHit(PointColor.Pink);
        Assert.IsTrue(_combo.IsComboActive);

        _combo.RegisterMiss();

        Assert.AreEqual(0, _combo.ComboCount);
        Assert.IsFalse(_combo.IsComboActive);
        Assert.IsNull(_combo.CurrentColor);
        Assert.AreEqual(1, _combo.Multiplier);
    }

    [Test]
    public void OnComboChanged_FiresWithCurrentStreakOnEachHit()
    {
        int lastReported = -1;
        System.Action<int> handler = streak => lastReported = streak;
        ComboManager.OnComboChanged += handler;

        try
        {
            _combo.RegisterHit(PointColor.Blue);
            Assert.AreEqual(1, lastReported);

            _combo.RegisterHit(PointColor.Blue);
            Assert.AreEqual(2, lastReported);

            _combo.ResetCombo();
            Assert.AreEqual(0, lastReported);
        }
        finally
        {
            ComboManager.OnComboChanged -= handler;
        }
    }
}
