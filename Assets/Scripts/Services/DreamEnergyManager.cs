using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

// Traumenergie: kleinste Währung, verdient durchs Spielen (nie gekauft) — gleiche Speicher-/
// Sync-Struktur wie DiamondManager/DiamondSplinterManager (lokal per PlayerPrefs + Cloud Save
// Backup). Wird nach jeder Infinity-Mode-Session anhand des
// Endscores vergeben (siehe CalculateReward/OnGameFinished, aufgerufen von MixedPointSpawner
// direkt bei Game Over, analog zu AchievementManager/MissionManager.OnGameFinished).
public static class DreamEnergyManager
{
    const string PrefKey  = "dream_energy_balance";
    const string CloudKey = "dreamEnergy";

    // Gesetzt, wenn ein Cloud-Save fehlgeschlagen ist (z.B. Netzwerkausfall) und der lokale
    // Stand seitdem potenziell von der Cloud-Kopie abweicht. Wird bei nächster Gelegenheit
    // (RetryPendingCloudSaveIfNeeded, aufgerufen von LoadFromCloudAsync bei App-Resume) erneut
    // versucht, statt den Drift stillschweigend bestehen zu lassen.
    const string CloudDirtyPrefKey = "dream_energy_cloud_dirty";

    // Separater, nie sinkender Zähler für "insgesamt jemals verdient" — anders als Balance (die
    // beim Ausgeben im Shop sinkt), treibt dieser den Dream-Energy-Fortschrittsbalken im
    // Hauptmenü (siehe DreamEnergyProgressUI). Wird NUR in AddDreamEnergy erhöht, nie in
    // TrySpendDreamEnergy angefasst.
    const string LifetimePrefKey  = "dream_energy_lifetime_earned";
    const string LifetimeCloudKey = "dreamEnergyLifetimeEarned";

    // Ensures cloud saves are sequential — prevents out-of-order writes
    // when multiple AddDreamEnergy calls fire rapidly.
    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public static event Action<int> OnDreamEnergyChanged;
    public static event Action<int> OnLifetimeEarnedChanged;

    public static int Balance        => PlayerPrefs.GetInt(PrefKey, 0);
    public static int LifetimeEarned => PlayerPrefs.GetInt(LifetimePrefKey, 0);

    // Session-Reward-Modell (Balancing-Tabelle "Session", 2026-08): Traumenergie besteht aus einem
    // Fix-Anteil + einem variablen Anteil je Score-Punkt. Die Tabelle ist rein score-basiert — "Zeit
    // (sec)" ist dort KEINE echte Stoppuhr, sondern wird 1:1 aus dem Score abgeleitet (Score ist im
    // Spiel immer ein Vielfaches von ScoreStep=10, ein Punkt "Zeit" entspricht also ScoreStep Score).
    // TE_Fix startet bei FixStartPerSecond bei t=1 und wächst pro weiterem t um FixGrowthPerSecond
    // (siehe Tabellenspalten "fix - Start" / "Anstieg fix").
    private const int   ScoreStep          = 10;
    private const int   FixStartPerSecond  = 10;
    private const int   FixGrowthPerSecond = 15;
    private const float VariablePerScore   = 0.1f;

    public static int CalculateReward(int score)
    {
        int clampedScore = Mathf.Max(0, score);
        int t = Mathf.Max(1, clampedScore / ScoreStep);
        int teFix      = FixStartPerSecond + FixGrowthPerSecond * (t - 1);
        int teVariable = Mathf.RoundToInt(clampedScore * VariablePerScore);
        return teFix + teVariable;
    }

    // Berechnet UND vergibt sofort die Traumenergie fürs beendete Spiel — Rückgabewert dient
    // nur der Anzeige (Score Canvas), ohne dass dort nochmal neu gerechnet werden muss.
    public static int OnGameFinished(int score)
    {
        int reward = CalculateReward(score);
        AddDreamEnergy(reward);
        return reward;
    }

    public static void AddDreamEnergy(int amount)
    {
        if (amount <= 0) return;
        int newBalance  = Balance + amount;
        int newLifetime = LifetimeEarned + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.SetInt(LifetimePrefKey, newLifetime);
        PlayerPrefs.Save();
        OnDreamEnergyChanged?.Invoke(newBalance);
        OnLifetimeEarnedChanged?.Invoke(newLifetime);
        _ = SaveToCloudAsync();
        Debug.Log($"[DreamEnergy] +{amount} → {newBalance} (lifetime {newLifetime})");
    }

    public static bool TrySpendDreamEnergy(int amount)
    {
        if (amount <= 0) return false;
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnDreamEnergyChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        return true;
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKey, LifetimeCloudKey });

            if (result.TryGetValue(CloudKey, out var item))
            {
                int cloudBalance = item.Value.GetAs<int>();
                if (cloudBalance > Balance)
                {
                    PlayerPrefs.SetInt(PrefKey, cloudBalance);
                    OnDreamEnergyChanged?.Invoke(cloudBalance);
                    Debug.Log($"[DreamEnergy] Cloud wiederhergestellt: {cloudBalance}");
                }
            }
            if (result.TryGetValue(LifetimeCloudKey, out var lifetimeItem))
            {
                int cloudLifetime = lifetimeItem.Value.GetAs<int>();
                if (cloudLifetime > LifetimeEarned)
                {
                    PlayerPrefs.SetInt(LifetimePrefKey, cloudLifetime);
                    OnLifetimeEarnedChanged?.Invoke(cloudLifetime);
                    Debug.Log($"[DreamEnergy] Cloud Lifetime wiederhergestellt: {cloudLifetime}");
                }
            }
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DreamEnergy] Cloud Load fehlgeschlagen: " + e.Message);
        }

        await RetryPendingCloudSaveIfNeeded();
    }

    // Reads the current balance at the time the lock is acquired, so that even if
    // multiple saves are queued, the last one always writes the most recent value.
    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            int balance  = Balance;
            int lifetime = LifetimeEarned;
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance }, { LifetimeCloudKey, lifetime } });
            Debug.Log($"[DreamEnergy] Cloud gespeichert: {balance} (lifetime {lifetime})");
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 0);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DreamEnergy] Cloud Save fehlgeschlagen: " + e.Message);
            PlayerPrefs.SetInt(CloudDirtyPrefKey, 1);
            PlayerPrefs.Save();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Holt einen fehlgeschlagenen Cloud-Save nach, falls seit dem letzten Versuch einer
    // offen geblieben ist. Von LoadFromCloudAsync aufgerufen (bestehender App-Start/Resume-
    // Einstiegspunkt), damit ein Netzwerkausfall den lokalen/Cloud-Stand nicht dauerhaft
    // auseinanderlaufen lässt, ohne dass es auffällt.
    public static async Task RetryPendingCloudSaveIfNeeded()
    {
        if (PlayerPrefs.GetInt(CloudDirtyPrefKey, 0) == 0) return;
        Debug.Log("[DreamEnergy] Offener Cloud-Save aus vorherigem Fehlversuch wird nachgeholt.");
        await SaveToCloudAsync();
    }
}
