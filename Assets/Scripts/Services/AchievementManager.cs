using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public enum AchievementId
{
    FirstGame,
    Games10,
    Games50,
    Games100,
    Score100,
    Score500,
    Score1000,
    Score2000,
    SpecialModeAny,
    SpecialModeAll,
    Streak3,
    Streak7,
    TutorialDone,
}

public class AchievementData
{
    public AchievementId Id;
    public string Title;
    public string Description;
    public int    Reward;
    public int    Target;
}

public static class AchievementManager
{
    const string PrefKeyGamesTotal   = "ach_games_total";
    const string PrefKeySpecialMask  = "ach_special_mask";  // bitmask: 1=gravity 2=gold 4=fountain
    const string PrefKeyCompletedSet  = "ach_completed";    // comma-separated IDs
    const string CloudKeyCompleted    = "ach_completed";
    const string CloudKeyStats        = "ach_stats";         // JSON

    public static event Action<AchievementData, int> OnAchievementUnlocked; // data, coinsEarned

    static readonly List<AchievementData> All = new()
    {
        new() { Id = AchievementId.FirstGame,       Title = "First Steps",        Description = "Play your first game",                  Reward = 50,  Target = 1   },
        new() { Id = AchievementId.Games10,         Title = "Getting Started",    Description = "Play 10 games",                         Reward = 100, Target = 10  },
        new() { Id = AchievementId.Games50,         Title = "Dedicated",          Description = "Play 50 games",                         Reward = 300, Target = 50  },
        new() { Id = AchievementId.Games100,        Title = "Century Club",       Description = "Play 100 games",                        Reward = 500, Target = 100 },
        new() { Id = AchievementId.Score100,        Title = "Beginner",           Description = "Score 100 points in a single game",     Reward = 50,  Target = 100 },
        new() { Id = AchievementId.Score500,        Title = "High Scorer",        Description = "Score 500 points in a single game",     Reward = 150, Target = 500 },
        new() { Id = AchievementId.Score1000,       Title = "Elite",              Description = "Score 1000 points in a single game",    Reward = 300, Target = 1000},
        new() { Id = AchievementId.Score2000,       Title = "Legend",             Description = "Score 2000 points in a single game",    Reward = 500, Target = 2000},
        new() { Id = AchievementId.SpecialModeAny,  Title = "Special Fan",        Description = "Activate any Special Mode",             Reward = 100, Target = 1   },
        new() { Id = AchievementId.SpecialModeAll,  Title = "Mode Explorer",      Description = "Activate all 3 Special Modes",          Reward = 200, Target = 7   }, // bitmask 7 = all 3
        new() { Id = AchievementId.Streak3,         Title = "Streak Starter",     Description = "Log in 3 days in a row",                Reward = 100, Target = 3   },
        new() { Id = AchievementId.Streak7,         Title = "Streak Master",      Description = "Log in 7 days in a row",                Reward = 300, Target = 7   },
        new() { Id = AchievementId.TutorialDone,    Title = "Tutorial Graduate",  Description = "Complete the tutorial",                 Reward = 100, Target = 1   },
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static bool IsCompleted(AchievementId id) =>
        GetCompletedSet().Contains(id.ToString());

    public static IReadOnlyList<AchievementData> GetAll() => All;

    public static void OnGameFinished(int score, GameMode mode)
    {
        int games = PlayerPrefs.GetInt(PrefKeyGamesTotal, 0) + 1;
        PlayerPrefs.SetInt(PrefKeyGamesTotal, games);

        PlayerPrefs.Save();

        TryUnlock(AchievementId.FirstGame, games);
        TryUnlock(AchievementId.Games10,   games);
        TryUnlock(AchievementId.Games50,   games);
        TryUnlock(AchievementId.Games100,  games);

        TryUnlock(AchievementId.Score100,  score);
        TryUnlock(AchievementId.Score500,  score);
        TryUnlock(AchievementId.Score1000, score);
        TryUnlock(AchievementId.Score2000, score);

        _ = SaveStatsToCloudAsync();
    }

    public static void OnSpecialModeTriggered(string type)
    {
        int mask = PlayerPrefs.GetInt(PrefKeySpecialMask, 0);
        int bit  = type switch { "gravity" => 1, "gold" => 2, "fountain" => 4, _ => 0 };
        if (bit == 0) return;

        mask |= bit;
        PlayerPrefs.SetInt(PrefKeySpecialMask, mask);
        PlayerPrefs.Save();

        TryUnlock(AchievementId.SpecialModeAny, mask > 0 ? 1 : 0);
        TryUnlock(AchievementId.SpecialModeAll, mask);
    }

    public static void OnTutorialCompleted()
    {
        TryUnlock(AchievementId.TutorialDone, 1);
    }

    public static void OnStreakReached(int streak)
    {
        TryUnlock(AchievementId.Streak3, streak);
        TryUnlock(AchievementId.Streak7, streak);
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKeyCompleted, CloudKeyStats });

            if (result.TryGetValue(CloudKeyCompleted, out var completedItem))
            {
                string cloudCompleted = completedItem.Value.GetAs<string>();
                string localCompleted = PlayerPrefs.GetString(PrefKeyCompletedSet, "");
                // Merge: keep all completed from either source
                var merged = new HashSet<string>(localCompleted.Split(','));
                foreach (var id in cloudCompleted.Split(','))
                    merged.Add(id);
                merged.Remove("");
                PlayerPrefs.SetString(PrefKeyCompletedSet, string.Join(",", merged));
            }

            if (result.TryGetValue(CloudKeyStats, out var statsItem))
            {
                // Stats: take max of cloud vs local for each stat
                try
                {
                    var stats = JsonUtility.FromJson<AchievementStats>(statsItem.Value.GetAs<string>());
                    if (stats.gamesTotal > PlayerPrefs.GetInt(PrefKeyGamesTotal, 0))
                        PlayerPrefs.SetInt(PrefKeyGamesTotal, stats.gamesTotal);
                    if (stats.specialMask > PlayerPrefs.GetInt(PrefKeySpecialMask, 0))
                        PlayerPrefs.SetInt(PrefKeySpecialMask, stats.specialMask);
                }
                catch { }
            }

            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Achievements] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    static void TryUnlock(AchievementId id, int currentValue)
    {
        if (IsCompleted(id)) return;
        var data = All.Find(a => a.Id == id);
        if (data == null || currentValue < data.Target) return;

        var set = GetCompletedSet();
        set.Add(id.ToString());
        PlayerPrefs.SetString(PrefKeyCompletedSet, string.Join(",", set));
        PlayerPrefs.Save();

        CoinManager.AddCoins(data.Reward);
        RewardNotificationQueue.Enqueue(data.Title, data.Description, data.Reward);
        Debug.Log($"[Achievement] '{data.Title}' unlocked! +{data.Reward} Coins");
        OnAchievementUnlocked?.Invoke(data, data.Reward);

        _ = SaveCompletedToCloudAsync();
    }

    static HashSet<string> GetCompletedSet()
    {
        string raw = PlayerPrefs.GetString(PrefKeyCompletedSet, "");
        var set = new HashSet<string>(raw.Split(','));
        set.Remove("");
        return set;
    }

    static async Task SaveCompletedToCloudAsync()
    {
        try
        {
            string completed = PlayerPrefs.GetString(PrefKeyCompletedSet, "");
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKeyCompleted, completed } });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Achievements] Cloud Save (completed) fehlgeschlagen: " + e.Message);
        }
    }

    static async Task SaveStatsToCloudAsync()
    {
        try
        {
            var stats = new AchievementStats
            {
                gamesTotal  = PlayerPrefs.GetInt(PrefKeyGamesTotal, 0),
                specialMask = PlayerPrefs.GetInt(PrefKeySpecialMask, 0),
            };
            string json = JsonUtility.ToJson(stats);
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKeyStats, json } });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Achievements] Cloud Save (stats) fehlgeschlagen: " + e.Message);
        }
    }

    [Serializable]
    class AchievementStats
    {
        public int gamesTotal;
        public int specialMask;
    }
}
