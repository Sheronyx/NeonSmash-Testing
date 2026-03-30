using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class PendingScore
{
    public string leaderboardId;
    public long score;
    public long timestamp;
}

public class OfflineScoreSync : MonoBehaviour
{
    public static OfflineScoreSync Instance { get; private set; }

    [SerializeField] string prefsKey = "lb_pending_v2";
    [SerializeField] float reachabilityCheckSeconds = 2f;

    readonly List<PendingScore> queue = new();
    bool watching = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void OnApplicationFocus(bool hasFocus) { if (hasFocus) _ = TryFlushAsync(); }
    void OnApplicationPause(bool paused) { if (!paused) _ = TryFlushAsync(); }

    public void Enqueue(string leaderboardId, long score)
    {
        if (string.IsNullOrWhiteSpace(leaderboardId)) return;
        var i = queue.FindIndex(p => p.leaderboardId == leaderboardId);
        if (i >= 0)
        {
            if (score > queue[i].score) queue[i].score = score;
        }
        else
        {
            queue.Add(new PendingScore { leaderboardId = leaderboardId, score = score,
                                         timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }
        Save();
        Debug.Log($"[OfflineScoreSync] queued {score} -> '{leaderboardId}' (pending={queue.Count})");
        EnsureWatcher();
    }

    public async Task<bool> TryFlushAsync()
    {
        if (queue.Count == 0) return true;
        if (Application.internetReachability == NetworkReachability.NotReachable)
        { EnsureWatcher(); return false; }

        bool online = await UgsBootstrap.Initialization;
        if (!online) return false;

        var snap = new List<PendingScore>(queue);
        bool success = false;

        foreach (var s in snap)
        {
            try
            {
                await LeaderboardApi.SubmitScoreAsync(s.score, s.leaderboardId);
                queue.RemoveAll(p => p.leaderboardId == s.leaderboardId && p.score <= s.score);
                success = true;
                Debug.Log($"[OfflineScoreSync] flushed {s.score} -> '{s.leaderboardId}'");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OfflineScoreSync] flush failed '{s.leaderboardId}': {e.Message}");
            }
        }
        if (success) Save();
        if (queue.Count > 0) EnsureWatcher();
        return success && queue.Count == 0;
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var go = new GameObject("OfflineScoreSync");
        go.AddComponent<OfflineScoreSync>();
        DontDestroyOnLoad(go);
    }

    void Load()
    {
        try
        {
            var j = PlayerPrefs.GetString(prefsKey, "");
            if (!string.IsNullOrEmpty(j))
            {
                var w = JsonUtility.FromJson<Wrapper>(j);
                if (w?.items != null) queue.AddRange(w.items);
            }
        } catch { }
    }

    void Save()
    {
        try
        {
            var w = new Wrapper { items = queue.ToArray() };
            var j = JsonUtility.ToJson(w);
            PlayerPrefs.SetString(prefsKey, j);
            PlayerPrefs.Save();
        } catch { }
    }

    [Serializable] class Wrapper { public PendingScore[] items; }

    void EnsureWatcher()
    {
        if (watching || queue.Count == 0) return;
        StartCoroutine(W());
    }

    System.Collections.IEnumerator W()
    {
        watching = true;
        while (queue.Count > 0 && Application.internetReachability == NetworkReachability.NotReachable)
            yield return new WaitForSecondsRealtime(reachabilityCheckSeconds);
        watching = false;
        if (queue.Count > 0) _ = TryFlushAsync();
    }
}
