using System;
using UnityEngine;

public enum MissionType
{
    PlayGames,        // Spiele X Spiele heute
    ScoreInOneGame,   // Erreiche X Punkte in einem Spiel
    TriggerSpecialMode, // Aktiviere einen Special Mode
}

[Serializable]
public class MissionData
{
    public MissionType Type;
    public int         Target;
    public int         Reward;
    public int         Progress;
    public bool        IsCompleted;

    public string Title => Type switch
    {
        MissionType.PlayGames           => $"Play {Target} games today",
        MissionType.ScoreInOneGame      => $"Score {Target} points in a single game",
        MissionType.TriggerSpecialMode  => "Activate any Special Mode",
        _                               => ""
    };
}

public static class MissionManager
{
    const string PrefKeyDate      = "mission_date";
    const int    MissionCount     = 3;

    public static event Action<MissionData, int> OnMissionCompleted; // mission, coinsEarned

    static MissionData[] _missions;

    // All possible missions (pool to pick from daily)
    static readonly MissionData[] Pool =
    {
        new() { Type = MissionType.PlayGames,          Target = 2,   Reward = 100 },
        new() { Type = MissionType.PlayGames,          Target = 5,   Reward = 175 },
        new() { Type = MissionType.PlayGames,          Target = 10,  Reward = 250 },
        new() { Type = MissionType.ScoreInOneGame,     Target = 100, Reward = 100 },
        new() { Type = MissionType.ScoreInOneGame,     Target = 300, Reward = 175 },
        new() { Type = MissionType.ScoreInOneGame,     Target = 500, Reward = 250 },
        new() { Type = MissionType.TriggerSpecialMode, Target = 1,   Reward = 125 },
    };

    public static MissionData[] GetTodaysMissions()
    {
        EnsureMissionsLoaded();
        return _missions;
    }

    public static void OnGameFinished(int score)
    {
        EnsureMissionsLoaded();
        bool anyUpdated = false;

        foreach (var m in _missions)
        {
            if (m.IsCompleted) continue;

            switch (m.Type)
            {
                case MissionType.PlayGames:
                    m.Progress++;
                    anyUpdated = true;
                    break;
                case MissionType.ScoreInOneGame:
                    if (score > m.Progress)
                    {
                        m.Progress = score;
                        anyUpdated = true;
                    }
                    break;
            }

            if (!m.IsCompleted && m.Progress >= m.Target)
                CompleteMission(m);
        }

        if (anyUpdated) SaveMissions();
    }

    public static void OnSpecialModeTriggered()
    {
        EnsureMissionsLoaded();

        foreach (var m in _missions)
        {
            if (m.IsCompleted || m.Type != MissionType.TriggerSpecialMode) continue;
            m.Progress = 1;
            CompleteMission(m);
        }

        SaveMissions();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    static void EnsureMissionsLoaded()
    {
        if (_missions != null && _missions.Length == MissionCount)
        {
            // Check if date changed → regenerate
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (PlayerPrefs.GetString(PrefKeyDate, "") == today) return;
        }

        string date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (PlayerPrefs.GetString(PrefKeyDate, "") == date)
        {
            _missions = LoadMissions();
        }
        else
        {
            // New day — generate fresh missions seeded by date + player ID
            string pid = "";
            try { pid = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId ?? ""; } catch { }
            int seed = (date + pid).GetHashCode();
            _missions = GenerateMissions(seed);
            PlayerPrefs.SetString(PrefKeyDate, date);
            SaveMissions();
        }
    }

    static MissionData[] GenerateMissions(int seed)
    {
        var rng       = new System.Random(seed);
        var chosen    = new MissionData[MissionCount];
        var usedTypes = new System.Collections.Generic.HashSet<MissionType>();

        // Shuffle pool indices
        int[] indices = new int[Pool.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = 0;
        foreach (int idx in indices)
        {
            if (count >= MissionCount) break;
            var candidate = Pool[idx];
            // Allow at most one mission per type
            if (usedTypes.Contains(candidate.Type)) continue;
            usedTypes.Add(candidate.Type);
            chosen[count++] = new MissionData
            {
                Type     = candidate.Type,
                Target   = candidate.Target,
                Reward   = candidate.Reward,
                Progress = 0,
                IsCompleted = false
            };
        }

        return chosen;
    }

    static void CompleteMission(MissionData m)
    {
        m.IsCompleted = true;
        CoinManager.AddCoins(m.Reward);
        RewardNotificationQueue.Enqueue("Mission Complete", m.Title, m.Reward);
        Debug.Log($"[Mission] '{m.Title}' completed! +{m.Reward} Coins");
        OnMissionCompleted?.Invoke(m, m.Reward);
    }

    static void SaveMissions()
    {
        for (int i = 0; i < MissionCount; i++)
        {
            var m = _missions[i];
            PlayerPrefs.SetInt($"mission_{i}_type",      (int)m.Type);
            PlayerPrefs.SetInt($"mission_{i}_target",    m.Target);
            PlayerPrefs.SetInt($"mission_{i}_reward",    m.Reward);
            PlayerPrefs.SetInt($"mission_{i}_progress",  m.Progress);
            PlayerPrefs.SetInt($"mission_{i}_done",      m.IsCompleted ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    static MissionData[] LoadMissions()
    {
        var result = new MissionData[MissionCount];
        for (int i = 0; i < MissionCount; i++)
        {
            result[i] = new MissionData
            {
                Type        = (MissionType)PlayerPrefs.GetInt($"mission_{i}_type",     0),
                Target      = PlayerPrefs.GetInt($"mission_{i}_target",   1),
                Reward      = PlayerPrefs.GetInt($"mission_{i}_reward",   100),
                Progress    = PlayerPrefs.GetInt($"mission_{i}_progress", 0),
                IsCompleted = PlayerPrefs.GetInt($"mission_{i}_done",     0) == 1,
            };
        }
        return result;
    }
}
